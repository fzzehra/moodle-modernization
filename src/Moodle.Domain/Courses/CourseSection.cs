namespace Moodle.Domain.Courses;

/// <summary>Minimal persistent course section used by ASG-FR-01; not a group or enrolment scope.</summary>
public sealed class CourseSection
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string Name { get; set; } = "";
}
