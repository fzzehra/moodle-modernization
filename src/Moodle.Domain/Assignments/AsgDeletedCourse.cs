namespace Moodle.Domain.Assignments;

/// <summary>Durable ASG deletion receipt and fence against delayed writes for a deleted course.</summary>
public sealed class AsgDeletedCourse
{
    public int CourseId { get; set; }
    public long EventId { get; set; }
    public DateTimeOffset DeletedAt { get; set; }
}