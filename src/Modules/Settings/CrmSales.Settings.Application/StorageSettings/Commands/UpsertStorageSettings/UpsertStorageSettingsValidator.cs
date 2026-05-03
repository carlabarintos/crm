using FluentValidation;

namespace CrmSales.Settings.Application.StorageSettings.Commands.UpsertStorageSettings;

public sealed class UpsertStorageSettingsValidator : AbstractValidator<UpsertStorageSettingsCommand>
{
    public UpsertStorageSettingsValidator()
    {
        RuleFor(x => x.MaxFileSizeBytes)
            .GreaterThan(0).WithMessage("Max file size must be greater than 0.")
            .LessThanOrEqualTo(100L * 1024 * 1024).WithMessage("Max file size cannot exceed 100 MB.");

        RuleFor(x => x.MaxFilesPerOrder)
            .GreaterThan(0).WithMessage("Max files per order must be greater than 0.")
            .LessThanOrEqualTo(50).WithMessage("Max files per order cannot exceed 50.");
    }
}
