namespace Moodle.Domain.Assignments;
/// <summary>ASG-FR-02, ASG-BR-01. First-version individual submission, attempt zero.</summary>
public sealed class Submission
{
    public int Id { get; set; }
    public int AssignmentId { get; set; }
    public int LearnerId { get; set; }
    public int FileCount { get; set; }
    public int Attempt { get; set; }
    public string Status { get; set; } = "draft";
    public string Text { get; set; } = "";
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; }
}
