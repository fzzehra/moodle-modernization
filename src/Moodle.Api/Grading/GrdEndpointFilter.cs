using Microsoft.EntityFrameworkCore;
using Moodle.Application.Errors;
using Moodle.Application.Grading;
using Npgsql;

namespace Moodle.Api.Grading;

/// <summary>GRD istisnalarını tek tip hata gövdesine çevirir (CrsEndpointFilter deseni). Veritabanı ayrıntısı dönmez.</summary>
public sealed class GrdEndpointFilter(GrdAuthorization authorization) : IEndpointFilter
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
        catch (GrdConflictException ex)
        {
            throw AppException.Conflict(ex.Code, ex.Message);
        }
        catch (ArgumentException ex)
        {
            throw AppException.Validation("validation_failed", ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Okuma ile yazma arasında başka bir istek değerlendirmeyi veya defter notunu değiştirdi.
            throw AppException.Conflict(GrdConflictException.VersionConflict,
                GrdConflictException.Version().Message);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation })
        {
            // Aynı öğrenci ve deneme için eşzamanlı ilk kayıtlar.
            throw AppException.Conflict("conflict", "İşlem mevcut kayıtlarla çakışıyor.");
        }
    }
}
