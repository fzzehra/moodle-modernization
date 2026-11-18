using Moodle.Application.Abstractions;
namespace Moodle.Infrastructure.Fakes;

/// <summary>CRS section model is not available. Unknown ownership must deny creation.</summary>
public sealed class UnavailableCourseSectionQuery : ICourseSectionQuery
{
    public Task<bool> BelongsToCourseAsync(int sectionId, int courseId, CancellationToken ct = default)
        => Task.FromResult(false);
}
