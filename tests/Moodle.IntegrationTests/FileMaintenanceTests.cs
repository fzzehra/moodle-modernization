using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moodle.Application.Files;
using Moodle.Domain.Files;
using Moodle.Infrastructure.Fakes;
using Moodle.Infrastructure.Files;
using Npgsql;

namespace Moodle.IntegrationTests;

/// <summary>
/// Temizlik görevi (FILE-FR-12; FILE-BR-01, 04, 05; FILE-BUG-02, 03, 04) gerçek PostgreSQL ve gerçek yerel disk
/// deposuyla, sahte zamanla sınanır. Varsayılan ayar: taslak süresi 96 saat, çöp bekleme süresi 24 saat.
/// </summary>
public sealed class FileMaintenanceTests(FilesPostgresFixture fixture) : IClassFixture<FilesPostgresFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private const int Owner = 1;
    private const int OtherOwner = 2;

    private readonly MutableClock _clock = new(T0);
    private readonly string _root = Path.Combine(Path.GetTempPath(), "moodle-file-maintenance-" + Guid.NewGuid().ToString("N"));
    private LocalDiskFileStore _store = null!;

    public async Task InitializeAsync()
    {
        _store = new LocalDiskFileStore(new FileStoreOptions(_root));
        await using var db = fixture.CreateContext();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE file_entry, file_draft, file_blob RESTART IDENTITY");
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        return Task.CompletedTask;
    }

    // ---------- FILE-FR-12, FILE-BR-05, FILE-BUG-02 ----------

    [Fact]
    public async Task FILE_FR_12_T_FILE_08_suresi_dolan_taslak_ve_kayitlari_silinir_dolmayan_kalir()
    {
        // A: t0'da oluşturulup dokunulmadı. B: t0'da oluşturuldu ama t0+95s'te dosya eklendi (FILE-BUG-02).
        var draftA = await NewDraftWithFileAsync(Owner, "eski.txt", "A icerigi");
        var draftB = await NewDraftAsync(Owner);
        _clock.Now = T0.AddHours(95);
        await AddToDraftAsync(Owner, draftB, "duzenleniyor.txt", "B icerigi");

        _clock.Now = T0.AddHours(97);
        var result = await RunAsync();

        Assert.Equal(1, result.ExpiredDrafts);
        Assert.False(result.Skipped);
        await using var db = fixture.CreateContext();
        Assert.False(await db.Drafts.AnyAsync(d => d.DraftId == draftA));
        Assert.False(await db.Entries.AnyAsync(e => e.Area == "draft" && e.ItemId == draftA));
        Assert.True(await db.Drafts.AnyAsync(d => d.DraftId == draftB));
        Assert.True(await db.Entries.AnyAsync(e => e.Area == "draft" && e.ItemId == draftB && e.DeletedAt == null));

        // A'nın içeriği artık kimse tarafından kullanılmıyor: hemen silinmez, bu andan bekleme süresi başlar.
        var hashA = Hash("A icerigi");
        var blobA = await db.Blobs.SingleAsync(b => b.ContentHash == hashA);
        Assert.Equal(_clock.Now, blobA.OrphanedAt);
        Assert.True(await _store.ExistsAsync(hashA));
    }

    [Fact]
    public async Task FILE_BUG_02_DeleteFromDraftAsync_ve_CommitAsync_son_etkinligi_gunceller_listeleme_guncellemez()
    {
        var draftId = await NewDraftAsync(Owner);
        await AddToDraftAsync(Owner, draftId, "bir.txt", "bir");
        var fileTwo = await AddToDraftAsync(Owner, draftId, "iki.txt", "iki");

        _clock.Now = T0.AddHours(10);
        await using (var db = fixture.CreateContext())
            await Files(db).DeleteFromDraftAsync(Owner, draftId, fileTwo.FileId);
        Assert.Equal(T0.AddHours(10), await LastActivityAsync(draftId));

        _clock.Now = T0.AddHours(20);
        await using (var db = fixture.CreateContext())
            await Files(db).ListDraftAsync(Owner, draftId);
        Assert.Equal(T0.AddHours(10), await LastActivityAsync(draftId)); // listeleme etkinlik sayılmaz

        _clock.Now = T0.AddHours(30);
        await using (var db = fixture.CreateContext())
            await Files(db).CommitAsync(Owner, draftId, PrivateArea(Owner));
        Assert.Equal(T0.AddHours(30), await LastActivityAsync(draftId)); // taslak satırı silinmez, etkinlik güncellenir
    }

    // ---------- FILE-BR-04, FILE-BUG-03 ----------

    [Fact]
    public async Task FILE_BR_04_BUG_03_copte_suresi_dolan_kayit_kalici_silinir_yeni_cope_atilan_kalir()
    {
        var eski = await PrivateFileAsync(Owner, "eski-cop.txt", "eski cop");
        var yeni = await PrivateFileAsync(Owner, "yeni-cop.txt", "yeni cop"); // ikisi de t0'da oluşturuldu

        _clock.Now = T0.AddHours(1);
        await DeleteAsync(Owner, eski);
        _clock.Now = T0.AddHours(20);
        await DeleteAsync(Owner, yeni); // dosya eski ama çöpe yeni atıldı (FILE-BUG-03)

        _clock.Now = T0.AddHours(25);
        var result = await RunAsync();

        Assert.Equal(1, result.PurgedEntries);
        await using var db = fixture.CreateContext();
        Assert.False(await db.Entries.AnyAsync(e => e.Id == eski));
        Assert.True(await db.Entries.AnyAsync(e => e.Id == yeni && e.DeletedAt != null));

        // Eski kaydın içeriği t0+1s'ten beri boşta: aynı turda kayıt silindikten sonra içerik de silinir.
        Assert.Equal(1, result.PurgedBlobs);
        Assert.False(await _store.ExistsAsync(Hash("eski cop")));
        Assert.True(await _store.ExistsAsync(Hash("yeni cop")));
    }

    // ---------- FILE-BR-01, FILE-BUG-04 ----------

    [Fact]
    public async Task FILE_BR_01_BUG_04_paylasilan_icerik_son_kayit_silinip_sure_dolunca_diskten_silinir()
    {
        var first = await PrivateFileAsync(Owner, "ortak.txt", "ortak icerik");
        var second = await PrivateFileAsync(OtherOwner, "ortak.txt", "ortak icerik");
        var hash = Hash("ortak icerik");

        await DeleteAsync(Owner, first);
        _clock.Now = T0.AddHours(30);
        var r1 = await RunAsync();

        Assert.Equal(1, r1.PurgedEntries);
        Assert.Equal(0, r1.PurgedBlobs);
        await using (var db = fixture.CreateContext())
            Assert.Null((await db.Blobs.SingleAsync(b => b.ContentHash == hash)).OrphanedAt);
        Assert.True(await _store.ExistsAsync(hash)); // ikinci kayıt hâlâ kullanıyor

        await DeleteAsync(OtherOwner, second); // t0+30s: içerik boşa düştü
        _clock.Now = T0.AddHours(53);
        var r2 = await RunAsync();
        Assert.Equal(0, r2.PurgedBlobs); // kayıt çöpte, süresi dolmadı: içerik dokunulmaz
        Assert.True(await _store.ExistsAsync(hash));

        _clock.Now = T0.AddHours(55);
        var r3 = await RunAsync();
        Assert.Equal(1, r3.PurgedEntries);
        Assert.Equal(1, r3.PurgedBlobs);
        Assert.Empty(r3.Errors);
        await using (var db = fixture.CreateContext())
            Assert.False(await db.Blobs.AnyAsync(b => b.ContentHash == hash));
        Assert.False(await _store.ExistsAsync(hash));
    }

    [Theory]
    [InlineData("orphaned_at_null")]
    [InlineData("new_entry")]
    public async Task FILE_BUG_04_temizlik_calisirken_icerik_yeniden_kullanilirsa_silinmez(string reuse)
    {
        var hash = await OrphanedBlobAsync("yeniden kullanilan", orphanedAt: T0);
        _clock.Now = T0.AddHours(48);

        // Başka bir işlem içeriği kilitler; temizlik bu kilitte beklerken içerik yeniden kullanılmaya başlar.
        await using var conn = new NpgsqlConnection(ConnectionString());
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await Exec(conn, tx, """SELECT 1 FROM file_blob WHERE "ContentHash" = @h FOR UPDATE""", hash);

        var run = Task.Run(() => RunAsync());
        await WaitForBlockedCleanupAsync();

        if (reuse == "orphaned_at_null")
            await Exec(conn, tx, """UPDATE file_blob SET "OrphanedAt" = NULL WHERE "ContentHash" = @h""", hash);
        else
            await Exec(conn, tx, """
                INSERT INTO file_entry ("ContentHash", "ContextId", "Component", "Area", "ItemId", "Path", "Name",
                    "OwnerUserId", "SizeBytes", "MimeType", "Status", "SortOrder", "CreatedAt", "UpdatedAt")
                VALUES (@h, 1, 'user', 'private', 0, '/', 'yeni.txt', 1, 18, 'text/plain', 'active', 0, now(), now())
                """, hash);
        await tx.CommitAsync();

        var result = await run.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(0, result.PurgedBlobs);
        await using var db = fixture.CreateContext();
        Assert.True(await db.Blobs.AnyAsync(b => b.ContentHash == hash));
        Assert.True(await _store.ExistsAsync(hash));
    }

    [Fact]
    public async Task FILE_FR_12_iki_eszamanli_calistirmadan_yalnizca_biri_calisir()
    {
        var hash = await OrphanedBlobAsync("kilit testi", orphanedAt: T0);
        _clock.Now = T0.AddHours(48);

        // Birinci çalıştırma diskten silme adımında, danışma kilidini tutarken bekletilir.
        var gated = new GatedStore(_store);
        await using var db1 = fixture.CreateContext();
        var first = Task.Run(() => Maintenance(db1, gated).RunAsync());
        await gated.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));

        var second = await RunAsync();
        Assert.True(second.Skipped);
        Assert.Equal(0, second.PurgedBlobs);

        gated.Release.SetResult();
        var firstResult = await first.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.False(firstResult.Skipped);
        Assert.Equal(1, firstResult.PurgedBlobs);
        Assert.False(await _store.ExistsAsync(hash));

        Assert.False((await RunAsync()).Skipped); // kilit bırakıldı
    }

    [Fact]
    public async Task FILE_BUG_04_diskten_silme_basarisiz_olursa_satir_geri_gelmez_hata_raporlanir()
    {
        var hash = await OrphanedBlobAsync("disk hatasi", orphanedAt: T0);
        _clock.Now = T0.AddHours(48);

        await using var db = fixture.CreateContext();
        var result = await Maintenance(db, new FailingDeleteStore(_store)).RunAsync();

        Assert.Equal(1, result.PurgedBlobs);
        Assert.Single(result.Errors);
        await using var check = fixture.CreateContext();
        Assert.False(await check.Blobs.AnyAsync(b => b.ContentHash == hash));
        var report = await new FileConsistencyChecker(check, _store).CheckAsync();
        Assert.Contains(hash, report.FilesWithoutBlob);
    }

    // ---------- yardımcılar ----------

    private Task<FileMaintenanceResult> RunAsync() => RunWithFreshContextAsync();

    private async Task<FileMaintenanceResult> RunWithFreshContextAsync()
    {
        await using var db = fixture.CreateContext();
        return await Maintenance(db, _store).RunAsync();
    }

    private FileMaintenanceService Maintenance(FilesDbContext db, IFileStore store) =>
        new(db, store, _clock, NullLogger<FileMaintenanceService>.Instance);

    private FileService Files(FilesDbContext db) =>
        new(db, _store, new FilePolicyRegistry([new UserFilePolicy()]), _clock);

    private static FileArea PrivateArea(int owner) => new(owner, "user", "private", 0);

    private async Task<long> NewDraftAsync(int owner)
    {
        await using var db = fixture.CreateContext();
        return (await Files(db).CreateDraftAsync(owner)).DraftId;
    }

    private async Task<FileMeta> AddToDraftAsync(int owner, long draftId, string name, string text)
    {
        await using var db = fixture.CreateContext();
        return await Files(db).AddToDraftAsync(owner, draftId, name, Content(text));
    }

    private async Task<long> NewDraftWithFileAsync(int owner, string name, string text)
    {
        var draftId = await NewDraftAsync(owner);
        await AddToDraftAsync(owner, draftId, name, text);
        return draftId;
    }

    private async Task<long> PrivateFileAsync(int owner, string name, string text)
    {
        var draftId = await NewDraftWithFileAsync(owner, name, text);
        await using var db = fixture.CreateContext();
        return (await Files(db).CommitAsync(owner, draftId, PrivateArea(owner), replace: false)).Single().FileId;
    }

    private async Task DeleteAsync(int owner, long fileId)
    {
        await using var db = fixture.CreateContext();
        await Files(db).DeleteAsync(owner, fileId);
    }

    private async Task<DateTimeOffset> LastActivityAsync(long draftId)
    {
        await using var db = fixture.CreateContext();
        return (await db.Drafts.SingleAsync(d => d.DraftId == draftId)).LastActivityAt;
    }

    /// <summary>Diske yazılmış, hiçbir kaydın kullanmadığı ve verilen andan beri boşta olan içerik.</summary>
    private async Task<string> OrphanedBlobAsync(string text, DateTimeOffset orphanedAt)
    {
        var stored = await _store.WriteAsync(Content(text));
        await using var db = fixture.CreateContext();
        db.Blobs.Add(new FileBlob
        {
            ContentHash = stored.ContentHash, SizeBytes = stored.SizeBytes, StorageKey = stored.StorageKey,
            CreatedAt = orphanedAt, OrphanedAt = orphanedAt,
        });
        await db.SaveChangesAsync();
        return stored.ContentHash;
    }

    private string ConnectionString()
    {
        using var db = fixture.CreateContext();
        return db.Database.GetConnectionString()!;
    }

    /// <summary>Temizliğin <c>file_blob</c> satır kilidinde beklediğini <c>pg_stat_activity</c> ile doğrular.</summary>
    private async Task WaitForBlockedCleanupAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString());
        await conn.OpenAsync();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            await using var cmd = new NpgsqlCommand("""
                SELECT count(*) FROM pg_stat_activity
                WHERE pid <> pg_backend_pid() AND wait_event_type = 'Lock'
                  AND query LIKE '%file_blob%FOR UPDATE%'
                """, conn);
            if ((long)(await cmd.ExecuteScalarAsync())! > 0) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("Temizlik görevi içerik kilidinde beklemeye başlamadı.");
    }

    private static async Task Exec(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, string hash)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("h", hash);
        await cmd.ExecuteNonQueryAsync();
    }

    private static MemoryStream Content(string text) => new(Encoding.UTF8.GetBytes(text));

    private static string Hash(string text) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>Diskten silme adımına gelince bekleyen depo: danışma kilidi tutulurken ikinci çalıştırmayı denemek için.</summary>
    private sealed class GatedStore(IFileStore inner) : IFileStore
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task DeleteAsync(string contentHash, CancellationToken ct = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(ct);
            await inner.DeleteAsync(contentHash, ct);
        }

        public Task<StoredBlob> WriteAsync(Stream content, CancellationToken ct = default) => inner.WriteAsync(content, ct);
        public Task<Stream?> OpenReadAsync(string contentHash, CancellationToken ct = default) => inner.OpenReadAsync(contentHash, ct);
        public Task<bool> ExistsAsync(string contentHash, CancellationToken ct = default) => inner.ExistsAsync(contentHash, ct);
        public IAsyncEnumerable<string> ListHashesAsync(CancellationToken ct = default) => inner.ListHashesAsync(ct);
    }

    private sealed class FailingDeleteStore(IFileStore inner) : IFileStore
    {
        public Task DeleteAsync(string contentHash, CancellationToken ct = default) =>
            throw new IOException("disk erişilemiyor");

        public Task<StoredBlob> WriteAsync(Stream content, CancellationToken ct = default) => inner.WriteAsync(content, ct);
        public Task<Stream?> OpenReadAsync(string contentHash, CancellationToken ct = default) => inner.OpenReadAsync(contentHash, ct);
        public Task<bool> ExistsAsync(string contentHash, CancellationToken ct = default) => inner.ExistsAsync(contentHash, ct);
        public IAsyncEnumerable<string> ListHashesAsync(CancellationToken ct = default) => inner.ListHashesAsync(ct);
    }
}
