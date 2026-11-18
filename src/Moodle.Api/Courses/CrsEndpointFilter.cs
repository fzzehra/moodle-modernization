using Microsoft.EntityFrameworkCore;
using Moodle.Application.Courses;
using Moodle.Application.Errors;
using Npgsql;

namespace Moodle.Api.Courses;

public sealed class CrsEndpointFilter(CrsAuthorization authorization) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        authorization.RequireIdentity();
        try
        {
            return await next(context);
        }
        catch (KeyNotFoundException)
        {
            throw AppException.NotFound();
        }
        catch (CrsConflictException ex)
        {
            throw AppException.Conflict(ex.Code, ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.InnerException is CrsConflictException)
        {
            var conflict = (CrsConflictException)ex.InnerException;
            throw AppException.Conflict(conflict.Code, conflict.Message);
        }
        catch (ArgumentException ex)
        {
            throw AppException.Validation("validation_failed", ex.Message);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation })
        {
            // Also handles concurrent writes which pass service pre-checks.
            throw AppException.Conflict("conflict", "İşlem mevcut kayıtlarla çakışıyor.");
        }
    }
}
