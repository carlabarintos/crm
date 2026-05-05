namespace CrmSales.Api.Master;

public class Invoice
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public Guid CompanyId { get; set; }
    public Guid? SubscriptionPlanId { get; set; }
    public string Status { get; set; } = InvoiceStatus.Draft;
    public decimal AmountPhp { get; set; }
    public string Description { get; set; } = "";
    public DateTime IssuedAt { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? Notes { get; set; }
    public string CreatedBy { get; set; } = "";

    public static Invoice Create(Guid companyId, Guid? planId, decimal amountPhp, string description,
        DateTime dueDate, string createdBy, string invoiceNumber) => new()
    {
        Id = Guid.NewGuid(),
        InvoiceNumber = invoiceNumber,
        CompanyId = companyId,
        SubscriptionPlanId = planId,
        Status = InvoiceStatus.Draft,
        AmountPhp = amountPhp,
        Description = description.Trim(),
        IssuedAt = DateTime.UtcNow,
        DueDate = dueDate,
        CreatedBy = createdBy
    };

    public void MarkSent() { Status = InvoiceStatus.Sent; SentAt = DateTime.UtcNow; }
    public void MarkPaid() { Status = InvoiceStatus.Paid; PaidAt = DateTime.UtcNow; }
    public void MarkOverdue() { Status = InvoiceStatus.Overdue; }
    public void Cancel() { Status = InvoiceStatus.Cancelled; }
}

public static class InvoiceStatus
{
    public const string Draft = "Draft";
    public const string Sent = "Sent";
    public const string Paid = "Paid";
    public const string Overdue = "Overdue";
    public const string Cancelled = "Cancelled";
}
