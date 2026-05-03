using CrmSales.SharedKernel.Domain;

namespace CrmSales.Settings.Domain.Entities;

public sealed class StorageSettings : AggregateRoot<Guid>
{
    public const long DefaultMaxFileSizeBytes = 10 * 1024 * 1024;
    public const int DefaultMaxFilesPerOrder = 10;

    public long MaxFileSizeBytes { get; private set; } = DefaultMaxFileSizeBytes;
    public int MaxFilesPerOrder { get; private set; } = DefaultMaxFilesPerOrder;
    public DateTime UpdatedAt { get; private set; }

    private StorageSettings() { }

    public static StorageSettings Create(long maxFileSizeBytes, int maxFilesPerOrder)
    {
        if (maxFileSizeBytes <= 0)
            throw new ArgumentException("Max file size must be positive.", nameof(maxFileSizeBytes));
        if (maxFilesPerOrder <= 0)
            throw new ArgumentException("Max files per order must be positive.", nameof(maxFilesPerOrder));

        return new StorageSettings
        {
            Id = Guid.NewGuid(),
            MaxFileSizeBytes = maxFileSizeBytes,
            MaxFilesPerOrder = maxFilesPerOrder,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(long maxFileSizeBytes, int maxFilesPerOrder)
    {
        if (maxFileSizeBytes <= 0)
            throw new ArgumentException("Max file size must be positive.", nameof(maxFileSizeBytes));
        if (maxFilesPerOrder <= 0)
            throw new ArgumentException("Max files per order must be positive.", nameof(maxFilesPerOrder));

        MaxFileSizeBytes = maxFileSizeBytes;
        MaxFilesPerOrder = maxFilesPerOrder;
        UpdatedAt = DateTime.UtcNow;
    }
}
