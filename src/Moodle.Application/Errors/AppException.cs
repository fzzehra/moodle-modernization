namespace Moodle.Application.Errors;

/// <summary>
/// İş mantığının fırlattığı, istemciye gösterilebilir hata. API katmanı bunu tek tip hata gövdesine çevirir
/// (<c>{ "error": { "code", "message" } }</c>, WBS-2 §3.6.4). Yığın izi hiçbir zaman dönmez (K3).
/// </summary>
public class AppException(string code, string message, int statusCode = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;

    public static AppException NotFound(string message = "Kaynak bulunamadı.") => new("not_found", message, 404);
    public static AppException Validation(string code, string message) => new(code, message, 422);
    public static AppException Conflict(string code, string message) => new(code, message, 409);
}
