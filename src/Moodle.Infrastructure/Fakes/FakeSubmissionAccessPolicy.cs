using Moodle.Application.Abstractions;
using Moodle.Application.Assignments;
namespace Moodle.Infrastructure.Fakes;

/// <summary>Explicit fixture: evaluator 2, course 1, learners 3..5, existing matching assignment only.</summary>
public sealed class FakeSubmissionAccessPolicy(IAssignmentStore store) : ISubmissionAccessPolicy
{
    public async Task<bool> CanAssessLearnerAsync(int evaluatorId, int courseId, int assignmentId,
        int learnerId, CancellationToken ct = default)
        => evaluatorId == 2 && courseId == 1 && learnerId is >= 3 and <= 5
            && (await store.FindAsync(assignmentId, ct))?.CourseId == courseId;
}
