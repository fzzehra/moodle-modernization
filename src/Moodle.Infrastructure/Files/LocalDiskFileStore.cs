using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Moodle.Application.Files;

namespace Moodle.Infrastructure.Files;

/// <summary>
/// Yerel disk içerik deposu (WBS-2 §3.6.1, §2.5). İçerik adresli: <c>kök/ab/cd/&lt;sha256&gt;</c>.
/// <list type="bullet">
/// <item>Aynı içerik bir kez saklanır, ikinci yazma mevcut kopyayı kullanır (FILE-BR-01, 03).</item>
/// <item>Önce geçici dosyaya yazılır, tamamlanınca tek adımda yerine taşınır; yazma yarıda kesilirse depoda yarım dosya kalmaz (FILE-NFR-02).</item>
/// <item>Dosya yolu yalnızca doğrulanmış özetten üretilir, istemci verisi yola karışmaz (yol geçişi saldırısı olmaz).</item>
/// </list>
/// </summary>
public sealed class LocalDiskFileStore : IFileStore
{
    private const string TempFolder = ".tmp";

    // Unix'te File.Move(overwrite:false) "var mı kontrol et, sonra rename" yapar; rename hedefi sessizce
    // ezer. Bu yüzden eşzamanlı yazımlarda birden çok çağrı "yeni" sonucunu alabiliyordu. Aynı özet için
    // var-mı + taşı adımını kilitle sıraya sokuyoruz (özete göre şeritlenmiş kilitler, bellek büyümez).
    private static readonly SemaphoreSlim[] MoveLocks =
        Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private readonly string _root;

    public LocalDiskFileStore(FileStoreOptions options)
    {
        _root = Path.GetFullPath(options.RootPath);
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, TempFolder));
    }

    public async Task<StoredBlob> WriteAsync(Stream content, CancellationToken ct = default)
    {
        var tempPath = Path.Combine(_root, TempFolder, Guid.NewGuid().ToString("N"));
        string hash;
        long size;
        try
        {
            await using (var temp = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             bufferSize: 81920, useAsync: true))
            using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920];
                int read;
                size = 0;
                while ((read = await content.ReadAsync(buffer, ct)) > 0)
                {
                    sha.AppendData(buffer, 0, read);
                    await temp.WriteAsync(buffer.AsMemory(0, read), ct);
                    size += read;
                }
                await temp.FlushAsync(ct);
                hash = Convert.ToHexStringLower(sha.GetHashAndReset());
            }

            var key = KeyFor(hash);
            var finalPath = PathFor(hash);
            var gate = MoveLocks[Convert.ToInt32(hash[..2], 16) % MoveLocks.Length];
            await gate.WaitAsync(ct);
            try
            {
                if (File.Exists(finalPath))
                {
                    File.Delete(tempPath); // içerik zaten var: tek fiziksel kopya
                    return new StoredBlob(hash, size, key, AlreadyExisted: true);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
                try
                {
                    File.Move(tempPath, finalPath); // aynı birim üzerinde tek adım
                }
                catch (IOException) when (File.Exists(finalPath))
                {
                    // Başka bir süreç aynı içeriği yazdı; içerik aynı olduğundan onunki kalır.
                    File.Delete(tempPath);
                    return new StoredBlob(hash, size, key, AlreadyExisted: true);
                }
                return new StoredBlob(hash, size, key, AlreadyExisted: false);
            }
            finally
            {
                gate.Release();
            }
        }
        catch
        {
            if (File.Exists(tempPath)) File.Delete(tempPath); // yarım iş artık bırakmaz
            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(string contentHash, CancellationToken ct = default)
    {
        var path = PathFor(contentHash);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string contentHash, CancellationToken ct = default)
        => Task.FromResult(File.Exists(PathFor(contentHash)));

    public Task DeleteAsync(string contentHash, CancellationToken ct = default)
    {
        var path = PathFor(contentHash);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<string> ListHashesAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var rel = Path.GetRelativePath(_root, file);
            if (rel.StartsWith(TempFolder, StringComparison.Ordinal)) continue;
            var name = Path.GetFileName(file);
            if (FileHash.IsValid(name)) yield return name;
        }
        await Task.CompletedTask;
    }

    private static string KeyFor(string hash) => $"{hash[..2]}/{hash[2..4]}/{hash}";

    private string PathFor(string hash)
    {
        if (!FileHash.IsValid(hash)) throw new ArgumentException("Geçersiz içerik özeti.", nameof(hash));
        return Path.Combine(_root, hash[..2], hash[2..4], hash);
    }
}

/// <summary>Depo kök dizini. Üretimde docker biriminin bağlandığı yol.</summary>
public sealed record FileStoreOptions(string RootPath)
{
    public static FileStoreOptions FromEnvironment() =>
        new(Environment.GetEnvironmentVariable("FILES_ROOT") ?? Path.Combine(AppContext.BaseDirectory, "files"));
}

/// <summary>SHA-256 özeti: 64 küçük harf onaltılık karakter.</summary>
internal static class FileHash
{
    internal static bool IsValid(string? hash) =>
        hash is { Length: 64 } && hash.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
