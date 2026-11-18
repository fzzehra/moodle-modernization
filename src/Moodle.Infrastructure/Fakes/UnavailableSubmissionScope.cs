using Moodle.Application.Abstractions;
namespace Moodle.Infrastructure.Fakes;

/// <summary>No real CRS/AUTH integration: fail closed, including for a known administrator.</summary>
public sealed class UnavailableSubmissionScope : IAssignmentLearnerQuery, ISubmissionAccessPolicy
{
    public Task<IReadOnlyList<int>> ListRequiredLearnerIdsAsync(int courseId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<int>>([]);
    public Task<bool> CanAssessLearnerAsync(int evaluatorId, int courseId, int assignmentId,
        int learnerId, CancellationToken ct = default) => Task.FromResult(false);
}
