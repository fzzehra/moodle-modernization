namespace Moodle.Api.Errors;
public sealed record ApiErrorResponse(ApiError Error);
public sealed record ApiError(string Code, string Message);
