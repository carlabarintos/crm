namespace CrmSales.Settings.Application.StorageSettings.DTOs;

public record StorageSettingsDto(long MaxFileSizeBytes, int MaxFilesPerOrder, DateTime? UpdatedAt);
