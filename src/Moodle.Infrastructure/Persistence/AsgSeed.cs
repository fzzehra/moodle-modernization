using Microsoft.EntityFrameworkCore;
using Moodle.Application.Abstractions;
using Moodle.Domain.Assignments;
namespace Moodle.Infrastructure.Persistence;

/// <summary>ASG-only development fixture: confirmation on/off, submitted/draft/missing.
/// Uses existing CRS fake identities; never creates AUTH/CRS records or changes existing submissions.</summary>
public static class AsgSeed
{
    public static async Task ApplyAsync(MoodleDbContext db, ICourseDirectory courses,
        ICourseSectionQuery sections, IEnrolmentQuery enrolments, CancellationToken ct = default, bool allowSyntheticFixture = false)
    {
        if (!allowSyntheticFixture
            || courses is not Moodle.Infrastructure.Fakes.FakeCourseDirectory
            || sections is not Moodle.Infrastructure.Fakes.FakeCourseSectionQuery
            || enrolments is not Moodle.Infrastructure.Fakes.FakeEnrolmentQuery)
            throw new InvalidOperationException("ASG synthetic seed requires explicit opt-in and the known development fixture providers.");
        if (await courses.FindAsync(1, ct) is not { ShortName: "SENG101" }
            || !await sections.BelongsToCourseAsync(1, 1, ct) || !await sections.BelongsToCourseAsync(2, 1, ct))
            throw new InvalidOperationException("ASG fixture requires the SENG101 development course and sections 1/2.");
        int[] learners = [3, 4, 5]; // Fixed synthetic identities of FakeEnrolmentQuery.
        if (!(await enrolments.ListEnrolledUserIdsAsync(1, ct)).Order().SequenceEqual(learners))
            throw new InvalidOperationException("Unexpected development fixture identities.");
        if (learners.Length < 3) throw new InvalidOperationException("ASG fixture requires three existing enrolled test identities.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(731103)", ct);
        foreach (var confirmation in new[] { false, true })
        {
            var title = confirmation ? "SENG101 Ödev 2" : "SENG101 Ödev 1";
            if (await db.Assignments.AnyAsync(a => a.CourseId == 1 && a.Title == title, ct)) continue;
            var assignment = new Assignment { CourseId = 1, SectionId = confirmation ? 2 : 1,
                Title = title, Instructions = confirmation ? "Fonksiyonlar" : "Değişkenler ve döngüler",
                RequiresConfirmation = confirmation };
            db.Assignments.Add(assignment);
            await db.SaveChangesAsync(ct);
            var time = new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero);
            db.Submissions.Add(new Submission { AssignmentId = assignment.Id,
                LearnerId = confirmation ? learners[1] : learners[0], Status = confirmation ? "draft" : "submitted",
                Text = confirmation ? "Yarım kalmış taslak" : "Sentetik teslim metni", Version = 1,
                UpdatedAt = time, SubmittedAt = confirmation ? null : time });
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}
