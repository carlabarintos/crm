using CrmSales.SharedKernel;
using CrmSales.SharedKernel.Application;

namespace CrmSales.Settings.Application.StorageSettings.Commands.UpsertStorageSettings;

public record UpsertStorageSettingsCommand(long MaxFileSizeBytes, int MaxFilesPerOrder) : ICommand<Result>;
