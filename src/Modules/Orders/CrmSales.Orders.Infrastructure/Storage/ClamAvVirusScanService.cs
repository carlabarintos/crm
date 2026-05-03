using CrmSales.Orders.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nClam;

namespace CrmSales.Orders.Infrastructure.Storage;

public sealed class ClamAvOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3310;
}

public sealed class ClamAvVirusScanService(
    IOptions<ClamAvOptions> opts,
    ILogger<ClamAvVirusScanService> logger) : IVirusScanService
{
    public async Task<ScanResult> ScanAsync(Stream content, CancellationToken ct)
    {
        try
        {
            var clam = new ClamClient(opts.Value.Host, opts.Value.Port);
            var result = await clam.SendAndScanFileAsync(content);
            return result.Result switch
            {
                ClamScanResults.Clean         => ScanResult.Clean,
                ClamScanResults.VirusDetected => ScanResult.Infected(
                    result.InfectedFiles?.FirstOrDefault()?.VirusName ?? "Unknown"),
                _                             => ScanResult.Error(result.RawResult ?? "Scan returned unknown status")
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ClamAV scan failed — rejecting upload as precaution");
            return ScanResult.Error("ClamAV unavailable");
        }
    }
}
