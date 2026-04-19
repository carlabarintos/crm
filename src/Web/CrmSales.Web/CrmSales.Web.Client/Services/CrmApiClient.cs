using System.Net.Http.Json;

namespace CrmSales.Web.Client.Services;

public class CrmApiClient(HttpClient httpClient)
{
    private readonly HttpClient _http = httpClient;

    // ── Categories ─────────────────────────────────────────────────────────
    public Task<List<CategoryDto>?> GetCategoriesAsync()
        => _http.GetFromJsonAsync<List<CategoryDto>>("/api/categories");

    public Task<HttpResponseMessage> CreateCategoryAsync(object body)
        => _http.PostAsJsonAsync("/api/categories", body);

    // ── Products ───────────────────────────────────────────────────────────
    public Task<List<ProductDto>?> GetProductsAsync(string? search = null, bool? isActive = null)
    {
        var url = "/api/products";
        var query = new List<string>();
        if (!string.IsNullOrEmpty(search)) query.Add($"search={Uri.EscapeDataString(search)}");
        if (isActive.HasValue) query.Add($"isActive={isActive}");
        if (query.Count > 0) url += "?" + string.Join("&", query);
        return _http.GetFromJsonAsync<List<ProductDto>>(url);
    }

    public Task<ProductDto?> GetProductAsync(Guid id)
        => _http.GetFromJsonAsync<ProductDto>($"/api/products/{id}");

    public Task<HttpResponseMessage> CreateProductAsync(object body)
        => _http.PostAsJsonAsync("/api/products", body);

    public Task<HttpResponseMessage> UpdateProductAsync(Guid id, object body)
        => _http.PutAsJsonAsync($"/api/products/{id}", body);

    public Task<HttpResponseMessage> DeleteProductAsync(Guid id)
        => _http.DeleteAsync($"/api/products/{id}");

    // ── Contacts ──────────────────────────────────────────────────────────────
    public Task<List<ContactDto>?> GetContactsAsync(string? search = null)
    {
        var url = "/api/contacts";
        if (!string.IsNullOrEmpty(search)) url += $"?search={Uri.EscapeDataString(search)}";
        return _http.GetFromJsonAsync<List<ContactDto>>(url);
    }

    public Task<ContactDetailDto?> GetContactAsync(Guid id)
        => _http.GetFromJsonAsync<ContactDetailDto>($"/api/contacts/{id}");

    public Task<HttpResponseMessage> CreateContactAsync(object body)
        => _http.PostAsJsonAsync("/api/contacts", body);

    public Task<HttpResponseMessage> UpdateContactAsync(Guid id, object body)
        => _http.PutAsJsonAsync($"/api/contacts/{id}", body);

    public Task<HttpResponseMessage> DeleteContactAsync(Guid id)
        => _http.DeleteAsync($"/api/contacts/{id}");

    // ── Opportunities ──────────────────────────────────────────────────────
    public Task<List<OpportunityDto>?> GetOpportunitiesAsync(string? search = null, string? stage = null)
    {
        var url = "/api/opportunities";
        var query = new List<string>();
        if (!string.IsNullOrEmpty(search)) query.Add($"search={Uri.EscapeDataString(search)}");
        if (!string.IsNullOrEmpty(stage)) query.Add($"stage={Uri.EscapeDataString(stage)}");
        if (query.Count > 0) url += "?" + string.Join("&", query);
        return _http.GetFromJsonAsync<List<OpportunityDto>>(url);
    }

    public Task<OpportunityDetailDto?> GetOpportunityAsync(Guid id)
        => _http.GetFromJsonAsync<OpportunityDetailDto>($"/api/opportunities/{id}");

    public Task<HttpResponseMessage> CreateOpportunityAsync(object body)
        => _http.PostAsJsonAsync("/api/opportunities", body);

    public Task<HttpResponseMessage> ChangeOpportunityStageAsync(Guid id, string stage)
        => _http.PatchAsJsonAsync($"/api/opportunities/{id}/stage", new { Stage = stage });

