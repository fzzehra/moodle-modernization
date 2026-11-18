using Microsoft.Extensions.DependencyInjection;
using Moodle.Infrastructure;
using Moodle.Application.Abstractions;
using Moodle.Application.Assignments;
using Moodle.Application.Errors;
using Moodle.Domain.Assignments;
using Moodle.Infrastructure.Fakes;

namespace Moodle.UnitTests;

public class AssignmentServiceTests
{
    private readonly MemoryStore store = new();
    private readonly TestUser user = new();
    private readonly TestPolicy policy = new();
    private readonly TestClock clock = new();
    private AssignmentService Service => new(store, user, policy, new FakeCourseDirectory(),
        new FakeCourseSectionQuery(), new FakeEnrolmentQuery(), clock, new FakeAssignmentLearnerQuery(), policy);
    private static CreateAssignment Request => new(1, "Ödev", "Metin", null, null, null, true, ["text"]);

    [Fact]
    public async Task T_ASG_02_ASG_FR_01_creates_in_course_and_section()
    {
        var result = await Service.CreateAsync(1, Request);
        Assert.Equal(1, result.CourseId); Assert.Equal(1, result.SectionId); Assert.Equal("Ödev", result.Title);
    }
    [Theory]
    [InlineData("", 1)] [InlineData(" ", 1)] [InlineData("Ödev", 5)]
    public async Task T_ASG_02_ASG_FR_01_rejects_invalid_fields(string title, int section)
    {
        var ex = await Assert.ThrowsAsync<AppException>(() => Service.CreateAsync(1, Request with { Title = title, SectionId = section }));
        Assert.Equal(422, ex.StatusCode); Assert.Empty(store.Assignments);
    }
    [Fact]
    public async Task T_ASG_02_ASG_FR_01_rejects_dates_and_types()
    {
        Assert.Equal(422, (await Assert.ThrowsAsync<AppException>(() => Service.CreateAsync(1,
            Request with { OpensAt = clock.Now, CutoffAt = clock.Now.AddSeconds(-1) }))).StatusCode);
        Assert.Equal(422, (await Assert.ThrowsAsync<AppException>(() => Service.CreateAsync(1,
            Request with { EnabledSubmissionTypes = ["file"] }))).StatusCode);
    }
    [Theory]
    [InlineData(true, "draft")] [InlineData(false, "submitted")]
    public async Task T_ASG_01_ASG_FR_02_ASG_BR_01_save_confirmation_and_read(bool confirmation, string status)
    {
        var a = await Service.CreateAsync(1, Request with { RequiresConfirmation = confirmation });
        var saved = await Service.SaveAsync(a.Id, new("Teslim", 0));
        Assert.True(saved.Created); Assert.Equal(status, saved.Submission.Status);
        Assert.Equal("Teslim", (await Service.MineAsync(a.Id)).Submission!.Text);
        var updated = await Service.SaveAsync(a.Id, new("Yeni metin", 1));
        Assert.False(updated.Created); Assert.Equal(2, updated.Submission.Version);
    }
    [Fact]
    public async Task T_ASG_01_ASG_FR_02_submit_then_edit_is_rejected()
    {
        var a = await Service.CreateAsync(1, Request);
        await Service.SaveAsync(a.Id, new("Taslak", 0));
        var sent = await Service.SubmitAsync(a.Id, new(1));
        Assert.Equal("submitted", sent.Status); Assert.Equal(clock.Now, sent.SubmittedAt);
        Assert.Equal("already_submitted", (await Assert.ThrowsAsync<AppException>(() => Service.SaveAsync(a.Id, new("Edit", 2)))).Code);
    }
    [Theory]
    [InlineData(-1, false)] [InlineData(0, true)] [InlineData(10, true)] [InlineData(11, false)]
    public async Task T_ASG_04_ASG_BR_02_window_boundaries(int seconds, bool allowed)
    {
        var start = clock.Now;
        var a = await Service.CreateAsync(1, Request with { OpensAt = start, DueAt = start.AddSeconds(5), CutoffAt = start.AddSeconds(10) });
        clock.Now = start.AddSeconds(seconds);
        if (allowed) await Service.SaveAsync(a.Id, new("Metin", 0));
        else { Assert.Equal("submission_closed", (await Assert.ThrowsAsync<AppException>(() => Service.SaveAsync(a.Id, new("Metin", 0)))).Code); Assert.Empty(store.Submissions); }
    }
    [Fact]
    public async Task T_ASG_04_ASG_BR_02_extension_and_lock()
    {
        var a = await Service.CreateAsync(1, Request with { CutoffAt = clock.Now.AddMinutes(-1) });
        store.Rule = new() { AssignmentId = a.Id, LearnerId = 3, ExtendedUntil = clock.Now.AddMinutes(1) };
        await Service.SaveAsync(a.Id, new("Metin", 0));
        store.Rule.Locked = true;
        Assert.Equal("submission_locked", (await Assert.ThrowsAsync<AppException>(() => Service.SaveAsync(a.Id, new("Yeni", 1)))).Code);
        Assert.Equal("Metin", store.Submissions.Single().Text);
    }
    [Fact]
    public async Task WBS2_version_conflict_preserves_text()
    {
        var a = await Service.CreateAsync(1, Request);
        await Service.SaveAsync(a.Id, new("İlk", 0));
        Assert.Equal("version_conflict", (await Assert.ThrowsAsync<AppException>(() => Service.SaveAsync(a.Id, new("Eski", 0)))).Code);
        Assert.Equal("İlk", store.Submissions.Single().Text);
    }
    [Fact]
    public async Task T_ASG_03_ASG_FR_03_lists_missing_and_submitted_with_paging()
    {
        var a = await Service.CreateAsync(1, Request with { RequiresConfirmation = false });
        await Service.SaveAsync(a.Id, new("Teslim", 0));
        var page = await Service.ListAsync(a.Id, limit: 1);
        Assert.Equal("submitted", page.Items.Single().Status); Assert.Equal(3, page.NextCursor);
        var rest = await Service.ListAsync(a.Id, "notSubmitted", page.NextCursor);
        Assert.Equal(new[] { 4, 5 }, rest.Items.Select(r => r.LearnerId)); Assert.Null(rest.NextCursor);
    }
    [Fact]
    public async Task T_ASG_03_ASG_FR_03_denies_student_list_and_invalid_filter()
    {
        var a = await Service.CreateAsync(1, Request);
        Assert.Equal(422, (await Assert.ThrowsAsync<AppException>(() => Service.ListAsync(a.Id, "bad"))).StatusCode);
        policy.Grade = false;
        Assert.Equal(403, (await Assert.ThrowsAsync<AppException>(() => Service.ListAsync(a.Id))).StatusCode);
    }
    [Fact]
    public async Task T_ASG_05_ASG_NFR_01_denies_other_student_but_allows_owner_and_grader()
    {
        var a = await Service.CreateAsync(1, Request);
        var s = (await Service.SaveAsync(a.Id, new("Özel metin", 0))).Submission;
        Assert.Equal(s.Text, (await Service.ReadAsync(a.Id, s.Id)).Text);
        user.UserId = 4; policy.Grade = false;
        Assert.Equal(404, (await Assert.ThrowsAsync<AppException>(() => Service.ReadAsync(a.Id, s.Id))).StatusCode);
        policy.Grade = true;
        Assert.Equal(s.Text, (await Service.ReadAsync(a.Id, s.Id)).Text);
    }
    [Fact]
    public async Task T_ASG_05_ASG_NFR_01_no_session_and_no_enrolment()
    {
        var a = await Service.CreateAsync(1, Request);
        user.UserId = null;
        Assert.Equal(401, (await Assert.ThrowsAsync<AppException>(() => Service.MineAsync(a.Id))).StatusCode);
        user.UserId = 99;
        Assert.Equal(403, (await Assert.ThrowsAsync<AppException>(() => Service.SaveAsync(a.Id, new("Metin", 0)))).StatusCode);
    }
    [Fact]
    public async Task IAssignmentQuery_returns_real_assignment_and_only_submitted_status()
    {
        var a = await Service.CreateAsync(1, Request);
        var query = new Moodle.Infrastructure.Persistence.AssignmentQuery(store);
        Assert.Equal(a.Title, (await query.FindAsync(a.Id))!.Title);
        Assert.Null(await query.FindAsync(999));
        Assert.False(await query.HasSubmittedAsync(a.Id, 3));
        await Service.SaveAsync(a.Id, new("Taslak", 0));
        Assert.False(await query.HasSubmittedAsync(a.Id, 3));
        await Service.SubmitAsync(a.Id, new(1));
        Assert.True(await query.HasSubmittedAsync(a.Id, 3));
        Assert.False(await query.HasSubmittedAsync(a.Id, 4));
    }
    [Theory]
    [InlineData(3,1,1)] [InlineData(4,1,1)] [InlineData(0,1,1)]
    [InlineData(3,2,1)] [InlineData(3,1,999)]
    public async Task Scope_is_identical_for_list_and_detail(int target,int course,int module)
    {
        var a=await Service.CreateAsync(1,Request);
        var submission=(await Service.SaveAsync(a.Id,new("Private",0))).Submission;
        user.UserId=2;
        policy.Scope=[target]; policy.Course=course; policy.Assignment=module;
        var page=await Service.ListAsync(a.Id);
        var allowed=target==3 && course==1 && module==a.Id;
        Assert.Equal(target is 3 or 4 && course==1 && module==a.Id ? new[]{target} : [],page.Items.Select(x=>x.LearnerId));
        if(allowed) Assert.Equal("Private",(await Service.ReadAsync(a.Id,submission.Id)).Text);
        else Assert.Equal(404,(await Assert.ThrowsAsync<AppException>(()=>Service.ReadAsync(a.Id,submission.Id))).StatusCode);
    }
    [Fact]
    public async Task Unknown_scope_defaults_to_deny_and_no_obligated_students()
    {
        using var provider=new ServiceCollection().AddInfrastructure().BuildServiceProvider();
        using var scope=Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(provider);
        var query=Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IAssignmentLearnerQuery>(scope.ServiceProvider);
        var access=Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ISubmissionAccessPolicy>(scope.ServiceProvider);
        Assert.Empty(await query.ListRequiredLearnerIdsAsync(1));
        Assert.False(await access.CanAssessLearnerAsync(2,1,1,3));
    }    [Theory]
    [InlineData(2,1,1,3,true)]
    [InlineData(2,1,1,2,false)]
    [InlineData(6,1,1,3,false)]
    [InlineData(2,2,1,3,false)]
    [InlineData(2,1,999,3,false)]
    public async Task Development_scope_allows_only_explicit_fixture(int evaluator,int course,int module,int learner,bool expected)
    {
        await Service.CreateAsync(1,Request);
        var fake=new FakeSubmissionAccessPolicy(store);
        Assert.Equal(expected,await fake.CanAssessLearnerAsync(evaluator,course,module,learner));
        var roster=new FakeAssignmentLearnerQuery();
        Assert.Equal(new[]{3,4,5},await roster.ListRequiredLearnerIdsAsync(1));
        Assert.Empty(await roster.ListRequiredLearnerIdsAsync(2));
    }    private sealed class TestUser : ICurrentUser
    {
        public int? UserId { get; set; } = 3;
        public bool IsAuthenticated => UserId is not null;
        public string? Username => "synthetic";
    }
    private sealed class TestPolicy : IAccessPolicy, ISubmissionAccessPolicy
    {
        public bool Grade { get; set; } = true;
        public HashSet<int> Scope { get; set; } = [3,4,5];
        public int Course { get; set; } = 1;
        public int Assignment { get; set; } = 1;
        public Task<bool> CanAssessLearnerAsync(int evaluatorId,int courseId,int assignmentId,int learnerId,CancellationToken ct=default)
            => Task.FromResult(courseId == Course && assignmentId == Assignment && Scope.Contains(learnerId));
        public Task<bool> HasCapabilityAsync(int userId, string capability, ContextRef context, CancellationToken ct = default)
            => Task.FromResult(capability != "mod/assign:grade" || Grade);
    }
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class MemoryStore : IAssignmentStore
    {
        public List<Assignment> Assignments { get; } = [];
        public List<Submission> Submissions { get; } = [];
        public SubmissionRule? Rule { get; set; }
        public Task<Assignment?> FindAsync(int id, CancellationToken ct) => Task.FromResult(Assignments.SingleOrDefault(a => a.Id == id));
        public Task AddAsync(Assignment a, CancellationToken ct) { a.Id = Assignments.Count + 1; Assignments.Add(a); return Task.CompletedTask; }
        public Task<Submission?> FindSubmissionAsync(int id, int learnerId, CancellationToken ct) => Task.FromResult(Submissions.SingleOrDefault(s => s.AssignmentId == id && s.LearnerId == learnerId));
        public Task<Submission?> FindSubmissionByIdAsync(int id, int sid, CancellationToken ct) => Task.FromResult(Submissions.SingleOrDefault(s => s.AssignmentId == id && s.Id == sid));
        public Task<SubmissionRule?> FindRuleAsync(int id, int learnerId, CancellationToken ct) => Task.FromResult(Rule);
        public Task<SubmissionPage> ListRowsAsync(int id, IReadOnlyList<int> learners, string? status, int? cursor, int limit, DateTimeOffset? dueAt, CancellationToken ct)
        {
            var rows = learners.Distinct().Order().Where(x => x > (cursor ?? 0)).Select(learner => {
                var s = Submissions.SingleOrDefault(x => x.AssignmentId == id && x.LearnerId == learner);
                return new SubmissionRow(learner, s?.Status ?? "notSubmitted", s?.Id, s?.SubmittedAt, s?.UpdatedAt, s?.Version ?? 0, s?.SubmittedAt > dueAt);
            }).Where(row => status is null || row.Status == status).Take(limit + 1).ToArray();
            return Task.FromResult(new SubmissionPage(rows.Take(limit).ToArray(), rows.Length > limit ? rows[limit - 1].LearnerId : null));
        }
        public void AddSubmission(Submission s) { s.Id = Submissions.Count + 1; Submissions.Add(s); }
        public Task<T> WriteAsync<T>(int id, Func<Task<T>> action, CancellationToken ct) => action();
    }
}
