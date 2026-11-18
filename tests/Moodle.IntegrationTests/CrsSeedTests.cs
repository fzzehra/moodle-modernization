using Microsoft.EntityFrameworkCore;
using Moodle.Application.Seeding;
using Moodle.Infrastructure.Courses;

namespace Moodle.IntegrationTests;

public sealed class CrsSeedTests(CrsPostgresFixture fixture) : IClassFixture<CrsPostgresFixture>
{
    [Fact]
    public async Task Ornek_veri_CRS_tablolarina_yuklenir_ve_tekrar_calistirilabilir()
    {
        var data = SampleDataset.Create();

        await using (var db = fixture.CreateContext())
            await new CrsSeedContributor(db).SeedAsync(data);
        await using (var db = fixture.CreateContext())
            await new CrsSeedContributor(db).SeedAsync(data); // ikinci çalıştırma: kopya üretmemeli

        await using var check = fixture.CreateContext();
        Assert.Equal(3, await check.Categories.CountAsync());
        Assert.Equal(3, await check.Courses.CountAsync());
        Assert.Equal(24, await check.Enrolments.CountAsync());

        var seng = await check.Categories.SingleAsync(c => c.Name == "Yazılım Mühendisliği");
        var muh = await check.Categories.SingleAsync(c => c.Name == "Mühendislik Fakültesi");
        Assert.Equal(muh.Id, seng.ParentId);

        var seng101 = await check.Courses.SingleAsync(c => c.ShortName == "SENG101");
        Assert.Equal(seng.Id, seng101.CategoryId);
        Assert.Equal(12, await check.Enrolments.CountAsync(e => e.CourseId == seng101.Id));
        Assert.Equal("EditingTeacher",
            (await check.Enrolments.SingleAsync(e => e.CourseId == seng101.Id && e.UserId == data.UserId("ogretmen1"))).Role);
    }
}
