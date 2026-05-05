namespace CrmSales.Api.Master;

public class AccessRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Company { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? Message { get; set; }
    public string Status { get; set; } = "Pending"; // Pending | Approved | Rejected
    public Guid? SelectedPlanId { get; set; }
    public string? SelectedPlanName { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public static AccessRequest Create(string name, string company, string email, string? phone, string? message, Guid? selectedPlanId = null, string? selectedPlanName = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Company = company,
            Email = email,
            Phone = phone,
            Message = message,
            SelectedPlanId = selectedPlanId,
            SelectedPlanName = selectedPlanName,
            Status = "Pending",
            RequestedAt = DateTime.UtcNow
        };
}
