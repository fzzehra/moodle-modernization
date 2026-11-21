namespace Moodle.Infrastructure.Assignments;

/// <summary>Only the ASG version-checked write transaction can mutate its permanent file area.</summary>
public sealed class AssignmentFileWriteScope
{
    internal int? SubmissionId { get; set; }
    internal int? UserId { get; set; }
    internal int? AssignmentId { get; set; }
}
