using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Moodle.Application.Errors;
using Moodle.Application.Files;
namespace Moodle.Infrastructure.Files;

public sealed class ClamAvOptions
{
    public string Host { get; set; } = "clamav";
    public int Port { get; set; } = 3310;
    public int TimeoutSeconds { get; set; } = 30;
}
/// <summary>clamd INSTREAM; bounded memory, no paths or file content in errors/logs.</summary>
public sealed class ClamAvVirusScanner(ClamAvOptions options) : IVirusScanner
{
    public async Task<VirusScanResult> ScanAsync(Stream content, CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        try {
            using var client = new TcpClient();
            await client.ConnectAsync(options.Host, options.Port, timeout.Token);
            await using var network = client.GetStream();
            await network.WriteAsync("zINSTREAM\0"u8.ToArray(), timeout.Token);
            var buffer = new byte[81920]; var length = new byte[4];
            int count;
            while ((count = await content.ReadAsync(buffer, timeout.Token)) > 0) {
                BinaryPrimitives.WriteInt32BigEndian(length, count);
                await network.WriteAsync(length, timeout.Token);
                await network.WriteAsync(buffer.AsMemory(0,count), timeout.Token);
            }
            Array.Clear(length);
            await network.WriteAsync(length, timeout.Token);
            var response = new List<byte>(); var one = new byte[1];
            while (response.Count < 4096 && await network.ReadAsync(one, timeout.Token) == 1 && one[0] != 0) response.Add(one[0]);
            var message = Encoding.UTF8.GetString(response.ToArray()).Trim();
            if (message.EndsWith(" FOUND", StringComparison.Ordinal)) return VirusScanResult.Infected;
            if (message == "stream: OK") return VirusScanResult.Clean;
            throw new IOException("Unexpected scanner response.");
        }
        catch (Exception ex) when (ex is IOException or SocketException || ex is OperationCanceledException && !ct.IsCancellationRequested) {
            throw new AppException("scanner_unavailable", "Virüs taraması tamamlanamadı; dosya kabul edilmedi.", 503);
        }
    }
}
