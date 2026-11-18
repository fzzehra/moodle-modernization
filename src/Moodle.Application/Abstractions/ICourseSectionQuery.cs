namespace Moodle.Application.Abstractions;

/// <summary>ASG-FR-01: CRS section ownership (approved supplementary contract).</summary>
public interface ICourseSectionQuery
{
    Task<bool> BelongsToCourseAsync(int sectionId, int courseId, CancellationToken ct = default);
}
