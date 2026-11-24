using Microsoft.AspNetCore.Identity;
using Moodle.Domain.Auth;

namespace Moodle.Infrastructure.Auth;

public enum LoginOutcome
{
    Success,
    InvalidCredentials,
    LockedOut,
}

public sealed record LoginResult(LoginOutcome Outcome, bool RequiresPasswordChange);

/// <summary>
/// AUTH-FR-01, 04, 05, 08, 09: giriş, çıkış, hesap durumu kontrolü, başarısız giriş kaydı ve parola
/// değişikliği. <see cref="SignInManager{TUser}"/>/<see cref="UserManager{TUser}"/> parola doğrulama,
/// kilitlenme sayacı ve oturum çerezini kendi içinde yönetir (WBS-2 §3.1.2 kural 5, 6, 8).
/// </summary>
public sealed class AuthService(
    SignInManager<AppUser> signInManager,
    UserManager<AppUser> userManager,
    AuthDbContext db,
    TimeProvider clock)
{
    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await userManager.FindByNameAsync(username);

        // AUTH-BR-02: silinmiş/askıya alınmış hesap, var olmayan kullanıcı veya yanlış parolayla aynı
        // genel mesajı alır; hangisinin doğru olduğu sızdırılmaz (gerçek Moodle'da da T-AUTH-04 aynı
        // genel mesajı gösterir, bkz. WBS-1 §4.1.10).
        if (user is null || user.Deleted || user.Suspended)
        {
            await LogAsync(user?.Id, username, SecurityEventType.LoginFailed, ct);
            return new LoginResult(LoginOutcome.InvalidCredentials, false);
        }

        var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            await LogAsync(user.Id, username, SecurityEventType.LoginRejectedLockedOut, ct);
            return new LoginResult(LoginOutcome.LockedOut, false);
        }

        if (!result.Succeeded)
        {
            await LogAsync(user.Id, username, SecurityEventType.LoginFailed, ct);
            return new LoginResult(LoginOutcome.InvalidCredentials, false);
        }

        await LogAsync(user.Id, username, SecurityEventType.LoginSucceeded, ct);
        return new LoginResult(LoginOutcome.Success, user.RequirePasswordChange);
    }

    public async Task LogoutAsync(int? userId, CancellationToken ct = default)
    {
        await signInManager.SignOutAsync();
        if (userId is > 0)
            await LogAsync(userId, "", SecurityEventType.LoggedOut, ct);
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded) return false;

        user.RequirePasswordChange = false;
        await userManager.UpdateAsync(user);
        // AUTH-NFR-03 (aday): ChangePasswordAsync güvenlik damgasını yeniledi. Parolayı değiştiren oturumun çerezi
        // yeni damgayla yeniden yazılır (kalıcılığı korunur); bu kullanıcının diğer çerezleri damga doğrulamasında reddedilir.
        await signInManager.RefreshSignInAsync(user);
        await LogAsync(userId, user.UserName ?? "", SecurityEventType.PasswordChanged, ct);
        return true;
    }

    /// <summary>
    /// AUTH-FR-09 / AUTH-BR-06: kullanıcı için zorunlu parola değişikliği bekliyor mu. Her istekte veritabanından
    /// okunur; böylece parola değişince (veya bayrak sonradan konunca) yeni oturum açmadan hemen etkili olur.
    /// </summary>
    public async Task<bool> RequiresPasswordChangeAsync(int userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user?.RequirePasswordChange == true;
    }

    private async Task LogAsync(int? userId, string attemptedUsername, SecurityEventType type, CancellationToken ct)
    {
        db.SecurityEvents.Add(new SecurityEvent
        {
            UserId = userId,
            AttemptedUsername = attemptedUsername,
            EventType = type,
            OccurredAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
    }
}
