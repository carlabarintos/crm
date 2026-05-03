namespace CrmSales.Orders.Application.Services;

public abstract record ScanResult
{
    public static readonly ScanResult Clean = new CleanResult();
    public static ScanResult Infected(string virusName) => new InfectedResult(virusName);
    public static ScanResult Error(string message) => new ErrorResult(message);

    public sealed record CleanResult : ScanResult;
    public sealed record InfectedResult(string VirusName) : ScanResult;
    public sealed record ErrorResult(string Message) : ScanResult;
}

public interface IVirusScanService
{
    Task<ScanResult> ScanAsync(Stream content, CancellationToken ct);
}
