namespace Moodle.Domain.Assignments;
/// <summary>ASG-BR-02: per-learner extension and editing lock.</summary>
public sealed class SubmissionRule
{
    public int AssignmentId { get; set; }
    public int LearnerId { get; set; }
    public DateTimeOffset? ExtendedUntil { get; set; }
    public bool Locked { get; set; }
}
