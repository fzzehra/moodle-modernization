namespace Moodle.Domain.Assignments;
/// <summary>ASG-FR-01, WBS-2 §3.4.1. Timestamps are UTC.</summary>
public sealed class Assignment
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int SectionId { get; set; }
    public string Title { get; set; } = "";
    public string Instructions { get; set; } = "";
    public DateTimeOffset? OpensAt { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? CutoffAt { get; set; }
    public bool RequiresConfirmation { get; set; }
    public string[] EnabledSubmissionTypes { get; set; } = ["text"];
    public long Version { get; set; } = 1;
}
