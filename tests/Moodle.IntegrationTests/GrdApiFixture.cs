using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moodle.Application.Abstractions;
using Moodle.Infrastructure.Grading;

namespace Moodle.IntegrationTests;

/// <summary>
/// GRD API testleri için gerçek PostgreSQL (Testcontainers) + uygulama (CrsApiFixture deseni).
/// AUTH, ASG ve CRS arayüzleri yalnızca test sunucusunda denetlenebilir sahtelerle değiştirilir.
/// </summary>
public sealed class GrdApiFixture : IAsyncLifetime
{
    public const int CourseId = 7;
    public const int GraderId = 42;
    public const int MissingAssignmentId = 999_999;
    public const int NotEnrolledLearnerId = 666;
    public const int MissingCourseId = 999_998;

    private static int _nextAssignmentId = 100;
    private static int _nextCourseId = 1000;
    private static readonly ConcurrentDictionary<int, int[]> CourseLearners = new();
    public GrdPostgresFixture Database { get; } = new();
    private readonly WebApplicationFactory<Program> _root = new();

    /// <summary>Her test kendi ödevini kullanır; böylece paylaşılan veritabanında testler birbirini etkilemez.</summary>
    public static int NextAssignmentId() => Interlocked.Increment(ref _nextAssignmentId);

    /// <summary>Rapor testleri kendi dersini kullanır; kalemler ve kayıtlar başka testlerle karışmaz.</summary>
    public static int NextCourseId() => Interlocked.Increment(ref _nextCourseId);

    /// <summary>Dersin etkin kayıtlı öğrencilerini belirler; belirtilmeyen derslerde 3, 4 ve 5 kayıtlıdır.</summary>
    public static void SetEnrolledLearners(int courseId, params int[] learnerIds) => CourseLearners[courseId] = learnerIds;

    public Task InitializeAsync() => Database.InitializeAsync();

    /// <param name="userId">Oturum sahibi; varsayılan değerlendirici (42).</param>
    /// <param name="now">Sunucu saati; verilmezse gerçek saat.</param>
    public WebApplicationFactory<Program> CreateFactory(
        bool authenticated = true, Func<string, ContextRef, bool>? permission = null,
        int userId = GraderId, DateTimeOffset? now = null)
    {
        using var db = Database.CreateContext();
        var connectionString = db.Database.GetConnectionString();
        return _root.WithWebHostBuilder(builder => builder.UseEnvironment("Production").ConfigureTestServices(services =>
        {
            services.RemoveAll<GrdDbContext>();
            services.RemoveAll<DbContextOptions<GrdDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<GrdDbContext>>();
            services.AddDbContext<GrdDbContext>(options => options.UseNpgsql(connectionString));
            services.RemoveAll<ICurrentUser>();
            services.RemoveAll<IAccessPolicy>();
            services.RemoveAll<IAssignmentQuery>();
            services.RemoveAll<IEnrolmentQuery>();
            services.RemoveAll<ICourseDirectory>();
            // Bu sahteler yalnızca test sunucusunda kaydedilir, Program.cs'te asla.
            services.AddSingleton<ICurrentUser>(new TestUser(authenticated, userId));
            services.AddSingleton<IAccessPolicy>(new TestPolicy(userId, permission ?? ((_, _) => true)));
            services.AddSingleton<IAssignmentQuery>(new TestAssignmentQuery());
            services.AddSingleton<IEnrolmentQuery>(new TestEnrolmentQuery());
            services.AddSingleton<ICourseDirectory>(new TestCourseDirectory());
            if (now is { } fixedNow)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(fixedNow));
            }
        }));
    }

    public async Task DisposeAsync()
    {
        await _root.DisposeAsync();
        await Database.DisposeAsync();
    }

    private sealed class TestUser(bool authenticated, int userId) : ICurrentUser
    {
        public bool IsAuthenticated => authenticated;
        public int? UserId => authenticated ? userId : null;
        public string? Username => authenticated ? "grd-api-test" : null;
    }

    private sealed class TestPolicy(int sessionUserId, Func<string, ContextRef, bool> permission) : IAccessPolicy
    {
        public Task<bool> HasCapabilityAsync(int userId, string capability, ContextRef context, CancellationToken ct = default) =>
            Task.FromResult(userId == sessionUserId && permission(capability, context));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary><see cref="MissingCourseId"/> dışındaki her ders vardır.</summary>
    private sealed class TestCourseDirectory : ICourseDirectory
    {
        public Task<CourseInfo?> FindAsync(int courseId, CancellationToken ct = default) =>
            Task.FromResult(courseId is > 0 and not MissingCourseId
                ? new CourseInfo(courseId, 1, $"Ders {courseId}", $"C{courseId}", true, null, null)
                : null);
    }

    /// <summary>Eksik kimlik dışındaki her ödev <see cref="CourseId"/> dersine aittir.</summary>
    private sealed class TestAssignmentQuery : IAssignmentQuery
    {
        public Task<AssignmentInfo?> FindAsync(int assignmentId, CancellationToken ct = default) =>
            Task.FromResult(assignmentId is > 0 and not MissingAssignmentId
                ? new AssignmentInfo(assignmentId, CourseId, $"Ödev {assignmentId}", null, null, null, false)
                : null);

        public Task<bool> HasSubmittedAsync(int assignmentId, int learnerId, CancellationToken ct = default) =>
            Task.FromResult(true);
    }

    /// <summary>
    /// <see cref="SetEnrolledLearners"/> ile belirlenmeyen derslerde <see cref="NotEnrolledLearnerId"/>
    /// dışındaki öğrenciler etkin kayıtlıdır; liste 3, 4 ve 5'tir.
    /// </summary>
    private sealed class TestEnrolmentQuery : IEnrolmentQuery
    {
        public Task<bool> IsActivelyEnrolledAsync(int userId, int courseId, CancellationToken ct = default) =>
            Task.FromResult(courseId != MissingCourseId && userId != NotEnrolledLearnerId &&
                            (!CourseLearners.TryGetValue(courseId, out var ids) || ids.Contains(userId)));

        public Task<IReadOnlyList<int>> ListEnrolledUserIdsAsync(int courseId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<int>>(
                CourseLearners.TryGetValue(courseId, out var ids) ? ids
                : courseId == MissingCourseId ? []
                : [3, 4, 5]);
    }
}

public static class GrdApiTestSupport
{
    public static string GradePath(int assignmentId, int learnerId = 3, int attempt = 0) =>
        $"/api/v1/assignments/{assignmentId}/learners/{learnerId}/attempts/{attempt}/grade";

    public static async Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path, string? body)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> PutGradeAsync(HttpClient client, string path, decimal? score, int version,
        string? feedback = null) =>
        SendAsync(client, "PUT", path, JsonSerializer.Serialize(new { score, feedback, version }));

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(expected == response.StatusCode, $"{(int)response.StatusCode}: {body}");
        using var json = JsonDocument.Parse(body);
        return json.RootElement.Clone();
    }

    /// <summary>Tek tip hata gövdesi; yığın izi, SQL ve bağlantı ayrıntısı içermez (K3).</summary>
    public static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(status == response.StatusCode, $"{(int)response.StatusCode}: {body}");
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(body);
        var error = json.RootElement.GetProperty("error");
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("message").GetString()));
        if (code is not null) Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.DoesNotContain("Password", body);
        Assert.DoesNotContain("Npgsql", body);
        Assert.DoesNotContain("grd_", body);
        Assert.DoesNotContain("   at ", body);
    }
}
