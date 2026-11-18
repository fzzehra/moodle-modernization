using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moodle.Application.Abstractions;
using Moodle.Application.Assignments;
using Moodle.Domain.Assignments;
using Moodle.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Moodle.IntegrationTests;

public sealed class AsgFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17").Build();
    public FixedClock Clock { get; } = new();
    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MoodleDbContext>().Database.MigrateAsync();
    }
    async Task IAsyncLifetime.DisposeAsync() { await DisposeAsync(); await postgres.DisposeAsync(); }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<MoodleDbContext>>();
            services.AddDbContext<MoodleDbContext>(o => o.UseNpgsql(postgres.GetConnectionString()));
            services.RemoveAll<ICurrentUser>(); services.AddScoped<ICurrentUser, HeaderUser>();
            services.RemoveAll<IAccessPolicy>(); services.AddScoped<IAccessPolicy, ScopedPolicy>();
            services.RemoveAll<IAssignmentLearnerQuery>(); services.AddScoped<IAssignmentLearnerQuery, Moodle.Infrastructure.Fakes.FakeAssignmentLearnerQuery>();
            services.RemoveAll<ISubmissionAccessPolicy>(); services.AddScoped<ISubmissionAccessPolicy, Moodle.Infrastructure.Fakes.FakeSubmissionAccessPolicy>();
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(Clock);
            services.AddHttpClient<Moodle.Web.Assignments.AsgClient>(c => c.BaseAddress = new Uri("http://localhost"))
                .ConfigurePrimaryHttpMessageHandler(sp => new UiBridge(Server.CreateHandler(), sp.GetRequiredService<IHttpContextAccessor>()));
        });
    }
    private sealed class UiBridge(HttpMessageHandler inner, IHttpContextAccessor context) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var id = context.HttpContext?.Request.Headers["X-Test-User"].ToString();
            if (!string.IsNullOrEmpty(id)) request.Headers.Add("X-Test-User", id);
            return base.SendAsync(request, ct);
        }
    }
    public HttpClient Client(int? user = 3)
    {
        var client = CreateClient();
        if (user is not null) client.DefaultRequestHeaders.Add("X-Test-User", user.ToString());
        return client;
    }
    public sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    // Identity headers exist only in the test host; production never accepts them.
    private sealed class HeaderUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        public int? UserId => int.TryParse(accessor.HttpContext?.Request.Headers["X-Test-User"], out var id) ? id : null;
        public bool IsAuthenticated => UserId is not null;
        public string? Username => null;
    }
    private sealed class ScopedPolicy : IAccessPolicy
    {
        public Task<bool> HasCapabilityAsync(int userId, string capability, ContextRef context, CancellationToken ct = default)
            => Task.FromResult(userId == 2 || (userId is >= 3 and <= 5
                && capability is "mod/assign:view" or "mod/assign:submit"));
    }
}

