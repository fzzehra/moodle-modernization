
namespace Moodle.Domain.Courses;

public sealed class CourseCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int? ParentId { get; set; }
}

public sealed class Course
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string FullName { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string? IdNumber { get; set; }
    public bool Visible { get; set; } = true;
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
}

public sealed class CourseEnrolment
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int UserId { get; set; }
    public bool Suspended { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public string Role { get; set; } = "Student";
}
