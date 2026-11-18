using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Application.Abstractions;
using Moodle.Application.Assignments;
using Moodle.Application.Errors;
using Moodle.Domain.Assignments;
using Moodle.Infrastructure.Persistence;

namespace Moodle.IntegrationTests;

public class AssignmentRevisionTests(AsgFixture fixture) : IClassFixture<AsgFixture>
{
    private async Task<Assignment> Create(bool confirmation = true, DateTimeOffset? opens = null, DateTimeOffset? cutoff = null)
    {
        using var teacher = fixture.Client(2);
        var response = await teacher.PostAsJsonAsync("/api/v1/courses/1/assignments",
            new CreateAssignment(1, "Revision " + Guid.NewGuid(), "", opens, null, cutoff, confirmation, ["text"]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Assignment>())!;
    }
    private static string Mine(int id) => $"/api/v1/assignments/{id}/my-submission";
    private static async Task Error(HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = body.RootElement.GetProperty("error");
        Assert.False(string.IsNullOrEmpty(error.GetProperty("message").GetString()));
        if (code is not null) Assert.Equal(code, error.GetProperty("code").GetString());
    }
    [Theory]
    [InlineData("locked")] [InlineData("disabled")] [InlineData("future")] [InlineData("closed")]
    public async Task ASG_BR_02_closed_conditions_reject_save_and_submit_and_disable_SSR_forms(string condition)
    {
        var a = await Create(); using var student = fixture.Client();
        Assert.Equal(HttpStatusCode.Created, (await student.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("Keep", 0))).StatusCode);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MoodleDbContext>();
            var entity = await db.Assignments.SingleAsync(x => x.Id == a.Id);
            if (condition == "locked") db.SubmissionRules.Add(new() { AssignmentId = a.Id, LearnerId = 3, Locked = true });
            if (condition == "disabled") entity.EnabledSubmissionTypes = [];
            if (condition == "future") entity.OpensAt = fixture.Clock.Now.AddMinutes(1);
            if (condition == "closed") entity.CutoffAt = fixture.Clock.Now.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        var view = (await student.GetFromJsonAsync<AssignmentView>($"/api/v1/assignments/{a.Id}"))!;
        Assert.False(view.Permissions.CanSave); Assert.False(view.Permissions.CanSend); Assert.NotNull(view.Permissions.UnavailableReason);
        var html = WebUtility.HtmlDecode(await student.GetStringAsync($"/assignments/{a.Id}/submit"));
        Assert.Contains(view.Permissions.UnavailableReason, html);
        Assert.Matches("<button[^>]*disabled[^>]*>Kaydet", html);
        Assert.Matches("<button[^>]*disabled[^>]*>Teslimi gönder", html);
        await Error(await student.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("Reject", 1)), HttpStatusCode.Conflict);
        await Error(await student.PostAsJsonAsync(Mine(a.Id) + "/submit", new SubmitSubmission(1)), HttpStatusCode.Conflict);
        // Forging the disabled SSR form must not bypass the same API checks.
        var token = System.Text.RegularExpressions.Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var post = await student.PostAsync($"/assignments/{a.Id}/submit", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"] = token, ["_handler"] = "saveSubmission", ["Form.Text"] = "Forged", ["Form.Version"] = "1"
        }));
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var mine = (await student.GetFromJsonAsync<MySubmission>(Mine(a.Id)))!;
        Assert.Equal("Keep", mine.Submission!.Text); Assert.Equal(1, mine.Submission.Version); Assert.Equal("draft", mine.Status);
    }
    [Theory]
    [InlineData("", 422)] [InlineData("  ", 422)]
    public async Task ASG_FR_02_blank_text_is_rejected_without_insert(string text, int status)
    {
        var a = await Create(); using var student = fixture.Client();
        await Error(await student.PutAsJsonAsync(Mine(a.Id), new SaveSubmission(text, 0)), (HttpStatusCode)status, "invalid_text");
        Assert.Null((await student.GetFromJsonAsync<MySubmission>(Mine(a.Id)))!.Submission);
    }
    [Theory]
    [InlineData("{\"text\":\"test\"}", false)]
    [InlineData("{\"text\":\"test\",\"version\":\"wrong\"}", false)]
    [InlineData("{}", true)]
    [InlineData("{\"version\":\"wrong\"}", true)]
    public async Task ASG_version_is_required_and_numeric(string json, bool submit)
    {
        var a = await Create(); using var student = fixture.Client();
        using var request = new HttpRequestMessage(submit ? HttpMethod.Post : HttpMethod.Put, Mine(a.Id) + (submit ? "/submit" : "")) {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        await Error(await student.SendAsync(request), HttpStatusCode.BadRequest);
    }
    [Fact]
    public async Task ASG_wrong_content_type_returns_415()
    {
        var a = await Create(); using var student = fixture.Client();
        await Error(await student.PutAsync(Mine(a.Id), new StringContent("text", Encoding.UTF8, "text/plain")), HttpStatusCode.UnsupportedMediaType);
    }
    [Theory]
    [InlineData("status=invalid",422)] [InlineData("cursor=-1",422)] [InlineData("limit=101",422)]
    [InlineData("limit=0",422)] [InlineData("cursor=bad",400)] [InlineData("limit=bad",400)]
    public async Task ASG_FR_03_invalid_page_inputs(string query, int status)
    {
        var a = await Create(); using var teacher = fixture.Client(2);
        await Error(await teacher.GetAsync($"/api/v1/assignments/{a.Id}/submissions?{query}"), (HttpStatusCode)status);
    }
    [Fact]
    public async Task ASG_FR_02_submit_without_draft_and_stale_submit()
    {
        var a = await Create(); using var student = fixture.Client();
        await Error(await student.PostAsJsonAsync(Mine(a.Id) + "/submit", new SubmitSubmission(0)), HttpStatusCode.NotFound);
        await student.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("Draft",0));
        await Error(await student.PostAsJsonAsync(Mine(a.Id) + "/submit", new SubmitSubmission(0)), HttpStatusCode.Conflict, "version_conflict");
        Assert.Equal("draft", (await student.GetFromJsonAsync<MySubmission>(Mine(a.Id)))!.Status);
    }
    [Fact]
    public async Task ASG_save_submit_race_has_one_winner_and_no_lost_update()
    {
        var a = await Create(); using var first = fixture.Client(); using var second = fixture.Client();
        await first.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("Initial",0));
        var results = await Task.WhenAll(first.PutAsJsonAsync(Mine(a.Id), new SaveSubmission("Updated",1)), second.PostAsJsonAsync(Mine(a.Id)+"/submit",new SubmitSubmission(1)));
        Assert.Single(results, x=>x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x=>x.StatusCode == HttpStatusCode.Conflict);
        var state=(await first.GetFromJsonAsync<MySubmission>(Mine(a.Id)))!.Submission!;
        Assert.Equal(2,state.Version);
        Assert.Equal(state.Status == "submitted" ? "Initial" : "Updated",state.Text);
    }
    [Fact]
    public async Task Y2_shared_pending_changes_survive_and_are_not_implicitly_saved()
    {
        var a = await Create(); using var scope = fixture.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<MoodleDbContext>();
        var tracked=await db.Assignments.SingleAsync(x=>x.Id==a.Id);
        tracked.Title="Pending caller edit";
        var pending=new Assignment { CourseId=1, SectionId=1, Title="Pending caller insert" };
        db.Assignments.Add(pending);
        var store=scope.ServiceProvider.GetRequiredService<IAssignmentStore>();
        await store.WriteAsync(a.Id, ()=> {store.AddSubmission(new() {AssignmentId=a.Id,LearnerId=3,Text="ASG",Version=1,UpdatedAt=fixture.Clock.Now}); return Task.FromResult(true);},default);
        Assert.Equal(EntityState.Modified,db.Entry(tracked).State); Assert.Equal(EntityState.Added,db.Entry(pending).State);
        using(var other=fixture.Services.CreateScope()) {
            var check=other.ServiceProvider.GetRequiredService<MoodleDbContext>();
            Assert.Equal(a.Title,(await check.Assignments.SingleAsync(x=>x.Id==a.Id)).Title);
            Assert.False(await check.Assignments.AnyAsync(x=>x.Title==pending.Title));
            Assert.True(await check.Submissions.AnyAsync(x=>x.AssignmentId==a.Id));
        }
        await db.SaveChangesAsync(); Assert.True(pending.Id>0);
    }
    [Fact]
    public async Task Y2_caller_transaction_owns_commit_and_rollback()
    {
        var a=await Create(); using var scope=fixture.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<MoodleDbContext>();
        var store=scope.ServiceProvider.GetRequiredService<IAssignmentStore>();
        await using var tx=await db.Database.BeginTransactionAsync();
        await store.WriteAsync(a.Id,()=> {store.AddSubmission(new(){AssignmentId=a.Id,LearnerId=3,Text="Uncommitted",Version=1,UpdatedAt=fixture.Clock.Now});return Task.FromResult(true);},default);
        Assert.Same(tx,db.Database.CurrentTransaction);
        Assert.True(await db.Submissions.AnyAsync(x=>x.AssignmentId==a.Id));
        using(var other=fixture.Services.CreateScope())
            Assert.False(await other.ServiceProvider.GetRequiredService<MoodleDbContext>().Submissions.AnyAsync(x=>x.AssignmentId==a.Id));
        await tx.RollbackAsync();
        using var after=fixture.Services.CreateScope();
        Assert.False(await after.ServiceProvider.GetRequiredService<MoodleDbContext>().Submissions.AnyAsync(x=>x.AssignmentId==a.Id));
    }
    [Fact]
    public async Task Y2_savepoint_failure_preserves_prior_work_and_outer_transaction_can_commit()
    {
        var a=await Create(); using var scope=fixture.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<MoodleDbContext>();
        var store=scope.ServiceProvider.GetRequiredService<IAssignmentStore>();
        await using var tx=await db.Database.BeginTransactionAsync();
        await db.Assignments.Where(x=>x.Id==a.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Title,"Caller before savepoint"));
        await Assert.ThrowsAsync<AppException>(()=>store.WriteAsync<bool>(a.Id,async ()=> {
            await db.Assignments.Where(x=>x.Id==a.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Title,"Must roll back"));
            throw AppException.Conflict("test","Synthetic rejection");
        },default));
        Assert.Equal("Caller before savepoint",(await db.Assignments.AsNoTracking().SingleAsync(x=>x.Id==a.Id)).Title);
        await store.WriteAsync(a.Id,()=> {store.AddSubmission(new(){AssignmentId=a.Id,LearnerId=3,Text="Recovery",Version=1,UpdatedAt=fixture.Clock.Now});return Task.FromResult(true);},default);
        await tx.CommitAsync();
        using var after=fixture.Services.CreateScope();
        Assert.True(await after.ServiceProvider.GetRequiredService<MoodleDbContext>().Submissions.AnyAsync(x=>x.AssignmentId==a.Id));
    }
    [Fact]
    public async Task O3_seed_requires_explicit_synthetic_fixture()
    {
        using var scope=fixture.Services.CreateScope(); var sp=scope.ServiceProvider;
        var db=sp.GetRequiredService<MoodleDbContext>(); var before=await db.Assignments.CountAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>AsgSeed.ApplyAsync(db,sp.GetRequiredService<ICourseDirectory>(),sp.GetRequiredService<ICourseSectionQuery>(),sp.GetRequiredService<IEnrolmentQuery>()));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>AsgSeed.ApplyAsync(db,sp.GetRequiredService<ICourseDirectory>(),sp.GetRequiredService<ICourseSectionQuery>(),new RealEnrolments(),allowSyntheticFixture:true));
        Assert.Equal(before,await db.Assignments.CountAsync());
    }
    [Fact]
    public async Task D2_OpenAPI_documents_exact_error_sets_and_standard_schema()
    {
        using var client=fixture.Client();
        using var doc=JsonDocument.Parse(await client.GetStringAsync("/api/v1/openapi.json"));
        var paths=doc.RootElement.GetProperty("paths");
        var operations=new (string Path,string Method,int[] Errors)[] {
            ("/api/v1/courses/{courseId}/assignments","post",[400,401,403,404,415,422,500]),
            ("/api/v1/assignments/{id}","get",[401,404,500]),
            ("/api/v1/assignments/{id}/my-submission","get",[401,403,404,500]),
            ("/api/v1/assignments/{id}/my-submission","put",[400,401,403,404,409,415,422,500]),
            ("/api/v1/assignments/{id}/my-submission/submit","post",[400,401,403,404,409,415,500]),
            ("/api/v1/assignments/{id}/submissions","get",[400,401,403,404,422,500]),
            ("/api/v1/assignments/{id}/submissions/{submissionId}","get",[401,403,404,500])
        };
        foreach(var operation in operations) {
            var responses=paths.GetProperty(operation.Path).GetProperty(operation.Method).GetProperty("responses");
            Assert.Equal(operation.Errors, responses.EnumerateObject().Where(x=>int.Parse(x.Name)>=400).Select(x=>int.Parse(x.Name)).Order());
            foreach(var status in operation.Errors) {
                var schema=responses.GetProperty(status.ToString()).GetProperty("content").GetProperty("application/json").GetProperty("schema");
                Assert.Equal("#/components/schemas/ApiErrorResponse",schema.GetProperty("$ref").GetString());
            }
        }
        var schemas=doc.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.GetProperty("ApiErrorResponse").GetProperty("properties").TryGetProperty("error",out _));
        var fields=schemas.GetProperty("ApiError").GetProperty("properties");
        Assert.True(fields.TryGetProperty("code",out _)); Assert.True(fields.TryGetProperty("message",out _));
    }
    [Fact]
    public async Task O5_projection_filters_and_pages_without_loading_text_entities()
    {
        var a=await Create(); using var student=fixture.Client();
        await student.PutAsJsonAsync(Mine(a.Id),new SaveSubmission(new string('x',100_000),0));
        using var scope=fixture.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<MoodleDbContext>();
        var store=scope.ServiceProvider.GetRequiredService<IAssignmentStore>();
        var first=await store.ListRowsAsync(a.Id,[3,4,5],null,null,1,null,default);
        Assert.Equal(3,first.Items.Single().LearnerId); Assert.Equal(3,first.NextCursor);
        var next=await store.ListRowsAsync(a.Id,[3,4,5],"notSubmitted",first.NextCursor,1,null,default);
        Assert.Equal(4,next.Items.Single().LearnerId); Assert.Equal(4,next.NextCursor);
        var last=await store.ListRowsAsync(a.Id,[3,4,5],"notSubmitted",next.NextCursor,1,null,default);
        Assert.Equal(5,last.Items.Single().LearnerId); Assert.Null(last.NextCursor);
        Assert.Empty(db.ChangeTracker.Entries<Submission>());
        Assert.Empty((await store.ListRowsAsync(a.Id,[],null,null,1,null,default)).Items);
        using var teacher=fixture.Client(2);
        var html=await teacher.GetStringAsync($"/assignments/{a.Id}/submissions?status=draft");
        var option=System.Text.RegularExpressions.Regex.Match(html,"<option[^>]*value=\"draft\"[^>]*>").Value;
        Assert.Contains("selected",option);
    }
    [Fact]
    public async Task D1_SSR_next_page_keeps_filter_and_cursor()
    {
        var handler = new ListPages();
        using var host = fixture.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddHttpClient<Moodle.Web.Assignments.AsgClient>(client => client.BaseAddress = new Uri("http://localhost"))
                .ConfigurePrimaryHttpMessageHandler(() => handler)));
        using var client = host.CreateClient();
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/assignments/42/submissions?status=notSubmitted"));
        Assert.Contains("/assignments/42/submissions?cursor=50&status=notSubmitted", html);
        Assert.Contains("selected", System.Text.RegularExpressions.Regex.Match(html,"<option[^>]*value=\"notSubmitted\"[^>]*>").Value);
        html = WebUtility.HtmlDecode(await client.GetStringAsync("/assignments/42/submissions?cursor=50&status=notSubmitted"));
        Assert.DoesNotContain("Sonraki sayfa",html);
        Assert.Equal(2,handler.Queries.Count);
        Assert.Contains("limit=50",handler.Queries[0]); Assert.Contains("status=notSubmitted",handler.Queries[0]);
        Assert.Contains("cursor=50",handler.Queries[1]); Assert.Contains("status=notSubmitted",handler.Queries[1]);
    }
    private sealed class ListPages : HttpMessageHandler
    {
        public List<string> Queries { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Queries.Add(request.RequestUri!.Query);
            var second = request.RequestUri.Query.Contains("cursor=50");
            var page = new SubmissionPage([new(second ? 51 : 50,"notSubmitted",null,null,null,0,false)],second ? null : 50);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=JsonContent.Create(page) });
        }
    }
    private sealed class RealEnrolments : IEnrolmentQuery
    {
        public Task<bool> IsActivelyEnrolledAsync(int userId,int courseId,CancellationToken ct=default)=>Task.FromResult(true);
        public Task<IReadOnlyList<int>> ListEnrolledUserIdsAsync(int courseId,CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<int>>([100,101,102]);
    }
}
