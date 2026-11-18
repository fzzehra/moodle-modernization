namespace Moodle.Application.Abstractions;

/// <summary>CRS: active students obligated to submit individual assignments in a course.
/// Return distinct positive IDs; unknown courses/scopes return an empty list, never all enrolled users.
/// Provider failures must propagate; callers must not broaden access as a fallback.</summary>
public interface IAssignmentLearnerQuery
{
    Task<IReadOnlyList<int>> ListRequiredLearnerIdsAsync(int courseId, CancellationToken ct = default);
}
