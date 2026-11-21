using Microsoft.EntityFrameworkCore;
using Moodle.Application.Files;
using Moodle.Application.Seeding;
using Moodle.Infrastructure.Fakes;
using Moodle.Infrastructure.Files;

namespace Moodle.IntegrationTests;

public sealed class FilesSeedTests(FilesPostgresFixture fixture) : IClassFixture<FilesPostgresFixture>
{
    private static FileService BuildService(FilesDbContext db)
    {
        var store=new FakeFileStore();
        return new(db,store,new FilePolicyRegistry([new UserFilePolicy()]),TimeProvider.System,
            new DraftUploadService(db,store,new CleanTestScanner(),new UploadLimits(),TimeProvider.System));
    }

    [Fact]
    public async Task Ornek_dosya_yuklenir_ve_tekrar_calistirma_yinelenen_kayit_birikmez()
    {
        var data = SampleDataset.Create();
        var ownerId = data.UserId("ogretmen1");

        await using (var db = fixture.CreateContext())
            await new FilesSeedContributor(BuildService(db)).SeedAsync(data);
        await using (var db = fixture.CreateContext())
            await new FilesSeedContributor(BuildService(db)).SeedAsync(data); // ikinci çalıştırma: kopya üretmemeli

        await using var check = fixture.CreateContext();
        var active = await check.Entries.Where(e => e.OwnerUserId == ownerId && e.DeletedAt == null).ToListAsync();
        var entry = Assert.Single(active, e => e.Name == "performans-ornek.txt");
        Assert.Equal("user", entry.Component);
        Assert.Equal("private", entry.Area);
        Assert.True(entry.SizeBytes > 190 * 1024, $"Beklenenden küçük: {entry.SizeBytes} bayt");
    }
}
