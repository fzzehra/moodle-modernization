using System.Net;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Api.Auth;
using Moodle.Application.Abstractions;
using Moodle.Infrastructure.Auth;
using Moodle.Web.Auth;
using Moodle.Web.Pages;

namespace Moodle.UnitTests;

// YZ destekli (Claude Code): AUTH-FR-09 / AUTH-BR-06 zorunlu parola değişikliği birim testleri.
public sealed class AuthFr09ForcedPasswordTests
{
    [Theory]
    [InlineData("/api/v1/courses", true)]
    [InlineData("/api/v1/courses/1/enrolments", true)]
    [InlineData("/api/v1/users/me/quota", true)]
    [InlineData("/api/v1/admin/file-settings", true)]
    [InlineData("/api/v1/assignments/1", true)]
    [InlineData("/api/v1/auth/login", false)]
    [InlineData("/api/v1/auth/session", false)]
    [InlineData("/api/v1/auth/logout", false)]
    [InlineData("/api/v1/auth/change-password", false)]
    [InlineData("/api/v1/health", false)]
    [InlineData("/api/v1/openapi.json", false)]
    [InlineData("/logout", false)]
    [InlineData("/change-password", false)]
    [InlineData("/courses", false)]
    [InlineData("/api/v1/authors", true)] // Segment match, not a string prefix.
    public void AUTH_FR_09_Only_non_auth_API_operations_are_guarded(string path, bool guarded) =>
        Assert.Equal(guarded, PasswordChangeRequiredMiddleware.IsGuarded(path));

    [Fact]
    public async Task AUTH_FR_09_Development_or_test_identities_are_not_looked_up_in_AUTH()
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        context.Request.Path = "/api/v1/courses";
        var called = false;
        var middleware = new PasswordChangeRequiredMiddleware(_ => { called = true; return Task.CompletedTask; });

        // No AuthService is registered: resolving it would throw, proving no AUTH lookup happens.
        await middleware.InvokeAsync(context, new StubUser());

        Assert.True(called);
    }

    [Fact]
    public async Task AUTH_FR_09_Anonymous_requests_pass_to_the_endpoints_own_authentication()
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        context.Request.Path = "/api/v1/courses";
        var called = false;
        var middleware = new PasswordChangeRequiredMiddleware(_ => { called = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, new AuthCurrentUser(new HttpContextAccessor { HttpContext = context }));

        Assert.True(called);
    }

    [Theory]
    [InlineData(true, "/change-password?required=true")]
    [InlineData(false, "/")]
    public async Task AUTH_FR_09_Login_redirects_to_password_change_only_when_required(bool required, string expected)
    {
        var handler = Handler(_ => CrsWebTestSupport.Json(new { authenticated = true, requiresPasswordChange = required }));
        using var context = Context(handler);
        var page = context.Render<Login>();

        page.Find("#username").Change("ogrenci10");
        page.Find("#password").Change("ogrenci10");
        await page.Find("form").SubmitAsync(EventArgs.Empty);

        var navigation = (BunitNavigationManager)context.Services.GetRequiredService<NavigationManager>();
        page.WaitForAssertion(() => Assert.EndsWith(expected, navigation.History.First().Uri));
        Assert.True(navigation.History.First().Options.ForceLoad);
        Assert.Equal(("POST", "/api/v1/auth/login"), (handler.Requests.Single().Method, handler.Requests.Single().Path));
    }

    [Fact]
    public async Task AUTH_FR_09_Required_change_uses_existing_endpoint_and_returns_to_normal_flow()
    {
        var handler = Handler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var context = Context(handler);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("change-password?required=true");
        var page = context.Render<ChangePassword>();
        Assert.Contains("Devam etmek için parolanızı değiştirmeniz gerekiyor.", page.Find("[role=status]").TextContent);

        page.Find("#current-password").Change("eskiParola1");
        page.Find("#new-password").Change("yeniParola1");
        page.Find("#confirm-password").Change("yeniParola1");
        await page.Find("form").SubmitAsync(EventArgs.Empty);

        var navigation = (BunitNavigationManager)context.Services.GetRequiredService<NavigationManager>();
        page.WaitForAssertion(() => Assert.EndsWith("/", navigation.History.First().Uri));
        Assert.True(navigation.History.First().Options.ForceLoad);
        var request = handler.Requests.Single();
        Assert.Equal(("POST", "/api/v1/auth/change-password"), (request.Method, request.Path));
        Assert.Contains("\"currentPassword\":\"eskiParola1\"", request.Body);
        Assert.Contains("\"newPassword\":\"yeniParola1\"", request.Body);
    }

    [Fact]
    public async Task AUTH_FR_09_Mismatched_confirmation_does_not_call_the_API()
    {
        var handler = Handler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var context = Context(handler);
        var page = context.Render<ChangePassword>();

        page.Find("#current-password").Change("eskiParola1");
        page.Find("#new-password").Change("yeniParola1");
        page.Find("#confirm-password").Change("baskaParola1");
        await page.Find("form").SubmitAsync(EventArgs.Empty);

        Assert.Contains("Yeni parolalar eşleşmiyor.", page.Markup);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AUTH_FR_09_Wrong_current_password_keeps_user_on_the_page_with_message()
    {
        var handler = Handler(_ => CrsWebTestSupport.Json(
            new { error = new { code = "current_password_invalid", message = "Mevcut parola hatalı." } }, HttpStatusCode.BadRequest));
        using var context = Context(handler);
        var page = context.Render<ChangePassword>();

        page.Find("#current-password").Change("yanlis");
        page.Find("#new-password").Change("yeniParola1");
        page.Find("#confirm-password").Change("yeniParola1");
        await page.Find("form").SubmitAsync(EventArgs.Empty);

        page.WaitForAssertion(() => Assert.Contains("Mevcut parola hatalı.", page.Find("[role=alert]").TextContent));
        var navigation = (BunitNavigationManager)context.Services.GetRequiredService<NavigationManager>();
        Assert.Empty(navigation.History);
    }

    private static CrsWebHandler Handler(Func<WebRequest, HttpResponseMessage> respond) =>
        new((request, _) => Task.FromResult(respond(request)));

    private static BunitContext Context(CrsWebHandler handler)
    {
        var context = new BunitContext();
        context.Services.AddSingleton(new AuthApiClient(new HttpClient(handler) { BaseAddress = new("http://localhost/") }));
        return context;
    }

    private sealed class StubUser : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public int? UserId => 42;
        public string? Username => "test";
    }
}
