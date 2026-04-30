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
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public static AccessRequest Create(string name, string company, string email, string? phone, string? message)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Company = company,
            Email = email,
            Phone = phone,
            Message = message,
            Status = "Pending",
            RequestedAt = DateTime.UtcNow
        };
}
