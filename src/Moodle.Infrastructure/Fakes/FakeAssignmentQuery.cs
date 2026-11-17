using Moodle.Application.Abstractions;

namespace Moodle.Infrastructure.Fakes;

/// <summary>Sahte gerçekleme: tek ödev, kimse göndermemiş. Aslı Ceren gerçek <c>IAssignmentQuery</c>'yi bağlayınca kaldırır.</summary>
public sealed class FakeAssignmentQuery : IAssignmentQuery
{
    private static readonly AssignmentInfo Sample = new(1, 1, "Örnek ödev", null, null, null, false);

    public Task<AssignmentInfo?> FindAsync(int assignmentId, CancellationToken ct = default)
        => Task.FromResult<AssignmentInfo?>(assignmentId == Sample.Id ? Sample : null);

    public Task<bool> HasSubmittedAsync(int assignmentId, int learnerId, CancellationToken ct = default)
        => Task.FromResult(false);
}
