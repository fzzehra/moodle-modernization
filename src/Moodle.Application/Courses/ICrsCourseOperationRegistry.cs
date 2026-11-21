namespace Moodle.Application.Courses;

public enum CrsCourseOperationKind { Backup = 1, Restore = 2 }

/// <summary>
/// CRS-owned deletion protection for pending/running backup and restore work.
/// Producers must commit registration before queueing/starting real work and release
/// it only once that work has actually ended (including cancellation/failure).
/// No expiry, queue, worker, or external-module status inference is provided.
/// </summary>
public interface ICrsCourseOperationRegistry
{
    Task<Guid> RegisterPendingAsync(int courseId, CrsCourseOperationKind kind, CancellationToken ct = default);
    Task MarkRunningAsync(int courseId, Guid operationId, CancellationToken ct = default);
    /// <summary>Releases an ended operation; a missing token is an idempotent no-op for an existing course.</summary>
    Task ReleaseAsync(int courseId, Guid operationId, CancellationToken ct = default);
}
