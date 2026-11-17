using Moodle.Application.Abstractions;

namespace Moodle.Infrastructure.Fakes;

/// <summary>Sahte gerçekleme: tek dersli bellek içi liste (SENG101). Sinem gerçek <c>ICourseDirectory</c>'yi bağlayınca kaldırır.</summary>
public sealed class FakeCourseDirectory : ICourseDirectory
{
    private static readonly CourseInfo Seng101 = new(1, 1, "Yazılım Mühendisliğine Giriş", "SENG101", true, null, null);

    public Task<CourseInfo?> FindAsync(int courseId, CancellationToken ct = default)
        => Task.FromResult<CourseInfo?>(courseId == Seng101.Id ? Seng101 : null);
}
