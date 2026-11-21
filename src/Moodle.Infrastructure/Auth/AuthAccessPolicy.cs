using Microsoft.AspNetCore.Identity;
using Moodle.Application.Abstractions;
using Moodle.Application.Auth;

namespace Moodle.Infrastructure.Auth;

/// <summary>
/// Gerçek <see cref="IAccessPolicy"/> gerçeklemesi (AUTH-FR-02, 03, 07). Sistem geneli rolleri (ASP.NET
/// Identity rolleri) ve — bağlam bir dersse — kullanıcının o dersteki etkin rolünü (CRS'nin
/// <see cref="IEnrolmentQuery"/> arayüzü üzerinden) toplar; kararı saf <see cref="CapabilityRules"/> verir.
/// Askıya alınmış/silinmiş kullanıcı hiçbir yeteneğe sahip değildir (AUTH-BR-02). Zorunlu parola
/// değişikliği bekleyen kullanıcı da değişikliği tamamlayana kadar hiçbir yeteneğe sahip değildir (AUTH-FR-09, BR-06).
/// </summary>
public sealed class AuthAccessPolicy(UserManager<AppUser> users, IEnrolmentQuery enrolments) : IAccessPolicy
{
    public async Task<bool> HasCapabilityAsync(int userId, string capability, ContextRef context, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || user.Suspended || user.Deleted || user.RequirePasswordChange) return false;

        var systemRoles = (await users.GetRolesAsync(user)).ToHashSet();

        string? courseRole = context.Level == ContextLevel.Course
            ? await enrolments.FindActiveRoleAsync(userId, context.InstanceId, ct)
            : null;

        return CapabilityRules.Evaluate(capability, systemRoles, courseRole);
    }
}