    public Task<HttpResponseMessage> AddActivityAsync(Guid id, object body)
        => _http.PostAsJsonAsync($"/api/opportunities/{id}/activities", body);

    // ── Quotes ─────────────────────────────────────────────────────────────
    public Task<List<QuoteDto>?> GetQuotesAsync(Guid? opportunityId = null)
        => _http.GetFromJsonAsync<List<QuoteDto>>(
            opportunityId.HasValue ? $"/api/quotes?opportunityId={opportunityId}" : "/api/quotes");

    public Task<QuoteDetailDto?> GetQuoteAsync(Guid id)
        => _http.GetFromJsonAsync<QuoteDetailDto>($"/api/quotes/{id}");

    public Task<HttpResponseMessage> CreateQuoteAsync(object body)
        => _http.PostAsJsonAsync("/api/quotes", body);

    public Task<HttpResponseMessage> AddLineItemAsync(Guid id, object body)
        => _http.PostAsJsonAsync($"/api/quotes/{id}/line-items", body);

    public Task<HttpResponseMessage> SendQuoteAsync(Guid id)
        => _http.PostAsync($"/api/quotes/{id}/send", null);

    public Task<HttpResponseMessage> AcceptQuoteAsync(Guid id)
        => _http.PostAsync($"/api/quotes/{id}/accept", null);

    public Task<HttpResponseMessage> RejectQuoteAsync(Guid id, string? reason)
        => _http.PostAsJsonAsync($"/api/quotes/{id}/reject", new { Reason = reason });

    // ── Orders ─────────────────────────────────────────────────────────────
    public Task<List<OrderDto>?> GetOrdersAsync(string? status = null)
        => _http.GetFromJsonAsync<List<OrderDto>>(
            status != null ? $"/api/orders?status={status}" : "/api/orders");

    public Task<OrderDetailDto?> GetOrderAsync(Guid id)
        => _http.GetFromJsonAsync<OrderDetailDto>($"/api/orders/{id}");

    public Task<HttpResponseMessage> ConfirmOrderAsync(Guid id)
        => _http.PostAsync($"/api/orders/{id}/confirm", null);

    public Task<HttpResponseMessage> ProcessOrderAsync(Guid id)
        => _http.PostAsync($"/api/orders/{id}/process", null);

    public Task<HttpResponseMessage> ShipOrderAsync(Guid id, string? trackingInfo)
        => _http.PostAsJsonAsync($"/api/orders/{id}/ship", new { TrackingInfo = trackingInfo });

    public Task<HttpResponseMessage> DeliverOrderAsync(Guid id)
        => _http.PostAsync($"/api/orders/{id}/deliver", null);

    public Task<HttpResponseMessage> CancelOrderAsync(Guid id, string reason)
        => _http.PostAsJsonAsync($"/api/orders/{id}/cancel", new { Reason = reason });

    public Task<HttpResponseMessage> AddOrderLineItemAsync(Guid id, object body)
        => _http.PostAsJsonAsync($"/api/orders/{id}/line-items", body);

    public Task<HttpResponseMessage> UpdateOrderLineItemAsync(Guid id, Guid lineItemId, object body)
        => _http.PutAsJsonAsync($"/api/orders/{id}/line-items/{lineItemId}", body);

    public Task<HttpResponseMessage> DeleteOrderLineItemAsync(Guid id, Guid lineItemId)
        => _http.DeleteAsync($"/api/orders/{id}/line-items/{lineItemId}");

    // ── Companies ─────────────────────────────────────────────────────────
    public Task<List<CompanyDto>?> GetCompaniesAsync()
        => _http.GetFromJsonAsync<List<CompanyDto>>("/api/companies");

    public Task<HttpResponseMessage> CreateCompanyAsync(object body)
        => _http.PostAsJsonAsync("/api/companies", body);

    public Task<HttpResponseMessage> CreateCompanyAdminAsync(Guid companyId, object body)
        => _http.PostAsJsonAsync($"/api/companies/{companyId}/admin", body);

