using System.Text;
using Moodle.Infrastructure.Files;

namespace Moodle.UnitTests;

public sealed class LocalDiskFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "moodle-files-test-" + Guid.NewGuid().ToString("N"));
    private LocalDiskFileStore Store() => new(new FileStoreOptions(_root));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static MemoryStream Bytes(string s) => new(Encoding.UTF8.GetBytes(s));

    [Fact]
    public async Task SHA256_ozeti_ve_ab_cd_yolu_uretilir()
    {
        var blob = await Store().WriteAsync(Bytes("abc"));
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", blob.ContentHash);
        Assert.Equal("ba/78/ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", blob.StorageKey);
        Assert.Equal(3, blob.SizeBytes);
        Assert.True(File.Exists(Path.Combine(_root, "ba", "78", blob.ContentHash)));
    }

    [Fact]
    public async Task FILE_BR_01_ayni_icerik_tek_fiziksel_kopya_olarak_saklanir()
    {
        var store = Store();
        var first = await store.WriteAsync(Bytes("ayni icerik"));
        var second = await store.WriteAsync(Bytes("ayni icerik"));
        Assert.False(first.AlreadyExisted);
        Assert.True(second.AlreadyExisted);
        Assert.Single(Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + ".tmp" + Path.DirectorySeparatorChar)));
    }

    [Fact]
    public async Task Yazilan_icerik_aynen_geri_okunur()
    {
        var store = Store();
        var blob = await store.WriteAsync(Bytes("merhaba dünya"));
        await using var stream = (await store.OpenReadAsync(blob.ContentHash))!;
        using var reader = new StreamReader(stream);
        Assert.Equal("merhaba dünya", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Olmayan_icerik_icin_null_doner()
        => Assert.Null(await Store().OpenReadAsync(new string('0', 64)));

    [Fact]
    public async Task FILE_NFR_02_yazma_yarida_kesilirse_depoda_yarim_dosya_kalmaz()
    {
        var store = Store();
        await Assert.ThrowsAsync<IOException>(() => store.WriteAsync(new FailingStream()));
        Assert.Empty(Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories));
        var hashes = new List<string>();
        await foreach (var h in store.ListHashesAsync()) hashes.Add(h);
        Assert.Empty(hashes);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("ba7816bf")]
    [InlineData("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD")]
    public async Task Gecersiz_ozet_reddedilir_yol_gecisi_olmaz(string hash)
    {
        var store = Store();
        await Assert.ThrowsAsync<ArgumentException>(() => store.OpenReadAsync(hash));
        await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteAsync(hash));
    }

    [Fact]
    public async Task Eszamanli_ayni_icerik_yazimi_tek_kopya_birakir()
    {
        var store = Store();
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => store.WriteAsync(Bytes("yaris durumu"))));
        Assert.Single(results.Select(r => r.ContentHash).Distinct());
        Assert.Equal(1, results.Count(r => !r.AlreadyExisted));
        var hashes = new List<string>();
        await foreach (var h in store.ListHashesAsync()) hashes.Add(h);
        Assert.Single(hashes);
    }

    [Fact]
    public async Task Silme_icerigi_kaldirir_ve_listeden_dusurur()
    {
        var store = Store();
        var blob = await store.WriteAsync(Bytes("silinecek"));
        Assert.True(await store.ExistsAsync(blob.ContentHash));
        await store.DeleteAsync(blob.ContentHash);
        Assert.False(await store.ExistsAsync(blob.ContentHash));
        await store.DeleteAsync(blob.ContentHash); // ikinci silme hata vermez
    }

    private sealed class FailingStream : Stream
    {
        private int _calls;
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (_calls++ == 0) { buffer.Span[0] = 1; return ValueTask.FromResult(1); }
            throw new IOException("bağlantı koptu");
        }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
