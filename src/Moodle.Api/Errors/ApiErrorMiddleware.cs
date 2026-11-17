using System.Text.Json;
using Moodle.Application.Errors;

namespace Moodle.Api.Errors;

/// <summary>
/// Tek tip hata gövdesi: <c>{ "error": { "code": "...", "message": "..." } }</c> (WBS-2 §3.6.4).
/// Beklenmeyen hatada ayrıntı ve yığın izi istemciye dönmez, yalnızca günlüğe yazılır (K3).
/// Yalnızca <c>/api</c> yolları için çalışır; Blazor sayfaları kendi hata sayfasını kullanır.
/// </summary>
public sealed class ApiErrorMiddleware(RequestDelegate next, ILogger<ApiErrorMiddleware> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            await WriteAsync(context, ex.StatusCode, ex.Code, ex.Message);
            return;
        }
        catch (BadHttpRequestException ex)
        {
            await WriteAsync(context, ex.StatusCode, CodeFor(ex.StatusCode), MessageFor(ex.StatusCode));
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "İstek işlenirken beklenmeyen hata: {Path}", context.Request.Path);
            await WriteAsync(context, StatusCodes.Status500InternalServerError, "internal_error",
                "Beklenmeyen bir hata oluştu.");
            return;
        }

        // Gövdesiz hata yanıtları (404, 401, 403, 405...) da aynı biçimi alır.
        if (context.Response.StatusCode >= 400 && !context.Response.HasStarted
            && context.Response.ContentLength is null && context.Response.ContentType is null)
        {
            await WriteAsync(context, context.Response.StatusCode, CodeFor(context.Response.StatusCode),
                MessageFor(context.Response.StatusCode));
        }
    }

    private static async Task WriteAsync(HttpContext context, int status, string code, string message)
    {
        if (context.Response.HasStarted) return;
        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = new { code, message } }, Json));
    }

    private static string CodeFor(int status) => status switch
    {
        400 => "bad_request",
        401 => "not_authenticated",
        403 => "forbidden",
        404 => "not_found",
        405 => "method_not_allowed",
        409 => "conflict",
        415 => "unsupported_media_type",
        422 => "validation_failed",
        _ => status >= 500 ? "internal_error" : "error",
    };

    private static string MessageFor(int status) => status switch
    {
        401 => "Oturum açmanız gerekiyor.",
        403 => "Bu işlem için yetkiniz yok.",
        404 => "Kaynak bulunamadı.",
        _ => "İstek işlenemedi.",
    };
}
