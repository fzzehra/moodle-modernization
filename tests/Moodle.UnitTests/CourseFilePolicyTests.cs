using Moodle.Application.Abstractions;
using Moodle.Application.Files;
using Moodle.Infrastructure.Files;

namespace Moodle.UnitTests;

/// <summary>FILE-FR-13: ders dosyaları politikası; rol × okuma/yazma tablosu (WBS-1 §4.6.7).</summary>
public sealed class CourseFilePolicyTests
{
    private const int CourseId = 101;
    private const int HiddenCourseId = 102;
    private const int MissingCourseId = 999;
    private const int UserId = 7;

    private sealed class Courses : ICourseDirectory
    {
        public Task<CourseInfo?> FindAsync(int courseId, CancellationToken ct = default) => Task.FromResult(courseId switch
        {
            CourseId => new CourseInfo(courseId, 1, "Programlamaya Giriş", "SENG101", true, null, null),
            HiddenCourseId => new CourseInfo(courseId, 1, "Gizli Ders", "SENG102", false, null, null),
            _ => (CourseInfo?)null,
        });
    }

    private sealed class Enrolments(string? role) : IEnrolmentQuery
    {
        public Task<bool> IsActivelyEnrolledAsync(int userId, int courseId, CancellationToken ct = default) =>
            Task.FromResult(role is not null);
        public Task<IReadOnlyList<int>> ListEnrolledUserIdsAsync(int courseId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<int>>(role is null ? [] : [UserId]);
        public Task<string?> FindActiveRoleAsync(int userId, int courseId, CancellationToken ct = default) =>
            Task.FromResult(userId == UserId && courseId is CourseId or HiddenCourseId ? role : null);
    }

    private sealed class Access(bool manager) : IAccessPolicy
    {
        public List<(string Capability, ContextRef Context)> Asked { get; } = [];

        public Task<bool> HasCapabilityAsync(int userId, string capability, ContextRef context, CancellationToken ct = default)
        {
            Asked.Add((capability, context));
            return Task.FromResult(manager);
        }
    }

    private static CourseFilePolicy Policy(string? role, bool manager = false, Access? access = null) =>
        new(new Courses(), new Enrolments(role), access ?? new Access(manager));

    [Theory]
    // rol,             yönetici, okur,  yazar
    [InlineData("EditingTeacher", false, true, true)]
    [InlineData("Teacher", false, true, false)]
    [InlineData("Student", false, true, false)]
    [InlineData(null, false, false, false)]          // kayıtsız (veya askıda/süresi geçmiş kayıt)
    [InlineData(null, true, true, true)]             // yönetici, derse kayıtlı değil
    [InlineData("Student", true, true, true)]        // yönetici yetkisi rolün önüne geçer
    public async Task FILE_FR_13_rol_tablosu(string? role, bool manager, bool canRead, bool canWrite)
    {
        var policy = Policy(role, manager);
        var area = CourseFilePolicy.AreaFor(CourseId);

        Assert.Equal(canRead, await policy.CanReadAsync(UserId, area));
        Assert.Equal(canWrite, await policy.CanWriteAsync(UserId, area));
    }

    [Theory]
    // rol,             yönetici, okur,  yazar  — gizli ders
    [InlineData("Student", false, false, false)]     // gizli dersi öğrenci göremez
    [InlineData("Teacher", false, true, false)]
    [InlineData("EditingTeacher", false, true, true)]
    [InlineData(null, true, true, true)]             // yönetici
    [InlineData(null, false, false, false)]
    public async Task FILE_FR_13_gizli_derste_ogrenci_okuyamaz_ogretmenler_ve_yonetici_okur(
        string? role, bool manager, bool canRead, bool canWrite)
    {
        var policy = Policy(role, manager);
        var area = CourseFilePolicy.AreaFor(HiddenCourseId);

        Assert.Equal(canRead, await policy.CanReadAsync(UserId, area));
        Assert.Equal(canWrite, await policy.CanWriteAsync(UserId, area));
    }

    [Theory]
    [InlineData("EditingTeacher", false)]
    [InlineData(null, true)]
    public async Task FILE_FR_13_olmayan_derste_kimse_okuyamaz_ve_yazamaz(string? role, bool manager)
    {
        var policy = Policy(role, manager);
        var area = CourseFilePolicy.AreaFor(MissingCourseId);

        Assert.False(await policy.CanReadAsync(UserId, area));
        Assert.False(await policy.CanWriteAsync(UserId, area));
    }

    [Fact]
    public async Task FILE_FR_13_kayitsiz_kullanici_okuyamaz_ve_yazamaz()
    {
        var policy = Policy(role: null);
        Assert.False(await policy.CanReadAsync(UserId, CourseFilePolicy.AreaFor(CourseId)));
        Assert.False(await policy.CanWriteAsync(UserId, CourseFilePolicy.AreaFor(CourseId)));
    }

    [Theory]
    [InlineData("other", 0)]
    [InlineData("files", 5)]
    public async Task FILE_FR_13_ders_dosyalari_disindaki_alanlar_reddedilir(string areaName, long itemId)
    {
        var policy = Policy("EditingTeacher", manager: true);
        var area = new FileArea(CourseId, "course", areaName, itemId);

        Assert.False(await policy.CanReadAsync(UserId, area));
        Assert.False(await policy.CanWriteAsync(UserId, area));
    }

    [Fact]
    public async Task FILE_FR_13_yonetici_course_managefiles_yetenegiyle_ders_baglaminda_sorulur()
    {
        var access = new Access(manager: false);
        var policy = Policy("Student", access: access);

        await policy.CanWriteAsync(UserId, CourseFilePolicy.AreaFor(CourseId));

        var asked = Assert.Single(access.Asked);
        Assert.Equal("course.managefiles", asked.Capability);
        Assert.Equal(ContextRef.ForCourse(CourseId), asked.Context);
    }

    [Fact]
    public void FILE_FR_13_bilesen_adi_course_ve_alan_kurali()
    {
        Assert.Equal("course", Policy(null).Component);
        Assert.Equal(new FileArea(CourseId, "course", "files", 0), CourseFilePolicy.AreaFor(CourseId));
    }

    [Theory]
    [InlineData(1L, 0L, 1, 0)]
    [InlineData(long.MaxValue, long.MaxValue, 8760, 8760)]
    public void FILE_ayarlari_sinir_degerlerde_gecerlidir(long max, long quota, int ttl, int trash)
    {
        new FileSettings(max, quota, ttl, trash).Validate();
    }

    [Theory]
    [InlineData(0L, 0L, 96, 24)]
    [InlineData(1L, -1L, 96, 24)]
    [InlineData(1L, 0L, 0, 24)]
    [InlineData(1L, 0L, 8761, 24)]
    [InlineData(1L, 0L, 96, -1)]
    [InlineData(1L, 0L, 96, 8761)]
    public void FILE_ayarlari_sinir_disinda_422_verir(long max, long quota, int ttl, int trash)
    {
        var ex = Assert.Throws<Moodle.Application.Errors.AppException>(() => new FileSettings(max, quota, ttl, trash).Validate());
        Assert.Equal(422, ex.StatusCode);
        Assert.Equal("validation_failed", ex.Code);
    }
}
