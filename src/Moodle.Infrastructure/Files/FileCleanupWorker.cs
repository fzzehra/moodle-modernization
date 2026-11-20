using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moodle.Application.Files;

namespace Moodle.Infrastructure.Files;

/// <summary>Temizlik görevinin ayarları: <c>Files:CleanupEnabled</c> (varsayılan açık), <c>Files:CleanupIntervalMinutes</c> (varsayılan 60).</summary>
public sealed record FileCleanupOptions(bool Enabled, TimeSpan Interval)
{
    public static FileCleanupOptions FromConfiguration(IConfiguration configuration)
    {
        var enabled = configuration.GetValue("Files:CleanupEnabled", true);
        var minutes = configuration.GetValue("Files:CleanupIntervalMinutes", 60);
        if (minutes < 1)
            throw new InvalidOperationException("Files:CleanupIntervalMinutes en az 1 olmalıdır.");
        return new FileCleanupOptions(enabled, TimeSpan.FromMinutes(minutes));
    }
}

/// <summary>
/// Temizlik görevini (<see cref="IFileMaintenance"/>) belirli aralıkla çalıştıran arka plan görevi (FILE-FR-12).
/// İlk çalıştırma, uygulama açıldıktan bir aralık sonradır. Bir turdaki hata görevi durdurmaz; günlüğe yazılır ve
/// sonraki tur yine çalışır. Birden çok örnekte aynı anda çalışmayı <see cref="FileMaintenanceService"/>'in danışma kilidi önler.
/// </summary>
public sealed class FileCleanupWorker(
    IServiceScopeFactory scopes, FileCleanupOptions options, TimeProvider clock, ILogger<FileCleanupWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Dosya temizlik görevi kapalı (Files:CleanupEnabled=false).");
            return;
        }

        using var timer = new PeriodicTimer(options.Interval, clock);
        while (await WaitForNextTickAsync(timer, stoppingToken))
            await RunOnceAsync(stoppingToken);
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IFileMaintenance>().RunAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Uygulama kapanıyor.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dosya temizlik turu başarısız oldu; sonraki turda yeniden denenecek.");
        }
    }
}
