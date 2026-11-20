namespace Moodle.Application.Files;

/// <summary>Scan the complete stream from its current position. Exceptions deny upload; never imply clean.</summary>
public interface IVirusScanner
{
    Task<VirusScanResult> ScanAsync(Stream content, CancellationToken ct = default);
}
public enum VirusScanResult { Clean, Infected }
