using Moodle.Application.Abstractions;
using Moodle.Application.Assignments;
namespace Moodle.Infrastructure.Persistence;
/// <summary>ASG-owned read contract for GRD; uses the same persisted ASG data.</summary>
public sealed class AssignmentQuery(IAssignmentStore store) : IAssignmentQuery
{
    public async Task<AssignmentInfo?> FindAsync(int assignmentId, CancellationToken ct = default)
    {
        var a = await store.FindAsync(assignmentId, ct);
        return a is null ? null : new(a.Id, a.CourseId, a.Title, a.OpensAt, a.DueAt, a.CutoffAt, a.RequiresConfirmation);
    }
    public async Task<bool> HasSubmittedAsync(int assignmentId, int learnerId, CancellationToken ct = default)
        => (await store.FindSubmissionAsync(assignmentId, learnerId, ct))?.Status == "submitted";
}
