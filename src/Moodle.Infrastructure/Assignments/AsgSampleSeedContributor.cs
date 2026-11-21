using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moodle.Application.Abstractions;
using Moodle.Application.Seeding;
using Moodle.Domain.Assignments;
using Moodle.Infrastructure.Auth;
using Moodle.Infrastructure.Courses;
using Moodle.Infrastructure.Persistence;
namespace Moodle.Infrastructure.Assignments;

/// <summary>Explicit synthetic ASG fixture; cannot bypass missing real CRS section ownership.</summary>
public sealed class AsgSampleSeedContributor(MoodleDbContext db, CrsDbContext courses,
    UserManager<AppUser> users, ICourseSectionQuery sections, IConfiguration configuration, IHostEnvironment environment) : ISeedContributor
{
    public int Order=>40;
    public string Name=>"ASG: sentetik ödev ve teslim";
    public async Task SeedAsync(SeedDataset data,CancellationToken ct=default)
    {
        if(!configuration.GetValue<bool>("ASG_SEED_SAMPLE")) return;
        if(!(environment.IsDevelopment() || environment.IsEnvironment("Testing")))
            throw new InvalidOperationException("ASG sample data is restricted to Development/Testing.");
        var courseIds=new Dictionary<string,int>();var userIds=new Dictionary<string,int>();
        foreach(var seed in data.Users) {
            var user=await users.FindByNameAsync(seed.Username);
            if(user is null || user.Email!=seed.Email || !await users.CheckPasswordAsync(user,seed.Username))
                throw new InvalidOperationException("ASG sample data requires verified synthetic AUTH identities.");
            userIds[seed.Username]=user.Id;
        }
        foreach(var seed in data.Assignments) {
            var course=await courses.Courses.AsNoTracking().SingleOrDefaultAsync(c=>c.ShortName==seed.CourseShortName,ct)
                ??throw new InvalidOperationException("Sample CRS course missing.");
            if(!await sections.BelongsToCourseAsync(seed.Section,course.Id,ct))
                throw new InvalidOperationException("ASG sample seed blocked: real CRS section model/query is not available.");
            courseIds[seed.CourseShortName]=course.Id;
        }
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(731104)",ct);
        foreach(var seed in data.Assignments) {
            var courseId=courseIds[seed.CourseShortName];
            if(await db.Assignments.AnyAsync(a=>a.CourseId==courseId && a.Title==seed.Name,ct)) continue;
            var assignment=new Assignment{CourseId=courseId,SectionId=seed.Section,Title=seed.Name,
                Instructions=seed.Intro,RequiresConfirmation=seed.SubmissionDrafts};
            db.Assignments.Add(assignment);await db.SaveChangesAsync(ct);
            foreach(var item in data.Submissions.Where(s=>s.AssignmentName==seed.Name)) {
                var time=new DateTimeOffset(2026,11,19,0,0,0,TimeSpan.Zero);
                db.Submissions.Add(new(){AssignmentId=assignment.Id,LearnerId=userIds[item.Username],Text=item.OnlineText,
                    Version=1,Status=seed.SubmissionDrafts?"draft":"submitted",UpdatedAt=time,SubmittedAt=seed.SubmissionDrafts?null:time});
            }
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}
