using Moodle.Application.Abstractions;
namespace Moodle.Infrastructure.Assignments;

/// <summary>Current AUTH models course-wide teachers, not groups. No inferred cross-course grants.</summary>
public sealed class AssignmentSubmissionAccessPolicy(IAccessPolicy policy, IAssignmentQuery assignments,
    IAssignmentLearnerQuery learners, IEnrolmentQuery enrolments) : ISubmissionAccessPolicy
{
    public async Task<bool> CanAssessLearnerAsync(int evaluatorId, int courseId, int assignmentId, int learnerId, CancellationToken ct = default)
        => (await assignments.FindAsync(assignmentId, ct))?.CourseId == courseId
            && await policy.HasCapabilityAsync(evaluatorId, "mod/assign:grade", ContextRef.ForModule(assignmentId), ct)
            && (await learners.ListRequiredLearnerIdsAsync(courseId, ct)).Contains(learnerId)
            && await enrolments.IsActivelyEnrolledAsync(learnerId, courseId, ct);
}
