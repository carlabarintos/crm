using CrmSales.SharedKernel.Domain;

namespace CrmSales.Orders.Domain.Entities;

public enum OrderDocumentType { Receipt, DeliveryPhoto, Invoice, PackingSlip, Other }

public sealed class OrderDocument : Entity<Guid>
{
    public Guid OrderId { get; private set; }
    public OrderDocumentType Type { get; private set; }
    public string FileName { get; private set; }
    public string StorageKey { get; private set; }
    public string ContentType { get; private set; }
    public long FileSizeBytes { get; private set; }
    public string? Notes { get; private set; }
    public DateTime UploadedAt { get; private set; }

    private OrderDocument() { FileName = StorageKey = ContentType = string.Empty; }

    public static OrderDocument Create(
        Guid orderId, OrderDocumentType type,
        string fileName, string storageKey, string contentType,
        long fileSizeBytes, string? notes)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key required.", nameof(storageKey));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name required.", nameof(fileName));

        return new OrderDocument
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Type = type,
            FileName = fileName.Trim(),
            StorageKey = storageKey,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            Notes = notes?.Trim(),
            UploadedAt = DateTime.UtcNow
        };
    }
}
