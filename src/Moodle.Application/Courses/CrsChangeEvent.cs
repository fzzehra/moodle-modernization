namespace Moodle.Application.Courses;

/// <summary>Versioned CRS notification. Contains identifiers and field names, never field values.</summary>
public sealed record CrsChangeEvent(
    long Id, string Kind, int CourseId, int? EnrolmentId, int? UserId,
    DateTimeOffset OccurredAt, int Version, string[] ChangedFields);

/// <summary>
/// Post-commit extension. Id must remain stable across deployments.
/// HandleAsync must be idempotent using (Id, notification.Id) as its deduplication key:
/// external effects cannot be atomically committed with the CRS delivery receipt.
/// Exceptions stop delivery; subsequent attempts retry the same notification.
/// </summary>
public interface ICrsEventExtension
{
    string Id { get; }
    int Order { get; }
    Task HandleAsync(CrsChangeEvent notification, CancellationToken ct = default);
}
