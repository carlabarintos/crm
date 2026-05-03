namespace CrmSales.Orders.Application.Services;

public interface IOrderDocumentStorage
{
    Task<string> UploadAsync(Guid orderId, string fileName, Stream content, string contentType, CancellationToken ct);
    Task<Stream> DownloadAsync(string storageKey, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}
