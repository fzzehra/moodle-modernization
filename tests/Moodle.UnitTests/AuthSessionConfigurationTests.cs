using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moodle.Infrastructure;

namespace Moodle.UnitTests;

// YZ destekli (Claude Code): AUTH-NFR-03 (aday) oturum kalıcılığı ve güvenlik damgası yapılandırması.
public sealed class AuthSessionConfigurationTests
{
    private static ServiceProvider Provider(string? keysPath = null)
    {
        var settings = new Dictionary<string, string?>();
        if (keysPath is not null) settings["DataProtection:KeysPath"] = keysPath;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AUTH_NFR_03_Session_cookie_keeps_14_day_sliding_persistence_and_validates_security_stamp()
    {
        using var provider = Provider();
        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.Equal(TimeSpan.FromDays(14), options.ExpireTimeSpan);
        Assert.True(options.SlidingExpiration);
        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Lax, options.Cookie.SameSite);
        Assert.Equal<Func<CookieValidatePrincipalContext, Task>>(
            SecurityStampValidator.ValidatePrincipalAsync, options.Events.OnValidatePrincipal);
    }

    [Fact]
    public async Task AUTH_NFR_03_Schemes_used_when_a_stale_cookie_is_rejected_are_registered()
    {
        // SecurityStampValidator signs out of the application and TwoFactorRememberMe schemes on rejection.
        using var provider = Provider();
        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        Assert.NotNull(await schemes.GetSchemeAsync(IdentityConstants.ApplicationScheme));
        Assert.NotNull(await schemes.GetSchemeAsync(IdentityConstants.TwoFactorRememberMeScheme));
        Assert.Equal(IdentityConstants.ApplicationScheme, (await schemes.GetDefaultAuthenticateSchemeAsync())?.Name);
    }

    [Fact]
    public void AUTH_NFR_03_Security_stamp_is_revalidated_every_30_minutes()
    {
        using var provider = Provider();
        Assert.Equal(TimeSpan.FromMinutes(30),
            provider.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value.ValidationInterval);
        Assert.Equal(TimeSpan.FromMinutes(30), DependencyInjection.SecurityStampValidationInterval);
    }

    [Fact]
    public void AUTH_NFR_03_Configured_key_directory_is_used_for_data_protection_keys()
    {
        var path = Path.Combine(Path.GetTempPath(), "moodle-dp-unit-" + Guid.NewGuid().ToString("N"));
        using var provider = Provider(path);

        var repository = Assert.IsType<FileSystemXmlRepository>(
            provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository);
        Assert.Equal(Path.GetFullPath(path), repository.Directory.FullName);
    }
}
