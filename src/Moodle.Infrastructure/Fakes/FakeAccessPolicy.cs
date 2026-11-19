using Moodle.Application.Abstractions;

namespace Moodle.Infrastructure.Fakes;

/// <summary>
/// Geliştirici kolaylığı (bkz. <see cref="FakeCurrentUser"/>): yalnızca yönetici (kimlik 2) her yeteneğe
/// sahiptir. Gerçek gerçekleme <c>Moodle.Infrastructure.Auth.AuthAccessPolicy</c>'dir.
/// </summary>
public sealed class FakeAccessPolicy : IAccessPolicy
{
    private const int AdminUserId = 2;

    public Task<bool> HasCapabilityAsync(int userId, string capability, ContextRef context, CancellationToken ct = default)
        => Task.FromResult(userId == AdminUserId);
}
