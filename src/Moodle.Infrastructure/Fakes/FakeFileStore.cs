using System.Collections.Concurrent;
using System.Security.Cryptography;
using Moodle.Application.Files;

namespace Moodle.Infrastructure.Fakes;

/// <summary>Sahte gerçekleme: bellek içi içerik deposu. Seray gerçek (yerel disk) gerçeklemeyi bağlayınca kaldırır.</summary>
public sealed class FakeFileStore : IFileStore
{
    private static readonly ConcurrentDictionary<string, byte[]> Blobs = new();

    public async Task<StoredBlob> WriteAsync(Stream content, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var existed = !Blobs.TryAdd(hash, bytes);
        return new StoredBlob(hash, bytes.LongLength, $"{hash[..2]}/{hash[2..4]}/{hash}", existed);
    }

    public Task<Stream?> OpenReadAsync(string contentHash, CancellationToken ct = default)
        => Task.FromResult<Stream?>(Blobs.TryGetValue(contentHash, out var b) ? new MemoryStream(b, writable: false) : null);

    public Task<bool> ExistsAsync(string contentHash, CancellationToken ct = default)
        => Task.FromResult(Blobs.ContainsKey(contentHash));

    public Task DeleteAsync(string contentHash, CancellationToken ct = default)
    {
        Blobs.TryRemove(contentHash, out _);
        return Task.CompletedTask;
    }
}
