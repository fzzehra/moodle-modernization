using Microsoft.AspNetCore.Antiforgery;
using Moodle.Application.Errors;
namespace Moodle.Api;

/// <summary>Cookie-authenticated ASG/FILE writes require an antiforgery token. Header-only test identities have no cookie session.</summary>
public sealed class CookieWriteProtection(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http=context.HttpContext;
        if(http.User.Identity?.IsAuthenticated==true && http.Request.Method is not ("GET" or "HEAD" or "OPTIONS")) {
            try { await antiforgery.ValidateRequestAsync(http); }
            catch(AntiforgeryValidationException) { throw new AppException("invalid_csrf","İstek doğrulaması başarısız.",400); }
        }
        return await next(context);
    }
}
