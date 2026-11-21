using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Moodle.Application.Courses;

namespace Moodle.Infrastructure.Courses;

public sealed class CrsCourseOperationRegistry(CrsDbContext db) : ICrsCourseOperationRegistry
{
    public Task<Guid> RegisterPendingAsync(int courseId, CrsCourseOperationKind kind, CancellationToken ct = default)
    {
        CrsCourseOperationRules.ValidateCourseId(courseId);
        CrsCourseOperationRules.ValidateKind(kind);
        return WithCourseLockAsync(courseId, async () =>
        {
            var id = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO crs_course_operations ("Id", "CourseId", "Kind", "State")
                VALUES ({id}, {courseId}, {(int)kind}, 1)
                """, ct);
            return id;
        }, ct);
    }

    public async Task MarkRunningAsync(int courseId, Guid operationId, CancellationToken ct = default)
    {
        CrsCourseOperationRules.ValidateCourseId(courseId);
        CrsCourseOperationRules.ValidateToken(operationId);
        await WithCourseLockAsync(courseId, async () =>
        {
            var state = await FindStateAsync(courseId, operationId, ct)
                ?? throw new KeyNotFoundException("Ders işlemi bulunamadı.");
            if (state == 1)
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE crs_course_operations SET "State" = 2
                    WHERE "Id" = {operationId} AND "CourseId" = {courseId}
                    """, ct);
            return true;
        }, ct);
    }

    public async Task ReleaseAsync(int courseId, Guid operationId, CancellationToken ct = default)
    {
        CrsCourseOperationRules.ValidateCourseId(courseId);
        CrsCourseOperationRules.ValidateToken(operationId);
        await WithCourseLockAsync(courseId, async () =>
        {
            if (await FindStateAsync(courseId, operationId, ct) is not null)
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    DELETE FROM crs_course_operations WHERE "Id" = {operationId} AND "CourseId" = {courseId}
                    """, ct);
            return true;
        }, ct);
    }

    internal async Task EnsureDeletionAllowedAsync(int courseId, CancellationToken ct)
    {
        var blocked = await db.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM crs_course_operations WHERE "CourseId" = {courseId}
            """).AnyAsync(ct);
        CrsCourseOperationRules.EnsureDeletionAllowed(blocked);
    }

    private async Task<int?> FindStateAsync(int courseId, Guid operationId, CancellationToken ct) =>
        (await db.Database.SqlQuery<int>($"""
            SELECT "State" AS "Value" FROM crs_course_operations
            WHERE "Id" = {operationId} AND "CourseId" = {courseId}
            """).ToListAsync(ct)).Select(x => (int?)x).SingleOrDefault();

    internal async Task<T> WithCourseLockAsync<T>(int courseId, Func<Task<T>> action, CancellationToken ct)
    {
        var externalTransaction = db.Database.CurrentTransaction is not null ||
            System.Transactions.Transaction.Current is not null ||
            db.GetService<IDbContextTransactionManager>() is ITransactionEnlistmentManager { EnlistedTransaction: not null };
        await using var transaction = externalTransaction ? null : await db.Database.BeginTransactionAsync(ct);
        // All registry mutations and deletion acquire the same parent lock first.
        var ids = await db.Database.SqlQuery<int>($"""
            SELECT "Id" AS "Value" FROM crs_courses WHERE "Id" = {courseId} FOR UPDATE
            """).ToListAsync(ct);
        if (ids.Count == 0) throw new KeyNotFoundException("Ders bulunamadı.");
        var result = await action();
        if (transaction is not null) await transaction.CommitAsync(ct);
        return result;
    }
}

internal static class CrsCourseOperationRules
{
    internal static void ValidateCourseId(int id)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
    }

    internal static void ValidateKind(CrsCourseOperationKind kind)
    {
        if (kind is not (CrsCourseOperationKind.Backup or CrsCourseOperationKind.Restore))
            throw new ArgumentOutOfRangeException(nameof(kind));
    }

    internal static void ValidateToken(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("İşlem kimliği boş olamaz.", nameof(id));
    }

    internal static void EnsureDeletionAllowed(bool hasOperation)
    {
        if (hasOperation)
            throw new CrsConflictException("course_operation_in_progress",
                "Bekleyen veya devam eden yedekleme/geri yükleme işlemi bulunan ders silinemez.");
    }
}
