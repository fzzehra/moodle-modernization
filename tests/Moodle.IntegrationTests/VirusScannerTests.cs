using System.Net;
using System.Net.Sockets;
using System.Text;
using Moodle.Application.Errors;
using Moodle.Application.Files;
using Moodle.Infrastructure.Files;
namespace Moodle.IntegrationTests;

public sealed class RealClamAvFactAttribute : FactAttribute
{
    public RealClamAvFactAttribute() {
        if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CLAMAV_TEST_HOST")))
            Skip="Set CLAMAV_TEST_HOST to a running clamd; this is not a fake-scanner acceptance test.";
    }
}
public class VirusScannerTests
{
    [RealClamAvFact]
    public async Task T_FILE_14_real_ClamAV_clean_and_EICAR()
    {
        var scanner=new ClamAvVirusScanner(new(){Host=Environment.GetEnvironmentVariable("CLAMAV_TEST_HOST")!});
        Assert.Equal(VirusScanResult.Clean,await scanner.ScanAsync(new MemoryStream("clean coursework"u8.ToArray())));
        var eicar="X5O!P%@AP[4\\PZX54(P^)7CC)7}$"+"EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";
        Assert.Equal(VirusScanResult.Infected,await scanner.ScanAsync(new MemoryStream(Encoding.ASCII.GetBytes(eicar))));
    }
    [Fact]
    public async Task T_FILE_14_unavailable_daemon_denies()
    {
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
        var scanner=new ClamAvVirusScanner(new(){Host="127.0.0.1",Port=port,TimeoutSeconds=1});
        Assert.Equal(503,(await Assert.ThrowsAsync<AppException>(()=>scanner.ScanAsync(new MemoryStream("data"u8.ToArray())))).StatusCode);
    }
    [Fact]
    public async Task T_FILE_14_timeout_denies()
    {
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        try {
            var scanner=new ClamAvVirusScanner(new(){Host="127.0.0.1",Port=((IPEndPoint)listener.LocalEndpoint).Port,TimeoutSeconds=1});
            var scanning=scanner.ScanAsync(new MemoryStream("data"u8.ToArray()));
            using var accepted=await listener.AcceptTcpClientAsync();
            Assert.Equal(503,(await Assert.ThrowsAsync<AppException>(()=>scanning)).StatusCode);
        } finally {listener.Stop();}
    }
}
