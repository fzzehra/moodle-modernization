using Moodle.Application.Abstractions;

namespace Moodle.Infrastructure.Fakes;

/// <summary>AUTH bitene kadar kullanılan sahte gerçekleme: her zaman oturum açmış site yöneticisi. İrem, <c>ICurrentUser</c> gerçeğini bağlayınca kaldırır.</summary>
public sealed class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => true;
    public int? UserId => 2;
    public string? Username => "admin";
}
