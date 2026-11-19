using Microsoft.EntityFrameworkCore;
using Moodle.Domain.Files;
using Moodle.Infrastructure.Files;
using Testcontainers.PostgreSql;

namespace Moodle.IntegrationTests;

public sealed class FilesPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public FilesDbContext CreateContext() => new(
        new DbContextOptionsBuilder<FilesDbContext>().UseNpgsql(_container.GetConnectionString()).Options);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

public sealed class FilesPostgresTests(FilesPostgresFixture fixture) : IClassFixture<FilesPostgresFixture>
{
    private static readonly string Hash = new('a', 64);

    private static FileEntry Entry(string name, string hash) => new()
    {
        ContentHash = hash, ContextId = 1, Component = "user", Area = "private", ItemId = 0, Path = "/",
        Name = name, SizeBytes = 3, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static FileBlob Blob(string hash) => new()
    {
        ContentHash = hash, SizeBytes = 3, StorageKey = $"{hash[..2]}/{hash[2..4]}/{hash}", CreatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Gecis_uygulanir_ve_varsayilan_ayar_satiri_vardir()
    {
        await using var db = fixture.CreateContext();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var setting = await db.Settings.SingleAsync();
        Assert.Equal(1, setting.Id);
        Assert.Equal(96, setting.DraftTtlHours);
    }

    [Fact]
    public async Task Ikinci_ayar_satiri_eklenemez()
    {
        await using var db = fixture.CreateContext();
        db.Settings.Add(new FileSetting { Id = 2 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task FILE_BR_02_ayni_alanda_ayni_adli_ikinci_kayit_reddedilir()
    {
        var hash = new string('b', 64);
        await using var db = fixture.CreateContext();
        db.Blobs.Add(Blob(hash));
        db.Entries.Add(Entry("rapor.pdf", hash));
        await db.SaveChangesAsync();

        await using var db2 = fixture.CreateContext();
        db2.Entries.Add(Entry("rapor.pdf", hash));
        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task FILE_BR_01_iki_kayit_ayni_icerigi_paylasir_ve_kullanimdaki_icerik_silinemez()
    {
        var hash = new string('c', 64);
        await using var db = fixture.CreateContext();
        db.Blobs.Add(Blob(hash));
        db.Entries.AddRange(Entry("a.txt", hash), Entry("b.txt", hash));
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.Entries.CountAsync(e => e.ContentHash == hash));
        Assert.Equal(1, await db.Blobs.CountAsync(b => b.ContentHash == hash));

        await using var db2 = fixture.CreateContext();
        db2.Blobs.Remove(await db2.Blobs.SingleAsync(b => b.ContentHash == hash));
        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task Icerigi_olmayan_kayit_eklenemez()
    {
        await using var db = fixture.CreateContext();
        db.Entries.Add(Entry("yetim.txt", new string('d', 64)));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}

public sealed class FilesConsistencyPostgresTests(FilesPostgresFixture fixture) : IClassFixture<FilesPostgresFixture>
{
    [Fact]
    public async Task FILE_NFR_02_kayit_ve_disk_tutarsizligi_gercek_veritabaniyla_yakalanir()
    {
        var root = Path.Combine(Path.GetTempPath(), "moodle-consistency-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LocalDiskFileStore(new FileStoreOptions(root));
            await using var db = fixture.CreateContext();

            // tutarlı: önce depoya yaz, sonra kaydet
            var good = await store.WriteAsync(new MemoryStream("tutarli"u8.ToArray()));
            db.Blobs.Add(new FileBlob { ContentHash = good.ContentHash, SizeBytes = good.SizeBytes, StorageKey = good.StorageKey, CreatedAt = DateTimeOffset.UtcNow });
            // yarım iş: depoya yazıldı ama kayıt eklenemedi
            var orphan = await store.WriteAsync(new MemoryStream("sahipsiz"u8.ToArray()));
            // bozuk: kayıt var, dosya yok
            var missingHash = new string('e', 64);
            db.Blobs.Add(new FileBlob { ContentHash = missingHash, SizeBytes = 1, StorageKey = "ee/ee/" + missingHash, CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();

            var report = await new FileConsistencyChecker(db, store).CheckAsync();

            Assert.Equal([missingHash], report.BlobsMissingOnDisk);
            Assert.Equal([orphan.ContentHash], report.FilesWithoutBlob);
            Assert.DoesNotContain(good.ContentHash, report.BlobsMissingOnDisk.Concat(report.FilesWithoutBlob));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
