namespace CrmSales.Api.Master;

public class Company
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public static Company Create(string name, string slug) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Slug = slug,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };
}
