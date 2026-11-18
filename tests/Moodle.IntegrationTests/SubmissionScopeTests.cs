using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moodle.Application.Abstractions;
using Moodle.Application.Assignments;
using Moodle.Domain.Assignments;
using Moodle.Infrastructure.Persistence;
namespace Moodle.IntegrationTests;

public class SubmissionScopeTests(AsgFixture fixture) : IClassFixture<AsgFixture>
{
    [Theory]
    [InlineData(6,3,1,false)] [InlineData(7,4,1,false)]
    [InlineData(8,0,1,false)] [InlineData(6,3,2,false)]
    [InlineData(6,3,1,true)]
    public async Task Evaluator_scope_is_identical_in_list_and_detail(int evaluator,int target,int course,bool wrongModule)
    {
        var (id,sid)=await Seed();
        var scope=new Scope(id,course,wrongModule ? id+999 : id);
        using var host=Host(scope);
        using var client=host.CreateClient(); client.DefaultRequestHeaders.Add("X-Test-User",evaluator.ToString());
        var page=await client.GetFromJsonAsync<SubmissionPage>($"/api/v1/assignments/{id}/submissions");
        var allowed=course==1 && !wrongModule;
        Assert.Equal(allowed && target>0 ? new[]{target} : [],page!.Items.Select(x=>x.LearnerId));
        var detail=await client.GetAsync($"/api/v1/assignments/{id}/submissions/{sid}");
        Assert.Equal(allowed && target==3 ? HttpStatusCode.OK : HttpStatusCode.NotFound,detail.StatusCode);
        if(detail.StatusCode!=HttpStatusCode.OK) Assert.DoesNotContain("Private learner 3",await detail.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Mixed_enrolments_exclude_teacher_observer_and_inactive_learner_before_paging()
    {
        var(id,_)=await Seed(); var scope=new Scope(id,1,id);
        using var host=Host(scope); using var client=host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User","2");
        var first=await client.GetFromJsonAsync<SubmissionPage>($"/api/v1/assignments/{id}/submissions?limit=1");
        Assert.Equal(3,first!.Items.Single().LearnerId); Assert.Equal(3,first.NextCursor);
        var next=await client.GetFromJsonAsync<SubmissionPage>($"/api/v1/assignments/{id}/submissions?limit=1&cursor=3&status=notSubmitted");
        Assert.Equal(4,next!.Items.Single().LearnerId); Assert.Null(next.NextCursor);
        // Historical teacher submission must not appear in either evaluator path.
        using var dbScope=fixture.Services.CreateScope(); var db=dbScope.ServiceProvider.GetRequiredService<MoodleDbContext>();
        var teacher=new Submission{AssignmentId=id,LearnerId=2,Text="Teacher private",UpdatedAt=fixture.Clock.Now,Version=1};
        db.Submissions.Add(teacher); await db.SaveChangesAsync();
        client.DefaultRequestHeaders.Remove("X-Test-User"); client.DefaultRequestHeaders.Add("X-Test-User","6");
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/v1/assignments/{id}/submissions/{teacher.Id}")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-User"); client.DefaultRequestHeaders.Add("X-Test-User","2");
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PutAsJsonAsync($"/api/v1/assignments/{id}/my-submission",new SaveSubmission("Not a student",0))).StatusCode);
    }
    [Fact]
    public async Task Provider_failure_does_not_fall_back_to_all_students()
    {
        var(id,sid)=await Seed();var scope=new Scope(id,1,id){Fail=true};
        using var host=Host(scope);using var client=host.CreateClient();client.DefaultRequestHeaders.Add("X-Test-User","6");
        foreach(var path in new[]{ $"/api/v1/assignments/{id}/submissions",$"/api/v1/assignments/{id}/submissions/{sid}"}) {
            var response=await client.GetAsync(path);Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);
            Assert.DoesNotContain("Private learner 3",await response.Content.ReadAsStringAsync());
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Missing_roster_or_module_capability_never_reveals_submission(bool missingModuleCapability)
    {
        var(id,sid)=await Seed();
        var scope=new Scope(missingModuleCapability ? id+999 : id,1,id){EmptyRoster=!missingModuleCapability};
        using var host=Host(scope);using var client=host.CreateClient();client.DefaultRequestHeaders.Add("X-Test-User","6");
        var list=await client.GetAsync($"/api/v1/assignments/{id}/submissions");
        if(missingModuleCapability) Assert.Equal(HttpStatusCode.NotFound,list.StatusCode);
        else Assert.Empty((await list.Content.ReadFromJsonAsync<SubmissionPage>())!.Items);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/v1/assignments/{id}/submissions/{sid}")).StatusCode);
    }    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Host(Scope scope)
        => fixture.WithWebHostBuilder(builder=>builder.ConfigureServices(services=>{
            services.RemoveAll<IAssignmentLearnerQuery>();services.AddSingleton<IAssignmentLearnerQuery>(scope);
            services.RemoveAll<ISubmissionAccessPolicy>();services.AddSingleton<ISubmissionAccessPolicy>(scope);
            services.RemoveAll<IEnrolmentQuery>();services.AddSingleton<IEnrolmentQuery>(scope);
            services.RemoveAll<IAccessPolicy>();services.AddSingleton<IAccessPolicy>(scope);
        }));
    private async Task<(int,int)> Seed()
    {
        using var teacher=fixture.Client(2);
        var response=await teacher.PostAsJsonAsync("/api/v1/courses/1/assignments",new CreateAssignment(1,"Scope test","",null,null,null,true,["text"]));
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);var a=(await response.Content.ReadFromJsonAsync<Assignment>())!;
        using var student=fixture.Client(3);
        var saved=await student.PutAsJsonAsync($"/api/v1/assignments/{a.Id}/my-submission",new SaveSubmission("Private learner 3",0));
        Assert.Equal(HttpStatusCode.Created,saved.StatusCode);
        using var dbScope=fixture.Services.CreateScope();
        var sid=await dbScope.ServiceProvider.GetRequiredService<MoodleDbContext>().Submissions.Where(x=>x.AssignmentId==a.Id).Select(x=>x.Id).SingleAsync();
        return(a.Id,sid);
    }
    private sealed class Scope(int visibleAssignment,int allowedCourse,int allowedAssignment) : IAssignmentLearnerQuery,ISubmissionAccessPolicy,IEnrolmentQuery,IAccessPolicy
    {
        public bool Fail {get;init;}
        public bool EmptyRoster {get;init;}
        public Task<IReadOnlyList<int>> ListRequiredLearnerIdsAsync(int courseId,CancellationToken ct=default)
            => Task.FromResult<IReadOnlyList<int>>(courseId==1 && !EmptyRoster ? [3,4,5,3] : []);
        public Task<bool> CanAssessLearnerAsync(int evaluatorId,int courseId,int assignmentId,int learnerId,CancellationToken ct=default)
        {
            if(Fail) throw new InvalidOperationException("Synthetic unavailable scope");
            return Task.FromResult(courseId==allowedCourse && assignmentId==allowedAssignment
                && (evaluatorId==2 || evaluatorId==6 && learnerId==3 || evaluatorId==7 && learnerId==4));
        }
        public Task<bool> IsActivelyEnrolledAsync(int userId,int courseId,CancellationToken ct=default)
            => Task.FromResult(courseId==1 && userId is 2 or 3 or 4 or 8);
        public Task<IReadOnlyList<int>> ListEnrolledUserIdsAsync(int courseId,CancellationToken ct=default)
            => Task.FromResult<IReadOnlyList<int>>([2,3,4,5,8]);
        public Task<bool> HasCapabilityAsync(int userId,string capability,ContextRef context,CancellationToken ct=default)
            => Task.FromResult(context==ContextRef.ForModule(visibleAssignment) && userId is 2 or 3 or 6 or 7 or 8);
    }
}
