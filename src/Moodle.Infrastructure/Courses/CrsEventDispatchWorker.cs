using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moodle.Application.Courses;

namespace Moodle.Infrastructure.Courses;

/// <summary>
/// YZ destekli (Claude Code), CRS-FR-05 / CRS-NFR-02 / CRS-FR-10: commit edilmiş CRS olaylarını (ör. <c>course.deleted</c>)
/// diğer modüllerin kaydettiği <see cref="ICrsEventExtension"/> uzantılarına iletir; böylece ASG/GRD/FILE kendi
/// ders verilerini CRS'nin iç koduna bağlanmadan temizleyebilir. Kayıtlı uzantı yoksa görev veritabanına
/// dokunmadan hemen çıkar. Aralık: <c>Crs:EventDispatch:IntervalMilliseconds</c> (varsayılan 5000, en az 10).
/// Teslim, <see cref="CrsEventDispatcher"/>'ın alındı kayıtlarıyla en az bir kez ve tekrar denenebilir yapılır.
/// </summary>
public sealed class CrsEventDispatchWorker(
    IServiceScopeFactory scopes, IConfiguration configuration, TimeProvider clock, ILogger<CrsEventDispatchWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = configuration.GetValue("Crs:EventDispatch:IntervalMilliseconds", 5000);
        if (interval < 10)
            throw new InvalidOperationException("Crs:EventDispatch:IntervalMilliseconds en az 10 olmalıdır.");

        await using (var scope = scopes.CreateAsyncScope())
            if (scope.ServiceProvider.GetRequiredService<CrsEventExtensionPipeline>().Extensions.Count == 0)
                return;

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(interval), clock);
        while (await WaitAsync(timer, stoppingToken))
        {
            try
            {
                int delivered;
                do
                {
                    await using var scope = scopes.CreateAsyncScope();
                    delivered = await scope.ServiceProvider.GetRequiredService<CrsEventDispatcher>()
                        .DispatchAsync(ct: stoppingToken);
                } while (delivered > 0 && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                // Teslim edilemeyen olayın alındısı yazılmaz; sonraki turda aynı olay yeniden denenir.
                logger.LogError(ex, "CRS olayları uzantılara iletilemedi; sonraki turda yeniden denenecek.");
            }
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
