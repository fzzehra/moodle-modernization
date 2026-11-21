using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moodle.Application.Files;
using Moodle.Domain.Files;
using Moodle.Infrastructure.Files;

namespace Moodle.IntegrationTests;

public sealed class FileMergeCompatibilityTests(FilesPostgresFixture fixture) : IClassFixture<FilesPostgresFixture>
{
    [Fact]
    public async Task FILE_snapshot_matches_model_after_main_merge()
    {
        await using var db = fixture.CreateContext();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("cancelled")]
    public async Task Cleanup_and_reupload_of_same_content_preserve_new_file(string deletion)
    {
        var root = Path.Combine(Path.GetTempPath(), "file-merge-" + Guid.NewGuid().ToString("N"));
        var store = new LocalDiskFileStore(new FileStoreOptions(root));
        var content = Encoding.UTF8.GetBytes("merge concurrency " + Guid.NewGuid());
        try
        {
            var blob = await store.WriteAsync(new MemoryStream(content));
            await using (var setup = fixture.CreateContext())
            {
                setup.Blobs.Add(new FileBlob {
                    ContentHash = blob.ContentHash, StorageKey = blob.StorageKey, SizeBytes = blob.SizeBytes,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-10), OrphanedAt = DateTimeOffset.UtcNow.AddDays(-10)
                });
                await setup.SaveChangesAsync();
            }
            var gated = new GatedDeleteStore(store, deletion);
            await using var cleanupDb = fixture.CreateContext();
            var cleanup = new FileMaintenanceService(cleanupDb, gated, TimeProvider.System, NullLogger<FileMaintenanceService>.Instance).RunAsync();
            await gated.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await using var uploadDb = fixture.CreateContext();
            var uploads = new DraftUploadService(uploadDb, store, new CleanTestScanner(), new UploadLimits(), TimeProvider.System);
            var draft = new FileDraft { OwnerUserId = 9876, CreatedAt = DateTimeOffset.UtcNow, LastActivityAt = DateTimeOffset.UtcNow };
            uploadDb.Drafts.Add(draft);
            await uploadDb.SaveChangesAsync();
            var upload = uploads.AddAsync(9876, draft.DraftId, "merge.txt", new MemoryStream(content), "/", false, CancellationToken.None);
            try
            {
                // Release only when upload finishes (old race) or PostgreSQL reports it waiting for the hash lock.
                await using var observer = fixture.CreateContext();
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (!upload.IsCompleted)
                {
                    var waiting = await observer.Database.SqlQuery<int>($"""
                        SELECT count(*)::int AS "Value" FROM pg_stat_activity
                        WHERE datname = current_database() AND wait_event_type = 'Lock'
                        AND query LIKE 'SELECT pg_advisory_xact_lock%'
                        """).SingleAsync();
                    if (waiting > 0) break;
                    if (DateTime.UtcNow > deadline) throw new TimeoutException("Upload neither completed nor waited for the content lock.");
                    await Task.Delay(25);
                }
            }
            finally { gated.Release.TrySetResult(); }
            if (deletion == "cancelled")
                await Assert.ThrowsAsync<OperationCanceledException>(() => cleanup.WaitAsync(TimeSpan.FromSeconds(30)));
            else
            {
                var cleaned = await cleanup.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.Equal(1, cleaned.PurgedBlobs);
                Assert.Equal(deletion == "failure" ? 1 : 0, cleaned.Errors.Count);
            }
            var result = await upload.WaitAsync(TimeSpan.FromSeconds(30));
            await using var check = fixture.CreateContext();
            Assert.True(await check.Entries.AnyAsync(e => e.Id == result.FileId && e.ContentHash == blob.ContentHash));
            Assert.True(await check.Blobs.AnyAsync(b => b.ContentHash == blob.ContentHash));
            await using var read = await store.OpenReadAsync(blob.ContentHash);
            Assert.NotNull(read);
            using var bytes = new MemoryStream();
            await read.CopyToAsync(bytes);
            Assert.Equal(content, bytes.ToArray());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class GatedDeleteStore(IFileStore inner, string deletion) : IFileStore
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task DeleteAsync(string hash, CancellationToken ct = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(45), ct);
            if (deletion == "failure") throw new IOException("Synthetic delete failure");
            if (deletion == "cancelled") throw new OperationCanceledException("Synthetic cancellation");
            await inner.DeleteAsync(hash, ct);
        }
        public Task<StoredBlob> WriteAsync(Stream content, CancellationToken ct = default) => inner.WriteAsync(content, ct);
        public Task<Stream?> OpenReadAsync(string hash, CancellationToken ct = default) => inner.OpenReadAsync(hash, ct);
        public Task<bool> ExistsAsync(string hash, CancellationToken ct = default) => inner.ExistsAsync(hash, ct);
        public IAsyncEnumerable<string> ListHashesAsync(CancellationToken ct = default) => inner.ListHashesAsync(ct);
    }
}
