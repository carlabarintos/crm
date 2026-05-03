using CrmSales.Settings.Application.StorageSettings.DTOs;
using CrmSales.SharedKernel;
using CrmSales.SharedKernel.Application;

namespace CrmSales.Settings.Application.StorageSettings.Queries.GetStorageSettings;

public record GetStorageSettingsQuery : IQuery<Result<StorageSettingsDto>>;
