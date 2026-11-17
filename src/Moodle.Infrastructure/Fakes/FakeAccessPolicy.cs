using Moodle.Application.Abstractions;

namespace Moodle.Infrastructure.Fakes;

/// <summary>Sahte gerçekleme: yalnızca yönetici (kimlik 2) her yeteneğe sahiptir. İrem gerçek politikayı bağlayınca kaldırır.</summary>
public sealed class FakeAccessPolicy : IAccessPolicy
{
    private const int AdminUserId = 2;

    public Task<bool> HasCapabilityAsync(int userId, string capability, ContextRef context, CancellationToken ct = default)
        => Task.FromResult(userId == AdminUserId);
}
