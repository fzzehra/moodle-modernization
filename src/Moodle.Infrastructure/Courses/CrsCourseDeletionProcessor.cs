using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Moodle.Infrastructure.Courses;

// YZ destekli (Claude Code): CRS-FR-11 / CRS-NFR-06 arka planda ders silme işleyicisi.

/// <summary>
/// Bekleyen silme işlerini işler. Her çağrı için ayrı bir DI scope'u (ve <see cref="CrsDbContext"/>) kullanılmalıdır.
/// Kilit sırası her zaman iş satırı → ders satırıdır; kuyruğa alma iş satırını kilitlemez, bu yüzden kilitlenme oluşmaz.
/// </summary>
public sealed class CrsCourseDeletionProcessor(
    CrsDbContext db, CrsService service, CrsBackgroundDeletionOptions options, ILogger<CrsCourseDeletionProcessor> logger)
{
    /// <summary>
    /// Açılışta "Running" kalmış işleri Pending'e döndürür. Başka bir örneğin o an işlediği iş satırı
    /// kilitli olduğundan SKIP LOCKED ile atlanır.
    /// </summary>
    public async Task<int> RecoverAsync(CancellationToken ct = default)
    {
        var recovered = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE crs_course_deletion_jobs SET "State" = 1
            WHERE "Id" IN (SELECT "Id" FROM crs_course_deletion_jobs WHERE "State" = 2 FOR UPDATE SKIP LOCKED)
            """, ct);
        if (recovered > 0)
            logger.LogWarning("{Count} yarım kalmış ders silme işi yeniden kuyruğa alındı.", recovered);
        return recovered;
    }

    public Task<int> CountActiveAsync(CancellationToken ct = default) =>
        db.Database.SqlQuery<int>($"""
            SELECT count(*)::integer AS "Value" FROM crs_course_deletion_jobs WHERE "State" IN (1, 2)
            """).SingleAsync(ct);

    /// <summary>Bir işi sahiplenip işler. Bekleyen iş yoksa false döner.</summary>
    public async Task<bool> ProcessNextAsync(CancellationToken ct = default)
    {
        // 1. Atomik sahiplenme: tek ifade, kendi transaction'ı içinde commit edilir.
        var claimed = (await db.Database.SqlQuery<ClaimRow>($"""
            UPDATE crs_course_deletion_jobs
            SET "State" = 2, "Attempts" = "Attempts" + 1, "StartedAt" = clock_timestamp(), "CompletedAt" = NULL
            WHERE "Id" = (
                SELECT "Id" FROM crs_course_deletion_jobs WHERE "State" = 1
                ORDER BY "CreatedAt", "Id" FOR UPDATE SKIP LOCKED LIMIT 1)
            RETURNING "Id", "CourseId", "Attempts"
            """).ToListAsync(ct)).SingleOrDefault();
        if (claimed is null) return false;

        // 2. Silme ve başarı kaydı aynı transaction'da: silindiği hâlde başarısız görünen iş oluşmaz.
        Exception? failure = null;
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            try
            {
                var owned = await db.Database.SqlQuery<Guid>($"""
                    SELECT "Id" AS "Value" FROM crs_course_deletion_jobs
                    WHERE "Id" = {claimed.Id} AND "State" = 2 FOR UPDATE
                    """).ToListAsync(ct);
                if (owned.Count == 0) return true; // Kurtarma tarafından geri alındı; başka bir tur işleyecek.

                // Kategori, kategori düzeyindeki durum sorgusu yetkisi için silme anındaki değerle güncellenir.
                var categories = await db.Database.SqlQuery<int>($"""
                    SELECT "CategoryId" AS "Value" FROM crs_courses WHERE "Id" = {claimed.CourseId} FOR UPDATE
                    """).ToListAsync(ct);
                if (categories.Count == 0) throw new KeyNotFoundException("Ders bulunamadı.");

                // Mevcut silme: aynı ders kilidi, NFR-03 kontrolü, cascade ve CRS olayları (trigger) korunur.
                await service.DeleteCourseAsync(claimed.CourseId, ct);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE crs_course_deletion_jobs
                    SET "State" = 3, "CompletedAt" = clock_timestamp(), "CategoryId" = {categories[0]},
                        "ErrorCode" = NULL, "ErrorMessage" = NULL
                    WHERE "Id" = {claimed.Id}
                    """, ct);
                await transaction.CommitAsync(ct);
                return true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                await RollbackQuietlyAsync(transaction);
                db.ChangeTracker.Clear();
                await ReturnToPendingAsync(claimed.Id);
                throw;
            }
            catch (Exception ex)
            {
                await RollbackQuietlyAsync(transaction);
                failure = ex;
            }
        }

        // 3. Hata kaydı silme transaction'ından ayrı, yeni bir transaction'da yapılır.
        db.ChangeTracker.Clear();
        var outcome = CrsCourseDeletionRules.Classify(failure!, claimed.Attempts, options.MaxAttempts);
        if (outcome.State == CrsCourseDeletionJobState.Failed && outcome.Code == CrsCourseDeletionRules.DeletionFailed)
            logger.LogError(failure, "Ders silme işi {JobId} (ders {CourseId}) başarısız oldu.", claimed.Id, claimed.CourseId);
        else
            logger.LogWarning(failure, "Ders silme işi {JobId} (ders {CourseId}) tamamlanamadı: {Code}.",
                claimed.Id, claimed.CourseId, outcome.Code);
        await using var failureTransaction = await db.Database.BeginTransactionAsync(CancellationToken.None);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE crs_course_deletion_jobs
            SET "State" = {(int)outcome.State}, "ErrorCode" = {outcome.Code}, "ErrorMessage" = {outcome.Message},
                "CompletedAt" = CASE WHEN {(int)outcome.State} = 4 THEN clock_timestamp() END
            WHERE "Id" = {claimed.Id} AND "State" = 2
            """, CancellationToken.None);
        await failureTransaction.CommitAsync(CancellationToken.None);
        return true;
    }

    private async Task ReturnToPendingAsync(Guid id)
    {
        try
        {
            // Kapanış bir deneme hakkı tüketmez; başarısız olursa açılış kurtarması işi geri alır.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE crs_course_deletion_jobs SET "State" = 1, "Attempts" = GREATEST("Attempts" - 1, 0)
                WHERE "Id" = {id} AND "State" = 2
                """, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ders silme işi {JobId} kapanışta Pending'e döndürülemedi.", id);
        }
    }

    private static async Task RollbackQuietlyAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        try { await transaction.RollbackAsync(CancellationToken.None); }
        catch { /* Bağlantı koptuysa sunucu transaction'ı zaten geri alır. */ }
    }

    private sealed class ClaimRow
    {
        public Guid Id { get; set; }
        public int CourseId { get; set; }
        public int Attempts { get; set; }
    }
}

