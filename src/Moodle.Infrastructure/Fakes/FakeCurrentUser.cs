using Moodle.Application.Abstractions;

namespace Moodle.Infrastructure.Fakes;

/// <summary>
/// Geliştirici kolaylığı: <c>Development:UseFakeIdentity=true</c> ile açıkça istenirse her zaman oturum
/// açmış site yöneticisi gibi davranır (<see cref="DevelopmentConfiguration"/> yalnızca Development/Testing
/// ortamında izin verir). AUTH gerçek gerçeklemesi <c>Moodle.Infrastructure.Auth.AuthCurrentUser</c>'dır
/// ve varsayılan (fakes kapalı) durumda, Production dâhil her zaman o kullanılır.
/// </summary>
public sealed class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => true;
    public int? UserId => 2;
    public string? Username => "admin";
}