    // ── Users ──────────────────────────────────────────────────────────────
    public Task<List<UserDto>?> GetUsersAsync()
        => _http.GetFromJsonAsync<List<UserDto>>("/api/users");

    public Task<HttpResponseMessage> CreateUserAsync(object body)
        => _http.PostAsJsonAsync("/api/users", body);

    public Task<HttpResponseMessage> UpdateUserAsync(Guid id, object body)
        => _http.PutAsJsonAsync($"/api/users/{id}", body);

    public Task<HttpResponseMessage> DeactivateUserAsync(Guid id)
        => _http.PostAsync($"/api/users/{id}/deactivate", null);

    public Task<HttpResponseMessage> ActivateUserAsync(Guid id)
        => _http.PostAsync($"/api/users/{id}/activate", null);
}

// ── DTOs ──────────────────────────────────────────────────────────────────
public record CategoryDto(Guid Id, string Name, string? Description, bool IsActive);

public record ProductDto(Guid Id, string Name, string? Description, string Sku,
    decimal Price, string Currency, Guid CategoryId, string? CategoryName,
    bool IsActive, int StockQuantity, DateTime CreatedAt, DateTime UpdatedAt);

public record ContactDto(Guid Id, string FirstName, string LastName, string FullName,
    string? Email, string? Phone, string? Company, string? JobTitle, bool IsActive, DateTime CreatedAt);

public record ContactDetailDto(Guid Id, string FirstName, string LastName, string FullName,
    string? Email, string? Phone, string? Company, string? JobTitle,
    string? Notes, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);

public record OpportunityDto(Guid Id, string Name, string AccountName, Guid? ContactId, string ContactName,
    string Stage, decimal EstimatedValue, string Currency, decimal Probability,
    DateTime? ExpectedCloseDate, Guid OwnerId, DateTime CreatedAt, DateTime UpdatedAt);

public record OpportunityDetailDto(Guid Id, string Name, string AccountName, Guid? ContactId, string ContactName,
    string? ContactEmail, string? ContactPhone, string Stage, decimal EstimatedValue,
    string Currency, decimal Probability, DateTime? ExpectedCloseDate, string? Description,
    Guid OwnerId, List<ActivityDto> Activities, DateTime CreatedAt, DateTime UpdatedAt);

public record ActivityDto(Guid Id, string Type, string Notes, DateTime OccurredAt);

public record QuoteDto(Guid Id, string QuoteNumber, Guid OpportunityId, string Status,
    decimal TotalAmount, string Currency, DateTime? ExpiryDate, DateTime CreatedAt);

public record QuoteDetailDto(Guid Id, string QuoteNumber, Guid OpportunityId, string Status,
    decimal SubTotal, decimal DiscountTotal, decimal TotalAmount, string Currency,
    DateTime? ExpiryDate, string? Notes, List<QuoteLineItemDto> LineItems,
    DateTime CreatedAt, DateTime UpdatedAt);

public record QuoteLineItemDto(Guid Id, Guid ProductId, string ProductName,
    int Quantity, decimal UnitPrice, decimal DiscountPercent, decimal LineTotal);

public record OrderDto(Guid Id, string OrderNumber, Guid QuoteId, string Status,
    decimal TotalAmount, string Currency, DateTime CreatedAt,
    DateTime? ShippedAt, DateTime? DeliveredAt);

public record OrderDetailDto(Guid Id, string OrderNumber, Guid QuoteId, string Status,
    decimal TotalAmount, string Currency, string? ShippingAddress, string? Notes,
    List<OrderLineItemDto> LineItems, DateTime CreatedAt,
    DateTime? ShippedAt, DateTime? DeliveredAt);

public record OrderLineItemDto(Guid Id, Guid ProductId, string ProductName,
    int Quantity, decimal UnitPrice, decimal LineTotal);

public record UserDto(Guid Id, string Email, string FirstName, string LastName, string FullName, string Role, bool IsActive);

public record CompanyDto(Guid Id, string Name, string Slug, bool IsActive, DateTime CreatedAt);
