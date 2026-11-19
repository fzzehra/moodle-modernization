using Moodle.Application.Abstractions;

namespace Moodle.Infrastructure.Fakes;

/// <summary>Sahte gerçekleme: SENG101'e 3 ile 5 arası kullanıcılar kayıtlı. Sinem gerçek <c>IEnrolmentQuery</c>'yi bağlayınca kaldırır.</summary>
public sealed class FakeEnrolmentQuery : IEnrolmentQuery
{
    private static readonly int[] Enrolled = [3, 4, 5];

    public Task<bool> IsActivelyEnrolledAsync(int userId, int courseId, CancellationToken ct = default)
        => Task.FromResult(courseId == 1 && Enrolled.Contains(userId));

    public Task<IReadOnlyList<int>> ListEnrolledUserIdsAsync(int courseId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<int>>(courseId == 1 ? Enrolled : []);

    public Task<string?> FindActiveRoleAsync(int userId, int courseId, CancellationToken ct = default)
        => Task.FromResult(courseId == 1 && Enrolled.Contains(userId) ? "Student" : null);
}
