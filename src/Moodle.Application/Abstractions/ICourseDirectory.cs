namespace Moodle.Application.Abstractions;

public sealed record CourseInfo(
    int Id,
    int CategoryId,
    string FullName,
    string ShortName,
    bool Visible,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate);

/// <summary>Ders bilgisini diğer modüllere sunar. Gerçek gerçekleme CRS modülündedir (Sinem).</summary>
public interface ICourseDirectory
{
    /// <returns>Ders yoksa <c>null</c>.</returns>
    Task<CourseInfo?> FindAsync(int courseId, CancellationToken ct = default);
}
