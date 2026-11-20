using System.Text.Json.Serialization;
using Moodle.Domain.Assignments;
namespace Moodle.Application.Assignments;
public sealed record CreateAssignment(int SectionId, string Title, string? Instructions,
    DateTimeOffset? OpensAt, DateTimeOffset? DueAt, DateTimeOffset? CutoffAt,
    bool RequiresConfirmation, string[] EnabledSubmissionTypes);
public sealed record SaveSubmission(string Text, [property: JsonRequired] long Version, long? FileDraftId = null);
public sealed record SubmitSubmission([property: JsonRequired] long Version);
public sealed record AssignmentPermissions(bool CanSubmit, bool CanGrade, bool CanSave = false, bool CanSend = false, string? UnavailableReason = null);
public sealed record AssignmentView(Assignment Assignment, AssignmentPermissions Permissions);
public sealed record MySubmission(string Status, Submission? Submission);
public sealed record SubmissionRow(int LearnerId, string Status, int? SubmissionId,
    DateTimeOffset? SubmittedAt, DateTimeOffset? UpdatedAt, long Version, bool IsLate, int FileCount = 0);
public sealed record SubmissionPage(IReadOnlyList<SubmissionRow> Items, int? NextCursor);
public sealed record SaveResult(Submission Submission, bool Created);
/// <summary>ASG persistence boundary; rules are rechecked inside the write transaction.</summary>
public interface IAssignmentStore
{
    Task<Assignment?> FindAsync(int id, CancellationToken ct);
    Task AddAsync(Assignment assignment, CancellationToken ct);
    Task<IReadOnlyList<Assignment>> ListCourseAsync(int courseId,CancellationToken ct) => throw new NotSupportedException();
    Task<Submission?> FindSubmissionAsync(int assignmentId, int learnerId, CancellationToken ct);
    Task<Submission?> FindSubmissionByIdAsync(int assignmentId, int submissionId, CancellationToken ct);
    Task<SubmissionRule?> FindRuleAsync(int assignmentId, int learnerId, CancellationToken ct);
    Task<SubmissionPage> ListRowsAsync(int assignmentId, IReadOnlyList<int> learners, string? status, int? cursor, int limit, DateTimeOffset? dueAt, CancellationToken ct);
    void AddSubmission(Submission submission);
    Task<int> AttachFilesAsync(int userId, long draftId, Submission submission, CancellationToken ct)
        => throw new NotSupportedException("File commits require the relational ASG store.");
    Task<T> WriteAsync<T>(int assignmentId, Func<Task<T>> action, CancellationToken ct);
}
