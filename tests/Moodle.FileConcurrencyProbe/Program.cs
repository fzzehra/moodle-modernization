using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moodle.Application.Files;
using Moodle.Infrastructure.Files;

// Test-only worker: configuration (including DB credentials) arrives on stdin, never command arguments/logs.
var config = JsonSerializer.Deserialize<ProbeConfig>((await Console.In.ReadLineAsync())!)!;
Console.WriteLine("started:" + Environment.ProcessId);
await using var db = new FilesDbContext(new DbContextOptionsBuilder<FilesDbContext>().UseNpgsql(config.Connection).Options);
var disk = new LocalDiskFileStore(new FileStoreOptions(config.Root));
var store = new GateStore(disk, config.Mode, config.Outcome);
try
{
    if (config.Mode == "cleanup")
    {
        var result = await new FileMaintenanceService(db, store, TimeProvider.System, NullLogger<FileMaintenanceService>.Instance).RunAsync();
        Console.WriteLine(JsonSerializer.Serialize(new { ok = true, purged = result.PurgedBlobs, errors = result.Errors.Count }));
    }
    else
    {
        var service = new DraftUploadService(db, store, new CleanScanner(), new UploadLimits(), TimeProvider.System);
        var file = await service.AddAsync(777, config.DraftId, "process.txt", new MemoryStream(Encoding.UTF8.GetBytes(config.Content)),
            "/", false, CancellationToken.None);
        Console.WriteLine(JsonSerializer.Serialize(new { ok = true, fileId = file.FileId, hash = file.ContentHash }));
    }
}
catch (Exception ex) when (ex is IOException or OperationCanceledException)
{
    Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = ex.GetType().Name }));
}

record ProbeConfig(string Connection, string Root, string Mode, string Outcome, long DraftId, string Content);
sealed class CleanScanner : IVirusScanner
{
    public Task<VirusScanResult> ScanAsync(Stream content, CancellationToken ct = default) => Task.FromResult(VirusScanResult.Clean);
}
sealed class GateStore(IFileStore inner, string mode, string outcome) : IFileStore
{
    private static async Task Pause()
    {
        Console.WriteLine("entered");
        if (await Console.In.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(45)) != "release")
            throw new IOException("Missing test release");
    }
    public async Task<StoredBlob> WriteAsync(Stream content, CancellationToken ct = default)
    {
        var blob = await inner.WriteAsync(content, ct);
        if (mode == "upload-gated")
        {
            await Pause();
            if (outcome == "cancelled") throw new OperationCanceledException("Injected cancellation");
            // The uploader rejects an inconsistent store result inside its transaction and rolls it back.
            if (outcome == "failure") return blob with { SizeBytes = blob.SizeBytes + 1 };
        }
        return blob;
    }
    public async Task DeleteAsync(string hash, CancellationToken ct = default)
    {
        if (mode == "cleanup")
        {
            await Pause();
            if (outcome == "failure") throw new IOException("Injected delete failure");
            if (outcome == "cancelled") throw new OperationCanceledException("Injected cancellation");
        }
        await inner.DeleteAsync(hash, ct);
    }
    public Task<Stream?> OpenReadAsync(string hash, CancellationToken ct = default) => inner.OpenReadAsync(hash, ct);
    public Task<bool> ExistsAsync(string hash, CancellationToken ct = default) => inner.ExistsAsync(hash, ct);
    public IAsyncEnumerable<string> ListHashesAsync(CancellationToken ct = default) => inner.ListHashesAsync(ct);
}
