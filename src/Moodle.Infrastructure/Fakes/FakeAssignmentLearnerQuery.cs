using Moodle.Application.Abstractions;
namespace Moodle.Infrastructure.Fakes;

/// <summary>Explicit development fixture only; never infers students from all enrolments.</summary>
public sealed class FakeAssignmentLearnerQuery : IAssignmentLearnerQuery
{
    public Task<IReadOnlyList<int>> ListRequiredLearnerIdsAsync(int courseId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<int>>(courseId == 1 ? [3, 4, 5] : []);
}
