using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moodle.Application.Abstractions;

namespace Moodle.Infrastructure.Auth;

/// <summary>
/// Gerçek <see cref="ICurrentUser"/> gerçeklemesi (AUTH-FR-08). Kimlik her zaman oturum çerezinden
/// (<see cref="HttpContext.User"/>) okunur, istekten (gövde/sorgu) asla alınmaz — WBS-2 §3.1.3 kural 7.
/// </summary>
public sealed class AuthCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public int? UserId =>
        IsAuthenticated && int.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    public string? Username => IsAuthenticated ? Principal?.Identity?.Name : null;
}
