namespace Moodle.Application.Abstractions;

/// <summary>AUTH: target-learner assessment scope, in addition to module grade capability.
/// Verify the evaluator, course, ASG assignment and learner relationship. Unknown scope denies.
/// assignmentId is an ASG domain ID, not an AUTH context-table primary key.</summary>
public interface ISubmissionAccessPolicy
{
    Task<bool> CanAssessLearnerAsync(int evaluatorId, int courseId, int assignmentId,
        int learnerId, CancellationToken ct = default);
}
