using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moodle.Application.Courses;
using Npgsql;

namespace Moodle.Infrastructure.Courses;

// YZ destekli (Claude Code): CRS-FR-11 / CRS-NFR-06 arka planda ders silme.

/// <summary>
/// <c>Crs:BackgroundDeletion:Enabled</c> (varsayılan false), <c>:PollIntervalMilliseconds</c> (varsayılan 5000),
/// <c>:MaxAttempts</c> (beklenmeyen hatalar için toplam deneme, varsayılan 3).
/// </summary>
public sealed record CrsBackgroundDeletionOptions(bool Enabled, TimeSpan PollInterval, int MaxAttempts)
{
    public static CrsBackgroundDeletionOptions FromConfiguration(IConfiguration configuration)
    {
        var enabled = configuration.GetValue("Crs:BackgroundDeletion:Enabled", false);
        var interval = configuration.GetValue("Crs:BackgroundDeletion:PollIntervalMilliseconds", 5000);
        var attempts = configuration.GetValue("Crs:BackgroundDeletion:MaxAttempts", 3);
        if (interval < 10)
            throw new InvalidOperationException("Crs:BackgroundDeletion:PollIntervalMilliseconds en az 10 olmalıdır.");
        if (attempts < 1)
            throw new InvalidOperationException("Crs:BackgroundDeletion:MaxAttempts en az 1 olmalıdır.");
        return new(enabled, TimeSpan.FromMilliseconds(interval), attempts);
    }
}

public enum CrsCourseDeletionJobState { Pending = 1, Running = 2, Succeeded = 3, Failed = 4 }

/// <summary>Silme işinin durumu. Hata mesajı yalnızca sabit, güvenli metinlerden oluşur.</summary>
public sealed record CrsCourseDeletionJob(
    Guid Id, int CourseId, int? CategoryId, string State, int Attempts, int? RequestedByUserId,
    DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt,
    string? ErrorCode, string? ErrorMessage);

internal readonly record struct CrsCourseDeletionOutcome(CrsCourseDeletionJobState State, string Code, string Message);

internal static class CrsCourseDeletionRules
{
    internal const string OperationInProgress = "course_operation_in_progress";
    internal const string CourseNotFound = "course_not_found";
    internal const string Conflict = "course_deletion_conflict";
    internal const string DeletionFailed = "deletion_failed";

    /// <summary>
    /// NFR-03 engeli, çakışma ve bulunamayan ders kalıcı hatadır (hemen Failed, tekrar denenmez).
    /// Diğer hatalar <paramref name="maxAttempts"/> dolana kadar Pending'e döner, sonra Failed olur.
    /// Exception metni (SQL, bağlantı ayrıntısı içerebilir) asla kaydedilmez.
    /// </summary>
    internal static CrsCourseDeletionOutcome Classify(Exception ex, int attempts, int maxAttempts) => ex switch
    {
        CrsConflictException { Code: OperationInProgress } => new(CrsCourseDeletionJobState.Failed, OperationInProgress,
            "Bekleyen veya devam eden yedekleme/geri yükleme işlemi bulunduğu için ders silinemedi."),
        CrsConflictException => new(CrsCourseDeletionJobState.Failed, Conflict, "Ders silme işlemi mevcut kayıtlarla çakıştı."),
        KeyNotFoundException => new(CrsCourseDeletionJobState.Failed, CourseNotFound, "Silinecek ders bulunamadı."),
        _ when attempts < maxAttempts => new(CrsCourseDeletionJobState.Pending, DeletionFailed,
            "Ders silme işlemi beklenmeyen bir hata nedeniyle başarısız oldu; yeniden denenecek."),
        _ => new(CrsCourseDeletionJobState.Failed, DeletionFailed,
            "Ders silme işlemi beklenmeyen bir hata nedeniyle başarısız oldu."),
    };
}

/// <summary>Kalıcı silme kuyruğu. Kuyruğa alma, silme ile aynı ders satırı kilidi altında yapılır.</summary>
public sealed class CrsCourseDeletionJobs(CrsDbContext db)
{
    /// <summary>
    /// Ders yoksa <see cref="KeyNotFoundException"/>, NFR-03 engeli varsa <see cref="CrsConflictException"/>.
    /// Aynı ders için bekleyen/çalışan iş varsa yeni iş açmadan onu döndürür.
    /// </summary>
    public async Task<CrsCourseDeletionJob> EnqueueAsync(int courseId, int? requestedByUserId, CancellationToken ct = default)
    {
        CrsCourseOperationRules.ValidateCourseId(courseId);
        var operations = new CrsCourseOperationRegistry(db);
        try
        {
            var id = await operations.WithCourseLockAsync(courseId, async () =>
            {
                // Ön kontrol; asıl kontrol worker'da silme kilidi altında tekrarlanır.
                await operations.EnsureDeletionAllowedAsync(courseId, ct);
                var active = await FindActiveIdAsync(courseId, ct);
                if (active is Guid existing) return existing;
                var created = Guid.NewGuid();
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO crs_course_deletion_jobs ("Id", "CourseId", "CategoryId", "State", "RequestedByUserId")
                    SELECT {created}, c."Id", c."CategoryId", 1, {requestedByUserId}
                    FROM crs_courses c WHERE c."Id" = {courseId}
                    """, ct);
                return created;
            }, ct);
            return (await FindAsync(id, ct))!;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // Ders kilidi eşzamanlı kuyruğa almayı zaten sıralar; kısmi unique index son güvencedir.
            var existing = await FindActiveIdAsync(courseId, ct) ?? throw new InvalidOperationException(
                "Etkin silme işi bulunamadı.", ex);
            return (await FindAsync(existing, ct))!;
        }
    }

    public async Task<CrsCourseDeletionJob?> FindAsync(Guid id, CancellationToken ct = default)
    {
        var row = (await db.Database.SqlQuery<JobRow>($"""
            SELECT "Id", "CourseId", "CategoryId", "State", "Attempts", "RequestedByUserId",
                   "CreatedAt", "StartedAt", "CompletedAt", "ErrorCode", "ErrorMessage"
            FROM crs_course_deletion_jobs WHERE "Id" = {id}
            """).ToListAsync(ct)).SingleOrDefault();
        return row is null ? null : new(row.Id, row.CourseId, row.CategoryId,
            ((CrsCourseDeletionJobState)row.State).ToString(), row.Attempts, row.RequestedByUserId,
            row.CreatedAt, row.StartedAt, row.CompletedAt, row.ErrorCode, row.ErrorMessage);
    }

    private async Task<Guid?> FindActiveIdAsync(int courseId, CancellationToken ct) =>
        (await db.Database.SqlQuery<Guid>($"""
            SELECT "Id" AS "Value" FROM crs_course_deletion_jobs
            WHERE "CourseId" = {courseId} AND "State" IN (1, 2)
            """).ToListAsync(ct)).Select(x => (Guid?)x).SingleOrDefault();

    private sealed class JobRow
    {
        public Guid Id { get; set; }
        public int CourseId { get; set; }
        public int? CategoryId { get; set; }
        public int State { get; set; }
        public int Attempts { get; set; }
        public int? RequestedByUserId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
