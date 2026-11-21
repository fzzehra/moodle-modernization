using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Moodle.Domain.Files;
using Moodle.Infrastructure.Files;

namespace Moodle.IntegrationTests;

/// <summary>Real separate OS processes, separate Npgsql sessions and a shared disk; no process-local lock can satisfy these tests.</summary>
public sealed class FileCrossProcessTests(FilesPostgresFixture fixture) : IClassFixture<FilesPostgresFixture>
{
    [Theory]
    [InlineData(true, "success")]
    [InlineData(true, "failure")]
    [InlineData(true, "cancelled")]
    [InlineData(false, "success")]
    [InlineData(false, "failure")]
    [InlineData(false, "cancelled")]
    public async Task Shared_database_lock_preserves_content_across_processes_and_retry(bool cleanupFirst, string outcome)
    {
        var root = Path.Combine(Path.GetTempPath(), "file-process-" + Guid.NewGuid().ToString("N"));
        var content = "Cross-process content " + Guid.NewGuid();
        var disk = new LocalDiskFileStore(new FileStoreOptions(root));
        var processes = new List<Process>();
        Process Spawn(string connection, string rootPath, string mode, string result, long id, string data)
        { var process = Start(connection, rootPath, mode, result, id, data); processes.Add(process); return process; }
        try
        {
            var blob = await disk.WriteAsync(new MemoryStream(Encoding.UTF8.GetBytes(content)));
            long draftId;
            string connection;
            await using (var setup = fixture.CreateContext())
            {
                await setup.Database.ExecuteSqlRawAsync("TRUNCATE file_entry, file_draft, file_blob RESTART IDENTITY");
                connection = setup.Database.GetConnectionString()!;
                setup.Blobs.Add(new FileBlob { ContentHash = blob.ContentHash, StorageKey = blob.StorageKey, SizeBytes = blob.SizeBytes,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-10), OrphanedAt = DateTimeOffset.UtcNow.AddDays(-10) });
                var draft = new FileDraft { OwnerUserId = 777, CreatedAt = DateTimeOffset.UtcNow, LastActivityAt = DateTimeOffset.UtcNow };
                setup.Drafts.Add(draft); await setup.SaveChangesAsync(); draftId = draft.DraftId;
            }
            var first = Spawn(connection, root, cleanupFirst ? "cleanup" : "upload-gated", outcome, draftId, content);
            await Started(first);
            Assert.Equal("entered", await Read(first));
            var second = Spawn(connection, root, cleanupFirst ? "upload" : "cleanup", "success", draftId, content);
            await Started(second);
            Assert.NotEqual(first.Id, second.Id);
            await WaitForDatabaseLock();
            await first.StandardInput.WriteLineAsync("release");
            var firstResult = JsonDocument.Parse(await Read(first));
            if (cleanupFirst)
            {
                Assert.Equal(outcome != "cancelled", firstResult.RootElement.GetProperty("ok").GetBoolean());
                if (outcome != "cancelled") Assert.Equal(outcome == "failure" ? 1 : 0, firstResult.RootElement.GetProperty("errors").GetInt32());
            }
            else Assert.Equal(outcome == "success", firstResult.RootElement.GetProperty("ok").GetBoolean());
            if (!cleanupFirst && outcome != "success")
            {
                Assert.Equal("entered", await Read(second));
                await second.StandardInput.WriteLineAsync("release");
            }
            using var secondResult = JsonDocument.Parse(await Read(second));
            Assert.True(secondResult.RootElement.GetProperty("ok").GetBoolean());
            await Exited(first); await Exited(second);

            if (!cleanupFirst && outcome != "success")
            {
                await using (var rolledBack = fixture.CreateContext())
                {
                    Assert.False(await rolledBack.Entries.AnyAsync(e => e.ItemId == draftId));
                    Assert.False(await rolledBack.Blobs.AnyAsync(b => b.ContentHash == blob.ContentHash));
                }
                var retry = Spawn(connection, root, "upload", "success", draftId, content);
                await Started(retry);
                using var retryResult = JsonDocument.Parse(await Read(retry));
                Assert.True(retryResult.RootElement.GetProperty("ok").GetBoolean());
                await Exited(retry);
            }
            await using var check = fixture.CreateContext();
            Assert.Single(await check.Entries.Where(e => e.ItemId == draftId && e.DeletedAt == null).ToListAsync());
            var row = Assert.Single(await check.Blobs.Where(b => b.ContentHash == blob.ContentHash).ToListAsync());
            Assert.Null(row.OrphanedAt);
            await using var stream = await disk.OpenReadAsync(blob.ContentHash);
            Assert.NotNull(stream);
            using var bytes = new MemoryStream(); await stream.CopyToAsync(bytes);
            Assert.Equal(Encoding.UTF8.GetBytes(content), bytes.ToArray());
        }
        finally
        {
            foreach (var process in processes)
            {
                if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
                process.Dispose();
            }
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static Process Start(string connection, string root, string mode, string outcome, long draftId, string content)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var probe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Moodle.FileConcurrencyProbe/bin", configuration, "net10.0/Moodle.FileConcurrencyProbe.dll"));
        Assert.True(File.Exists(probe), "Build the test-only concurrency probe through the integration project.");
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(probe);
        var process = Process.Start(start)!;
        process.StandardInput.WriteLine(JsonSerializer.Serialize(new { Connection = connection, Root = root, Mode = mode,
            Outcome = outcome, DraftId = draftId, Content = content }));
        return process;
    }
    private static async Task<string> Read(Process process) =>
        await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(45)) ?? throw new InvalidOperationException("Probe exited before expected signal.");
    private static async Task Started(Process process)
    {
        Assert.Equal("started:" + process.Id, await Read(process));
        Assert.NotEqual(Environment.ProcessId, process.Id);
    }
    private static async Task Exited(Process process)
    {
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
        Assert.Equal(0, process.ExitCode);
    }
    private async Task WaitForDatabaseLock()
    {
        await using var observer = fixture.CreateContext();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var count = await observer.Database.SqlQuery<int>($"""
                SELECT count(*)::int AS "Value" FROM pg_stat_activity
                WHERE datname = current_database() AND wait_event_type = 'Lock'
                  AND query LIKE 'SELECT pg_advisory%lock%'
                """).SingleAsync();
            if (count > 0) return;
            await Task.Delay(25);
        }
        throw new TimeoutException("Separate process did not wait for the database content lock.");
    }
}
