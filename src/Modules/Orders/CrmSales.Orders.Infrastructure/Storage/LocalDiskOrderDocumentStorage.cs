using CrmSales.Orders.Application.Services;
using Microsoft.Extensions.Options;

namespace CrmSales.Orders.Infrastructure.Storage;

public sealed class LocalStorageOptions
{
    public string BasePath { get; set; } = "uploads";
}

public sealed class LocalDiskOrderDocumentStorage(IOptions<LocalStorageOptions> opts) : IOrderDocumentStorage
{
    public async Task<string> UploadAsync(Guid orderId, string fileName, Stream content, string contentType, CancellationToken ct)
    {
        var ext = Path.GetExtension(fileName);
        var key = Path.Combine("orders", orderId.ToString(), $"{Guid.NewGuid()}{ext}");
        var fullPath = Path.Combine(opts.Value.BasePath, key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var file = File.Create(fullPath);
        await content.CopyToAsync(file, ct);
        return key;
    }

    public Task<Stream> DownloadAsync(string storageKey, CancellationToken ct)
        => Task.FromResult<Stream>(File.OpenRead(Path.Combine(opts.Value.BasePath, storageKey)));

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var path = Path.Combine(opts.Value.BasePath, storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