/// <summary>
/// <see cref="CrsBackgroundDeletionOptions.Enabled"/> açıkken bekleyen silme işlerini yoklama aralığıyla işler.
/// HTTP isteğinden bağımsızdır: yalnızca uygulamanın durma token'ını kullanır ve her iş için yeni scope açar.
/// </summary>
public sealed class CrsCourseDeletionWorker(
    IServiceScopeFactory scopes, CrsBackgroundDeletionOptions options, TimeProvider clock,
    ILogger<CrsCourseDeletionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            await WarnIfJobsWaitingAsync(stoppingToken);
            return;
        }

        var recovered = await TryRecoverAsync(stoppingToken);
        using var timer = new PeriodicTimer(options.PollInterval, clock);
        while (await WaitAsync(timer, stoppingToken))
        {
            if (!recovered) recovered = await TryRecoverAsync(stoppingToken);
            if (recovered) await DrainAsync(stoppingToken);
        }
    }

    private async Task DrainAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await using var scope = scopes.CreateAsyncScope();
                if (!await scope.ServiceProvider.GetRequiredService<CrsCourseDeletionProcessor>().ProcessNextAsync(ct))
                    return;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ders silme kuyruğu işlenemedi; sonraki turda yeniden denenecek.");
        }
    }

    private async Task<bool> TryRecoverAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<CrsCourseDeletionProcessor>().RecoverAsync(ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return false; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Yarım kalmış ders silme işleri kurtarılamadı; sonraki turda yeniden denenecek.");
            return false;
        }
    }

    private async Task WarnIfJobsWaitingAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var waiting = await scope.ServiceProvider.GetRequiredService<CrsCourseDeletionProcessor>().CountActiveAsync(ct);
            if (waiting > 0)
                logger.LogWarning("Arka planda ders silme kapalı (Crs:BackgroundDeletion:Enabled=false) ancak " +
                    "{Count} bekleyen silme işi var; ayar açılana kadar işlenmeyecek.", waiting);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Bekleyen ders silme işleri kontrol edilemedi.");
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
