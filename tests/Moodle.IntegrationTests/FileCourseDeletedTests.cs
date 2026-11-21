using Microsoft.EntityFrameworkCore;
using Moodle.Application.Courses;
using Moodle.Domain.Files;
using Moodle.Infrastructure.Files;

namespace Moodle.IntegrationTests;

/// <summary>CRS <c>course.deleted</c> olayında ders dosyalarının çöpe taşınması (FILE ↔ CRS entegrasyonu).</summary>
public sealed class FileCourseDeletedTests(FilesPostgresFixture fixture) : IClassFixture<FilesPostgresFixture>
{
    private static CrsChangeEvent Event(string kind, int courseId) =>
        new(Id: 1, Kind: kind, CourseId: courseId, EnrolmentId: null, UserId: null,
            OccurredAt: DateTimeOffset.UtcNow, Version: 1, ChangedFields: []);

    private static FileEntry Entry(string name, string hash, int contextId, string component) => new()
    {
        ContentHash = hash, ContextId = contextId, Component = component, Area = "files", ItemId = 0, Path = "/",
        Name = name, SizeBytes = 3, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static FileBlob Blob(string hash) => new()
    {
        ContentHash = hash, SizeBytes = 3, StorageKey = $"{hash[..2]}/{hash[2..4]}/{hash}", CreatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Ders_silinince_ders_dosyalari_coplenir_baska_dersin_dosyalari_ve_teslimler_etkilenmez()
    {
        const int course = 9101, other = 9102;
        string hOnly = new('1', 64), hShared = new('2', 64), hOther = new('3', 64);
        await using (var seed = fixture.CreateContext())
        {
            seed.Blobs.AddRange(Blob(hOnly), Blob(hShared), Blob(hOther));
            seed.Entries.AddRange(
                Entry("sadece-bu-ders.pdf", hOnly, course, "course"),
                Entry("ortak.pdf", hShared, course, "course"),
                Entry("ortak.pdf", hShared, other, "course"),         // aynı içerik başka derste de kullanılıyor
                Entry("diger.pdf", hOther, other, "course"),
                Entry("teslim.pdf", hOnly, course, "mod_assign"));   // ASG'nin dosyası: bu uzantının işi değil
            await seed.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext())
            await new FileCourseDeletedExtension(db, TimeProvider.System).HandleAsync(Event("course.deleted", course));

        await using var check = fixture.CreateContext();
        var entries = await check.Entries.Where(e => e.ContextId == course || e.ContextId == other).ToListAsync();

        Assert.All(entries.Where(e => e.ContextId == course && e.Component == "course"), e => Assert.NotNull(e.DeletedAt));
        Assert.Null(entries.Single(e => e.ContextId == course && e.Component == "mod_assign").DeletedAt);
        Assert.All(entries.Where(e => e.ContextId == other), e => Assert.Null(e.DeletedAt));

        // hOnly hâlâ teslim dosyasında kullanılıyor, hShared başka derste: ikisi de boşa düşmez.
        Assert.Null((await check.Blobs.FindAsync(hOnly))!.OrphanedAt);
        Assert.Null((await check.Blobs.FindAsync(hShared))!.OrphanedAt);
        Assert.Null((await check.Blobs.FindAsync(hOther))!.OrphanedAt);
    }

    [Fact]
    public async Task Kimse_kullanmiyorsa_icerik_bosa_dusurulur_ve_olay_tekrarlanirsa_sonuc_degismez()
    {
        const int course = 9103;
        var hash = new string('4', 64);
        await using (var seed = fixture.CreateContext())
        {
            seed.Blobs.Add(Blob(hash));
            seed.Entries.Add(Entry("tek.pdf", hash, course, "course"));
            await seed.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext())
            await new FileCourseDeletedExtension(db, TimeProvider.System).HandleAsync(Event("course.deleted", course));
        DateTimeOffset? firstOrphaned, firstDeleted;
        await using (var c = fixture.CreateContext())
        {
            firstOrphaned = (await c.Blobs.FindAsync(hash))!.OrphanedAt;
            firstDeleted = (await c.Entries.SingleAsync(e => e.ContextId == course)).DeletedAt;
        }
        Assert.NotNull(firstOrphaned);

        // CRS aynı olayı yeniden iletir (en az bir kez teslim): zaman damgaları ve sonuç değişmemeli.
        await using (var db = fixture.CreateContext())
            await new FileCourseDeletedExtension(db, TimeProvider.System).HandleAsync(Event("course.deleted", course));
        await using var check = fixture.CreateContext();
        Assert.Equal(firstOrphaned, (await check.Blobs.FindAsync(hash))!.OrphanedAt);
        Assert.Equal(firstDeleted, (await check.Entries.SingleAsync(e => e.ContextId == course)).DeletedAt);
    }

    [Fact]
    public async Task Baska_turdeki_olaylar_dosyalara_dokunmaz()
    {
        const int course = 9104;
        var hash = new string('5', 64);
        await using (var seed = fixture.CreateContext())
        {
            seed.Blobs.Add(Blob(hash));
            seed.Entries.Add(Entry("kalir.pdf", hash, course, "course"));
            await seed.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext())
            await new FileCourseDeletedExtension(db, TimeProvider.System).HandleAsync(Event("course.updated", course));

        await using var check = fixture.CreateContext();
        Assert.Null((await check.Entries.SingleAsync(e => e.ContextId == course)).DeletedAt);
    }

    [Fact]
    public void Uzanti_kimligi_sabit_ve_sirasi_tanimlidir()
    {
        var ext = new FileCourseDeletedExtension(null!, TimeProvider.System);
        Assert.Equal("file.course-deleted", ext.Id);
        Assert.Equal(100, ext.Order);
    }
}
