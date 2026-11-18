using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moodle.Application.Abstractions;
using Moodle.Application.Assignments;
using Moodle.Application.Errors;
using Moodle.Domain.Courses;
using Moodle.Infrastructure;
using Moodle.Infrastructure.Courses;
using Moodle.Infrastructure.Fakes;
using Moodle.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Moodle.IntegrationTests;

public class AsgCrsMergeTests
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Both_contexts_migrate_in_either_order_and_snapshots_match(bool asgFirst)
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17").Build();
        await postgres.StartAsync();
        await using var asg = new MoodleDbContext(new DbContextOptionsBuilder<MoodleDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        await using var crs = new CrsDbContext(new DbContextOptionsBuilder<CrsDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        if(asgFirst) { await asg.Database.MigrateAsync(); await crs.Database.MigrateAsync(); }
        else { await crs.Database.MigrateAsync(); await asg.Database.MigrateAsync(); }
        Assert.False(asg.Database.HasPendingModelChanges());
        Assert.False(crs.Database.HasPendingModelChanges());
        Assert.Empty(await asg.Database.GetPendingMigrationsAsync());
        Assert.Empty(await crs.Database.GetPendingMigrationsAsync());
        await asg.Database.MigrateAsync(); await crs.Database.MigrateAsync();
        Assert.Empty(await asg.Assignments.ToArrayAsync()); Assert.Empty(await crs.Courses.ToArrayAsync());
    }

    [Fact]
    public async Task Real_CRS_roster_and_ASG_scope_preserve_student_privacy()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17").Build();
        await postgres.StartAsync();
        var services = new ServiceCollection().AddInfrastructure();
        services.RemoveAll<DbContextOptions<CrsDbContext>>();
        services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<CrsDbContext>>();
        services.AddDbContext<CrsDbContext>(o=>o.UseNpgsql(postgres.GetConnectionString()));
        services.AddDbContext<MoodleDbContext>(o=>o.UseNpgsql(postgres.GetConnectionString()));
        services.AddScoped<ICurrentUser>(_=>new Evaluator());
        services.AddScoped<IAccessPolicy>(_=>new ModulePolicy());
        using var provider=services.BuildServiceProvider();
        using var scope=provider.CreateScope();
        var sp=scope.ServiceProvider;
        var crs=sp.GetRequiredService<CrsDbContext>(); var asg=sp.GetRequiredService<MoodleDbContext>();
        await crs.Database.MigrateAsync(); await asg.Database.MigrateAsync();
        var service=sp.GetRequiredService<CrsService>();
        var category=await service.SaveCategoryAsync(new(){Name="Merge fixture"});
        var course=await service.SaveCourseAsync(new(){CategoryId=category.Id,FullName="Real course",ShortName="MERGE"});
        var now=DateTimeOffset.UtcNow;
        foreach(var enrolment in new[]{
            new CourseEnrolment{UserId=3,Role="Student"},
            new CourseEnrolment{UserId=4,Role="Student"},
            new CourseEnrolment{UserId=2,Role="Teacher"},
            new CourseEnrolment{UserId=5,Role="Observer"},
            new CourseEnrolment{UserId=6,Role="Student",Suspended=true},
            new CourseEnrolment{UserId=7,Role="Student",StartsAt=now.AddDays(1)},
            new CourseEnrolment{UserId=8,Role="Student",EndsAt=now.AddDays(-1)}
        }) { enrolment.CourseId=course.Id; await service.EnrolAsync(enrolment); }
        var roster=sp.GetRequiredService<IAssignmentLearnerQuery>();
        Assert.Same(service,roster);
        Assert.Equal(new[]{3,4},await roster.ListRequiredLearnerIdsAsync(course.Id));
        Assert.Empty(await roster.ListRequiredLearnerIdsAsync(course.Id+100));
        Assert.Contains(2,await service.ListEnrolledUserIdsAsync(course.Id));
        var assignment=new Moodle.Domain.Assignments.Assignment{CourseId=course.Id,SectionId=1,Title="Existing ASG"};
        asg.Assignments.Add(assignment);await asg.SaveChangesAsync();
        var submission=new Moodle.Domain.Assignments.Submission{AssignmentId=assignment.Id,LearnerId=3,Text="Private",UpdatedAt=now,Version=1};
        asg.Submissions.Add(submission);await asg.SaveChangesAsync();
        var app=sp.GetRequiredService<AssignmentService>();
        // Default AUTH target scope must not inherit synthetic IDs even if courseId happens to be 1.
        Assert.Empty((await app.ListAsync(assignment.Id)).Items);
        Assert.Equal(404,(await Assert.ThrowsAsync<AppException>(()=>app.ReadAsync(assignment.Id,submission.Id))).StatusCode);
        Assert.False(await sp.GetRequiredService<ICourseSectionQuery>().BelongsToCourseAsync(1,course.Id));
        Assert.Equal("invalid_section",(await Assert.ThrowsAsync<AppException>(()=>app.CreateAsync(course.Id,new(1,"Rejected","",null,null,null,true,["text"])))).Code);
        // Explicit test-only AUTH grant: real CRS students still determine the candidate set.
        var allowed=new AssignmentService(sp.GetRequiredService<IAssignmentStore>(),new Evaluator(),new ModulePolicy(),
            service,sp.GetRequiredService<ICourseSectionQuery>(),service,TimeProvider.System,service,new TargetPolicy());
        Assert.Equal(new[]{3},(await allowed.ListAsync(assignment.Id)).Items.Select(x=>x.LearnerId));
        Assert.Equal("Private",(await allowed.ReadAsync(assignment.Id,submission.Id)).Text);
        await service.UpdateEnrolmentAsync((await service.ListEnrolmentsAsync(course.Id)).Single(x=>x.UserId==3).Id,true,"Student");
        Assert.Empty((await allowed.ListAsync(assignment.Id)).Items);
        Assert.Equal(404,(await Assert.ThrowsAsync<AppException>(()=>allowed.ReadAsync(assignment.Id,submission.Id))).StatusCode);
    }
    private sealed class Evaluator : ICurrentUser
    {
        public int? UserId=>2; public bool IsAuthenticated=>true; public string? Username=>"merge-test";
    }
    private sealed class ModulePolicy : IAccessPolicy
    {
        public Task<bool> HasCapabilityAsync(int userId,string capability,ContextRef context,CancellationToken ct=default)
            => Task.FromResult(userId==2);
    }
    private sealed class TargetPolicy : ISubmissionAccessPolicy
    {
        public Task<bool> CanAssessLearnerAsync(int evaluatorId,int courseId,int assignmentId,int learnerId,CancellationToken ct=default)
            =>Task.FromResult(evaluatorId==2 && learnerId==3);
    }
}
