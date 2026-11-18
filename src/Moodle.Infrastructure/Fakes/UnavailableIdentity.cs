using Moodle.Application.Abstractions;
namespace Moodle.Infrastructure.Fakes;

/// <summary>Fail-closed defaults until AUTH is registered; never impersonates a user.</summary>
public sealed class UnavailableIdentity : ICurrentUser, IAccessPolicy
{
    public bool IsAuthenticated => false;
    public int? UserId => null;
    public string? Username => null;
    public Task<bool> HasCapabilityAsync(int userId, string capability, ContextRef context, CancellationToken ct = default)
        => Task.FromResult(false);
}
