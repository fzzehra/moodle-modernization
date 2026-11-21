using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moodle.Application.Abstractions;
using Moodle.Application.Files;
using Moodle.Infrastructure.Fakes;
using Moodle.Infrastructure.Files;

namespace Moodle.IntegrationTests;

/// <summary>
/// Yönetici dosya ayarları ve ders dosyaları uçları için FILE fixture'ı: gerçek FILE veritabanı (Testcontainers)
/// ve gerçek <see cref="FileService"/>/<see cref="CourseFilePolicy"/>. Kimlik, rol ve ders bilgisi yalnızca test
/// sunucusunda kaydedilen basit karşılıklarla verilir (CrsApiFixture deseni); <c>Program.cs</c>'te kullanılmazlar.
/// </summary>
public sealed class FileRolesApiFixture : IAsyncLifetime
{
    public const int CourseId = 101;
    public const int HiddenCourseId = 102;
    public const int MissingCourseId = 999;

    public const int SiteAdmin = 1;
    public const int Manager = 2;
    public const int EditingTeacher = 10;
    public const int Teacher = 11;
    public const int Student = 12;
    public const int NotEnrolled = 13;

    public FilesPostgresFixture Database { get; } = new();
    private readonly WebApplicationFactory<Program> _root = new();

    public Task InitializeAsync() => Database.InitializeAsync();

    /// <param name="userId">Oturumdaki kullanıcı; <c>null</c> ise oturum yok.</param>
    /// <param name="fakeAuth"><c>true</c> ise depodaki AUTH sahtesi kullanılır (503 beklenir).</param>
    public WebApplicationFactory<Program> CreateFactory(int? userId, bool fakeAuth = false)
    {
        using var db = Database.CreateContext();
        var connectionString = db.Database.GetConnectionString();
        return _root.WithWebHostBuilder(builder => builder.UseEnvironment("Production").ConfigureTestServices(services =>
        {
            services.RemoveAll<FilesDbContext>();
            services.RemoveAll<DbContextOptions<FilesDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<FilesDbContext>>();
            services.AddDbContext<FilesDbContext>(options => options.UseNpgsql(connectionString));

            services.RemoveAll<IVirusScanner>();
            services.AddSingleton<IVirusScanner, CleanTestScanner>();
            services.RemoveAll<IFileStore>();
            services.AddSingleton<IFileStore>(new FakeFileStore());

            services.RemoveAll<ICurrentUser>();
            services.RemoveAll<IAccessPolicy>();
            services.RemoveAll<ICourseDirectory>();
            services.RemoveAll<IEnrolmentQuery>();
            services.RemoveAll<ISiteAdminQuery>();
            services.AddSingleton<ICurrentUser>(fakeAuth ? new FakeCurrentUser() : new TestUser(userId));
            services.AddSingleton<IAccessPolicy>(new TestAccessPolicy());
            services.AddSingleton<ICourseDirectory>(new TestCourses());
            services.AddSingleton<IEnrolmentQuery>(new TestEnrolments());
            services.AddSingleton<ISiteAdminQuery>(new TestSiteAdmins());
        }));
    }

    public async Task DisposeAsync()
    {
        await _root.DisposeAsync();
        await Database.DisposeAsync();
    }

    public static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    private sealed class TestUser(int? userId) : ICurrentUser
    {
        public bool IsAuthenticated => userId is not null;
        public int? UserId => userId;
        public string? Username => userId is null ? null : $"user-{userId}";
    }

    /// <summary>Site yöneticisi ve Manager her yeteneğe sahiptir (CapabilityRules'taki yönetici kuralı).</summary>
    private sealed class TestAccessPolicy : IAccessPolicy
    {
        public Task<bool> HasCapabilityAsync(int userId, string capability, ContextRef context, CancellationToken ct = default) =>
            Task.FromResult(userId is SiteAdmin or Manager);
    }

    private sealed class TestSiteAdmins : ISiteAdminQuery
    {
        public Task<bool> IsSiteAdminAsync(int userId, CancellationToken ct = default) => Task.FromResult(userId == SiteAdmin);
    }

    private sealed class TestCourses : ICourseDirectory
    {
        public Task<CourseInfo?> FindAsync(int courseId, CancellationToken ct = default) => Task.FromResult(courseId switch
        {
            CourseId => new CourseInfo(courseId, 1, "Programlamaya Giriş", "SENG101", true, null, null),
            HiddenCourseId => new CourseInfo(courseId, 1, "Gizli Ders", "SENG102", false, null, null),
            _ => (CourseInfo?)null,
        });
    }

    /// <summary>Aynı kullanıcılar hem görünür (101) hem gizli (102) derse aynı rolle kayıtlıdır.</summary>
    private sealed class TestEnrolments : IEnrolmentQuery
    {
        private static string? Role(int userId, int courseId) => courseId is not (CourseId or HiddenCourseId) ? null : userId switch
        {
            EditingTeacher => "EditingTeacher",
            Teacher => "Teacher",
            Student => "Student",
            _ => null,
        };

        public Task<bool> IsActivelyEnrolledAsync(int userId, int courseId, CancellationToken ct = default) =>
            Task.FromResult(Role(userId, courseId) is not null);
        public Task<IReadOnlyList<int>> ListEnrolledUserIdsAsync(int courseId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<int>>(courseId is CourseId or HiddenCourseId ? [EditingTeacher, Teacher, Student] : []);
        public Task<string?> FindActiveRoleAsync(int userId, int courseId, CancellationToken ct = default) =>
            Task.FromResult(Role(userId, courseId));
    }
}
