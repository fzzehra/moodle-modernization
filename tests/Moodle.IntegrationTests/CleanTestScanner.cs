using Moodle.Application.Files;
namespace Moodle.IntegrationTests;

// Only registered explicitly in isolated test hosts; production always uses clamd.
public sealed class CleanTestScanner : IVirusScanner
{
    public Task<VirusScanResult> ScanAsync(Stream content,CancellationToken ct=default)=>Task.FromResult(VirusScanResult.Clean);
}