public class AssignmentApiTests(AsgFixture fixture) : IClassFixture<AsgFixture>
{
    private CreateAssignment Request(bool confirmation = true) => new(1, "Sentetik ödev", "Açıklama", null, null, null, confirmation, ["text"]);
    private async Task<Assignment> Create(CreateAssignment? request = null)
    {
        using var teacher = fixture.Client(2);
        var response = await teacher.PostAsJsonAsync("/api/v1/courses/1/assignments", request ?? Request());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var assignment = (await response.Content.ReadFromJsonAsync<Assignment>())!;
        Assert.Equal($"/api/v1/assignments/{assignment.Id}", response.Headers.Location!.ToString());
        return assignment;
    }
    private static string Mine(int id) => $"/api/v1/assignments/{id}/my-submission";
    private static async Task Error(HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("error").GetProperty("message").GetString()));
        if (code is not null) Assert.Equal(code, json.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.DoesNotContain("StackTrace", body); Assert.DoesNotContain("Npgsql", body);
    }
    [Fact]
    public async Task T_ASG_02_ASG_FR_01_create_persists_and_student_sees_permissions()
    {
        var a = await Create();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MoodleDbContext>();
        var stored = await db.Assignments.SingleAsync(x => x.Id == a.Id);
        Assert.Equal(1, stored.CourseId); Assert.Equal(1, stored.SectionId);
        using var student = fixture.Client();
        var view = await student.GetFromJsonAsync<AssignmentView>($"/api/v1/assignments/{a.Id}");
        Assert.True(view!.Permissions.CanSubmit); Assert.False(view.Permissions.CanGrade);
    }
    [Fact]
    public async Task T_ASG_02_ASG_FR_01_invalid_fields_dates_section_and_permission()
    {
        using var teacher = fixture.Client(2);
        foreach (var invalid in new[] { Request() with { Title = " " }, Request() with { SectionId = 9 },
            Request() with { OpensAt = fixture.Clock.Now, DueAt = fixture.Clock.Now.AddMinutes(-1) },
            Request() with { EnabledSubmissionTypes = ["file"] } })
            await Error(await teacher.PostAsJsonAsync("/api/v1/courses/1/assignments", invalid), HttpStatusCode.UnprocessableEntity);
        using var student = fixture.Client();
        await Error(await student.PostAsJsonAsync("/api/v1/courses/1/assignments", Request()), HttpStatusCode.Forbidden);
        await Error(await teacher.PostAsJsonAsync("/api/v1/courses/99/assignments", Request()), HttpStatusCode.NotFound);
    }
    [Theory]
    [InlineData(true, "draft")] [InlineData(false, "submitted")]
    public async Task T_ASG_01_ASG_FR_02_ASG_BR_01_save_update_reopen_and_query(bool confirmation, string status)
    {
        var a = await Create(Request(confirmation));
        using var student = fixture.Client();
        var empty = await student.GetFromJsonAsync<MySubmission>(Mine(a.Id));
        Assert.Equal("notSubmitted", empty!.Status); Assert.Null(empty.Submission);
        var response = await student.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("İlk metin", 0));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var first = (await response.Content.ReadFromJsonAsync<Submission>())!;
        Assert.Equal(status, first.Status);
        response = await student.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("Güncel metin", first.Version));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var mine = (await student.GetFromJsonAsync<MySubmission>(Mine(a.Id)))!;
        Assert.Equal("Güncel metin", mine.Submission!.Text); Assert.Equal(2, mine.Submission.Version);
        using var scope = fixture.Services.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<IAssignmentQuery>();
        Assert.IsType<AssignmentQuery>(query);
        Assert.Equal(a.Title, (await query.FindAsync(a.Id))!.Title);
        Assert.Equal(!confirmation, await query.HasSubmittedAsync(a.Id, 3));
        Assert.Null(await query.FindAsync(int.MaxValue));
    }
    [Fact]
    public async Task T_ASG_01_ASG_FR_02_submit_and_prevent_further_edits()
    {
        var a = await Create(); using var student = fixture.Client();
        await student.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("Taslak", 0));
        var response = await student.PostAsJsonAsync(Mine(a.Id) + "/submit", new SubmitSubmission(1));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sent = (await response.Content.ReadFromJsonAsync<Submission>())!;
        Assert.Equal("submitted", sent.Status); Assert.Equal(2, sent.Version);
        await Error(await student.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("Değiştir", 2)), HttpStatusCode.Conflict, "already_submitted");
        await Error(await student.PostAsJsonAsync(Mine(a.Id) + "/submit", new SubmitSubmission(2)), HttpStatusCode.Conflict);
        using var scope = fixture.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<IAssignmentQuery>().HasSubmittedAsync(a.Id, 3));
    }
    [Theory]
    [InlineData(-1, false)] [InlineData(0, true)] [InlineData(10, true)] [InlineData(11, false)]
    public async Task T_ASG_04_ASG_BR_02_real_database_window_boundaries(int offset, bool allowed)
    {
        var now = fixture.Clock.Now;
        var a = await Create(Request() with { OpensAt = now.AddSeconds(-offset), DueAt = now.AddSeconds(5-offset), CutoffAt = now.AddSeconds(10-offset) });
        using var student = fixture.Client();
        var response = await student.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("Metin", 0));
        if (allowed) Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        else {
            await Error(response, HttpStatusCode.Conflict, "submission_closed");
            using var scope = fixture.Services.CreateScope();
            Assert.False(await scope.ServiceProvider.GetRequiredService<MoodleDbContext>().Submissions.AnyAsync(s => s.AssignmentId == a.Id));
        }
    }
    [Fact]
    public async Task T_ASG_04_ASG_BR_02_cutoff_rejects_update_and_submit_without_changes_extension_allows()
    {
        var a = await Create(); using var student = fixture.Client();
        await student.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("Korunacak", 0));
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<MoodleDbContext>();
            (await db.Assignments.SingleAsync(x => x.Id == a.Id)).CutoffAt = fixture.Clock.Now.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        await Error(await student.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("Kaybolmamalı", 1)), HttpStatusCode.Conflict);
        await Error(await student.PostAsJsonAsync(Mine(a.Id) + "/submit", new SubmitSubmission(1)), HttpStatusCode.Conflict);
        var mine = (await student.GetFromJsonAsync<MySubmission>(Mine(a.Id)))!;
        Assert.Equal("Korunacak", mine.Submission!.Text); Assert.Equal(1, mine.Submission.Version);
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<MoodleDbContext>();
            db.SubmissionRules.Add(new() { AssignmentId = a.Id, LearnerId = 3, ExtendedUntil = fixture.Clock.Now.AddMinutes(1) });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsJsonAsync(Mine(a.Id) + "/submit", new SubmitSubmission(1))).StatusCode);
    }
    [Fact]
    public async Task WBS2_version_conflict_two_concurrent_requests_one_winner()
    {
        var a = await Create(Request(false)); using var first = fixture.Client(); using var second = fixture.Client();
        var responses = await Task.WhenAll(first.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("Bir", 0)),
            second.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("İki", 0)));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        await Error(Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict, "version_conflict");
        responses = await Task.WhenAll(first.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("Üç", 1)),
            second.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("Dört", 1)));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var winner = (await responses.Single(r => r.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<Submission>())!;
        var mine = (await first.GetFromJsonAsync<MySubmission>(Mine(a.Id)))!;
        Assert.Equal(winner.Text, mine.Submission!.Text); Assert.Equal(2, mine.Submission.Version);
    }
    [Fact]
    public async Task T_ASG_03_ASG_FR_03_list_missing_filter_page_empty_and_access()
    {
        var a = await Create(Request(false)); using var student = fixture.Client(); using var teacher = fixture.Client(2);
        await student.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("Özel metin listede olmamalı", 0));
        var url = $"/api/v1/assignments/{a.Id}/submissions";
        var page = (await teacher.GetFromJsonAsync<SubmissionPage>(url + "?limit=1"))!;
        Assert.Equal("submitted", page.Items.Single().Status); Assert.Equal(3, page.NextCursor);
        page = (await teacher.GetFromJsonAsync<SubmissionPage>(url + "?status=notSubmitted&cursor=3"))!;
        Assert.Equal(new[] { 4, 5 }, page.Items.Select(x => x.LearnerId)); Assert.Null(page.NextCursor);
        Assert.Empty((await teacher.GetFromJsonAsync<SubmissionPage>(url + "?status=draft"))!.Items);
        Assert.DoesNotContain("Özel metin", await teacher.GetStringAsync(url));
        await Error(await student.GetAsync(url), HttpStatusCode.Forbidden);
        await Error(await teacher.GetAsync(url + "?limit=0"), HttpStatusCode.UnprocessableEntity);
        await Error(await teacher.GetAsync("/api/v1/assignments/2147483647/submissions"), HttpStatusCode.NotFound);
    }
    [Fact]
    public async Task T_ASG_05_ASG_NFR_01_direct_api_owner_grader_other_student_and_anonymous()
    {
        var a = await Create(); using var owner = fixture.Client(); using var other = fixture.Client(4);
        using var teacher = fixture.Client(2); using var anonymous = fixture.Client(null);
        var saved = (await (await owner.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("Gizli metin", 0))).Content.ReadFromJsonAsync<Submission>())!;
        var url = $"/api/v1/assignments/{a.Id}/submissions/{saved.Id}";
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await teacher.GetAsync(url)).StatusCode);
        await Error(await other.GetAsync(url), HttpStatusCode.NotFound);
        await Error(await anonymous.GetAsync(url), HttpStatusCode.Unauthorized);
        var otherMine = await other.GetFromJsonAsync<MySubmission>(Mine(a.Id) + "?learnerId=3");
        Assert.Null(otherMine!.Submission);
        var different = await Create();
        await Error(await teacher.GetAsync($"/api/v1/assignments/{different.Id}/submissions/{saved.Id}"), HttpStatusCode.NotFound);
    }
    [Fact]
    public async Task ASG_database_foreign_keys_unique_and_optimistic_token()
    {
        var a = await Create(); using var student = fixture.Client();
        await student.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("Metin", 0));
        using var scope1 = fixture.Services.CreateScope(); using var scope2 = fixture.Services.CreateScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<MoodleDbContext>(); var db2 = scope2.ServiceProvider.GetRequiredService<MoodleDbContext>();
        var s1 = await db1.Submissions.SingleAsync(s => s.AssignmentId == a.Id);
        var s2 = await db2.Submissions.SingleAsync(s => s.AssignmentId == a.Id);
        s1.Text = "Kazanan"; s1.Version++; await db1.SaveChangesAsync();
        s2.Text = "Kaybeden"; s2.Version++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
        db2.ChangeTracker.Clear();
        db2.Submissions.Add(new() { AssignmentId = a.Id, LearnerId = 3, Text = "Çift", Version = 1, UpdatedAt = fixture.Clock.Now });
        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
        db2.ChangeTracker.Clear();
        db2.Submissions.Add(new() { AssignmentId = int.MaxValue, LearnerId = 3, Text = "Yetim", Version = 1, UpdatedAt = fixture.Clock.Now });
        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
        Assert.False(db1.Database.HasPendingModelChanges());
    }
    [Fact]
    public async Task ASG_Blazor_pages_render_authorized_data_and_encode_submission_text()
    {
        var a = await Create(); using var owner = fixture.Client(); using var teacher = fixture.Client(2);
        var submission = (await (await owner.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("<script>alert(1)</script>", 0))).Content.ReadFromJsonAsync<Submission>())!;
        foreach (var path in new[] { $"/assignments/{a.Id}", $"/assignments/{a.Id}/submit" })
        {
            var html = await owner.GetStringAsync(path);
            Assert.DoesNotContain("role=\"alert\"", html);
            Assert.Contains("<h1>", html);
        }
        var list = WebUtility.HtmlDecode(await teacher.GetStringAsync($"/assignments/{a.Id}/submissions"));
        Assert.Contains("Taslak", list); Assert.Contains("Teslim edilmedi", list);
        var detail = await teacher.GetStringAsync($"/assignments/{a.Id}/submissions/{submission.Id}");
        Assert.DoesNotContain("<script>alert", detail); Assert.Contains("&lt;script&gt;", detail);
        using var other = fixture.Client(4);
        Assert.DoesNotContain("alert(1)", await other.GetStringAsync($"/assignments/{a.Id}/submissions/{submission.Id}"));
        Assert.Contains("createAssignment", await teacher.GetStringAsync("/courses/1/assignments/new"));
    }
    private static string Antiforgery(string html)
    {
        var match = System.Text.RegularExpressions.Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "SSR form must include an antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    [Fact]
    public async Task ASG_Blazor_create_form_posts_to_api_and_persists()
    {
        using var teacher = fixture.Client(2);
        var path = "/courses/1/assignments/new";
        var token = Antiforgery(await teacher.GetStringAsync(path));
        var title = "SSR " + Guid.NewGuid();
        var response = await teacher.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string> {
            ["__RequestVerificationToken"] = token, ["_handler"] = "createAssignment",
            ["Form.SectionId"] = "1", ["Form.Title"] = title, ["Form.Instructions"] = "SSR metni",
            ["Form.RequiresConfirmation"] = "true"
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ödev oluşturuldu", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        using var scope = fixture.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<MoodleDbContext>().Assignments.AnyAsync(a => a.Title == title));
    }
    [Fact]
    public async Task ASG_Blazor_submission_form_saves_conflict_preserves_input_and_confirmation_sends()
    {
        var a = await Create(); using var owner = fixture.Client();
        var path = $"/assignments/{a.Id}/submit";
        var token = Antiforgery(await owner.GetStringAsync(path));
        var response = await owner.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string> {
            ["__RequestVerificationToken"] = token, ["_handler"] = "saveSubmission",
            ["Form.Text"] = "UI taslak", ["Form.Version"] = "0"
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Teslim kaydedildi", WebUtility.HtmlDecode(html));
        token = Antiforgery(html);
        response = await owner.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string> {
            ["__RequestVerificationToken"] = token, ["_handler"] = "saveSubmission",
            ["Form.Text"] = "Korunacak kullanıcı metni", ["Form.Version"] = "0"
        }));
        html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Korunacak kullanıcı metni", html); Assert.Contains("Güncel sürümü", html);
        token = Antiforgery(await owner.GetStringAsync(path));
        response = await owner.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string> {
            ["__RequestVerificationToken"] = token, ["_handler"] = "confirmSubmission",
            ["Confirmation.Version"] = "1", ["Confirmation.Confirmed"] = "true"
        }));
        Assert.Contains("Teslim gönderildi", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Equal("submitted", (await owner.GetFromJsonAsync<MySubmission>(Mine(a.Id)))!.Status);
    }
    [Fact]
    public async Task ASG_malformed_json_returns_standard_400()
    {
        var a = await Create(); using var owner = fixture.Client();
        await Error(await owner.PutAsync(Mine(a.Id), new StringContent("{", System.Text.Encoding.UTF8, "application/json")), HttpStatusCode.BadRequest);
    }
    [Fact]
    public async Task ASG_seed_is_idempotent_and_preserves_existing_data()
    {
        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<MoodleDbContext>();
        async Task Seed() => await AsgSeed.ApplyAsync(db, sp.GetRequiredService<ICourseDirectory>(),
            sp.GetRequiredService<ICourseSectionQuery>(), sp.GetRequiredService<IEnrolmentQuery>(), allowSyntheticFixture: true);
        await Seed();
        var count = await db.Assignments.CountAsync(); var submissions = await db.Submissions.CountAsync();
        await Seed();
        Assert.Equal(count, await db.Assignments.CountAsync()); Assert.Equal(submissions, await db.Submissions.CountAsync());
        var a = await db.Assignments.SingleAsync(x => x.Title == "SENG101 Ödev 2");
        Assert.Equal("draft", (await db.Submissions.SingleAsync(x => x.AssignmentId == a.Id)).Status);
    }
    [Fact]
    public async Task ASG_openapi_contains_all_seven_operations()
    {
        using var client = fixture.Client();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/openapi.json"));
        var paths = doc.RootElement.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/v1/courses/{courseId}/assignments").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/v1/assignments/{id}").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/v1/assignments/{id}/my-submission").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/v1/assignments/{id}/my-submission").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/assignments/{id}/my-submission/submit").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/v1/assignments/{id}/submissions").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/v1/assignments/{id}/submissions/{submissionId}").TryGetProperty("get", out _));
    }
}
