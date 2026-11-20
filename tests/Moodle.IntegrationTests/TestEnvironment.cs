using System.Runtime.CompilerServices;

namespace Moodle.IntegrationTests;

/// <summary>Test derlemesi yüklenirken bir kez çalışır; bütün WebApplicationFactory sunucularında geçerlidir.</summary>
internal static class TestEnvironment
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Dosya temizlik görevi (FileCleanupWorker) test sunucularında kapalıdır; temizlik testleri
        // FileMaintenanceService'i doğrudan ve sahte zamanla çağırır.
        Environment.SetEnvironmentVariable("Files__CleanupEnabled", "false");
    }
}
