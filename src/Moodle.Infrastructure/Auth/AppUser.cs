using Microsoft.AspNetCore.Identity;

namespace Moodle.Infrastructure.Auth;

/// <summary>
/// ASP.NET Core Identity kullanıcısı (WBS-2 §3.1.2 <c>AspNetUsers</c>). Parola özeti, kilitlenme sayacı
/// ve güvenlik damgası Identity'nin kendi alanlarıdır (WBS-2 §3.1.2 kural 5, 6, 8). Moodle'daki
/// <c>user.suspended</c>/<c>user.deleted</c> karşılığı ve zorunlu parola değişikliği buraya eklenmiştir.
/// Bu tür bilerek Infrastructure'da tutulur (Domain hiçbir çerçeveye bağlı olmamalıdır, WBS-2 §2.2).
/// </summary>
public sealed class AppUser : IdentityUser<int>
{
    public bool Suspended { get; set; }
    public bool Deleted { get; set; }
    public bool RequirePasswordChange { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
}
