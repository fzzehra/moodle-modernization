using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moodle.Application.Files;
using Moodle.Infrastructure.Files;

namespace Moodle.UnitTests;

public sealed class FileCleanupWorkerTests
{
    private sealed class CountingMaintenance : IFileMaintenance
    {
        private int _runs;
        public int Runs => Volatile.Read(ref _runs);
        public TaskCompletionSource ReachedTwo { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool ThrowOnFirst { get; init; }

        public Task<FileMaintenanceResult> RunAsync(CancellationToken ct = default)
        {
            var n = Interlocked.Increment(ref _runs);
            if (n >= 2) ReachedTwo.TrySetResult();
            if (ThrowOnFirst && n == 1) throw new InvalidOperationException("ilk tur başarısız");
            return Task.FromResult(new FileMaintenanceResult(0, 0, 0, []));
        }
    }

    private static FileCleanupWorker Worker(IFileMaintenance maintenance, FileCleanupOptions options)
    {
        var services = new ServiceCollection().AddScoped(_ => maintenance).BuildServiceProvider();
        return new FileCleanupWorker(services.GetRequiredService<IServiceScopeFactory>(), options,
            TimeProvider.System, NullLogger<FileCleanupWorker>.Instance);
    }

    [Fact]
    public async Task FILE_FR_12_CleanupEnabled_false_iken_temizlik_calismaz()
    {
        var maintenance = new CountingMaintenance();
        using var worker = Worker(maintenance, new FileCleanupOptions(false, TimeSpan.FromMilliseconds(10)));

        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5)); // kapalıyken görev hemen biter
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(0, maintenance.Runs);
    }

    [Fact]
    public async Task FILE_FR_12_CleanupEnabled_true_iken_aralikla_tekrar_tekrar_calisir()
    {
        var maintenance = new CountingMaintenance();
        using var worker = Worker(maintenance, new FileCleanupOptions(true, TimeSpan.FromMilliseconds(20)));

        await worker.StartAsync(CancellationToken.None);
        await maintenance.ReachedTwo.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(maintenance.Runs >= 2);
    }

    [Fact]
    public async Task FILE_FR_12_bir_turdaki_hata_gorevi_durdurmaz()
    {
        var maintenance = new CountingMaintenance { ThrowOnFirst = true };
        using var worker = Worker(maintenance, new FileCleanupOptions(true, TimeSpan.FromMilliseconds(20)));

        await worker.StartAsync(CancellationToken.None);
        await maintenance.ReachedTwo.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(maintenance.Runs >= 2);
    }

    [Fact]
    public void Ayarlar_yapilandirmadan_okunur_varsayilan_acik_ve_60_dakikadir()
    {
        var empty = new ConfigurationBuilder().Build();
        Assert.Equal(new FileCleanupOptions(true, TimeSpan.FromMinutes(60)), FileCleanupOptions.FromConfiguration(empty));

        var custom = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Files:CleanupEnabled"] = "false",
            ["Files:CleanupIntervalMinutes"] = "15",
        }).Build();
        Assert.Equal(new FileCleanupOptions(false, TimeSpan.FromMinutes(15)), FileCleanupOptions.FromConfiguration(custom));
    }

    [Fact]
    public void Gecersiz_aralik_reddedilir()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Files:CleanupIntervalMinutes"] = "0",
        }).Build();
        Assert.Throws<InvalidOperationException>(() => FileCleanupOptions.FromConfiguration(config));
    }
}
