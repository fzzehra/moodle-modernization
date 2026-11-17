namespace Moodle.Application.Abstractions;

public sealed record AssignmentInfo(
    int Id,
    int CourseId,
    string Title,
    DateTimeOffset? OpensAt,
    DateTimeOffset? DueAt,
    DateTimeOffset? CutoffAt,
    bool RequiresConfirmation);

/// <summary>Ödev bilgisini diğer modüllere (GRD) sunar. Gerçek gerçekleme ASG modülündedir (Aslı Ceren).</summary>
public interface IAssignmentQuery
{
    /// <returns>Ödev yoksa <c>null</c>.</returns>
    Task<AssignmentInfo?> FindAsync(int assignmentId, CancellationToken ct = default);

    /// <summary>Öğrenci ödevi göndermiş mi (taslak sayılmaz)?</summary>
    Task<bool> HasSubmittedAsync(int assignmentId, int learnerId, CancellationToken ct = default);
}
