using Moodle.Application.Abstractions;
namespace Moodle.Infrastructure.Fakes;
/// <summary>Temporary SENG101 sections 1–4; CRS replaces this registration.</summary>
public sealed class FakeCourseSectionQuery : ICourseSectionQuery
{
    public Task<bool> BelongsToCourseAsync(int sectionId, int courseId, CancellationToken ct = default)
        => Task.FromResult(courseId == 1 && sectionId is >= 1 and <= 4);
}
