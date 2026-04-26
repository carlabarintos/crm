using CrmSales.Quotes.Domain.Events;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Quotes.Domain.Entities;

public enum QuoteStatus { Draft, Sent, Accepted, Rejected, Expired }

public sealed class Quote : AggregateRoot<Guid>
{
    public string QuoteNumber { get; private set; }
    public Guid OpportunityId { get; private set; }
    public Guid OwnerId { get; private set; }
    public QuoteStatus Status { get; private set; }
    public DateTime? ExpiryDate { get; private set; }
    public string? Notes { get; private set; }
    public string Currency { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<QuoteLineItem> _lineItems = [];
    public IReadOnlyCollection<QuoteLineItem> LineItems => _lineItems.AsReadOnly();

    public string? TaxRateName { get; private set; }
    public decimal TaxRatePercent { get; private set; }

    public decimal SubTotal => _lineItems.Sum(l => l.LineTotal);
    public decimal DiscountTotal => _lineItems.Sum(l => l.DiscountAmount);
    public decimal TotalAmount => SubTotal - DiscountTotal;
    public decimal TaxAmount => Math.Round(TotalAmount * (TaxRatePercent / 100m), 4);
    public decimal GrandTotal => TotalAmount + TaxAmount;

    private Quote() { QuoteNumber = string.Empty; Currency = string.Empty; }

    public static Quote Create(Guid opportunityId, Guid ownerId, string currency,
        DateTime? expiryDate, string? notes)
    {
        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            QuoteNumber = GenerateQuoteNumber(),
            OpportunityId = opportunityId,
            OwnerId = ownerId,
            Status = QuoteStatus.Draft,
            Currency = currency,
            ExpiryDate = expiryDate,
            Notes = notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        quote.RaiseDomainEvent(new QuoteCreatedEvent(quote.Id, quote.QuoteNumber, opportunityId, ownerId));
        return quote;
    }

    public void AddLineItem(Guid productId, string productName, int quantity, decimal unitPrice, decimal discountPercent = 0)
    {
        if (Status != QuoteStatus.Draft)
            throw new InvalidOperationException("Can only modify draft quotes.");

        var existing = _lineItems.FirstOrDefault(l => l.ProductId == productId);
        if (existing != null)
            existing.UpdateQuantity(existing.Quantity + quantity);
        else
            _lineItems.Add(QuoteLineItem.Create(Id, productId, productName, quantity, unitPrice, discountPercent));

        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveLineItem(Guid lineItemId)
    {
        if (Status != QuoteStatus.Draft)
            throw new InvalidOperationException("Can only modify draft quotes.");
        var item = _lineItems.FirstOrDefault(l => l.Id == lineItemId)
            ?? throw new InvalidOperationException("Line item not found.");
        _lineItems.Remove(item);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Send()
    {
        if (Status != QuoteStatus.Draft)
            throw new InvalidOperationException("Only draft quotes can be sent.");
        if (!_lineItems.Any())
            throw new InvalidOperationException("Cannot send a quote with no line items.");
        Status = QuoteStatus.Sent;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new QuoteSentEvent(Id, QuoteNumber, OpportunityId, TotalAmount, Currency));
    }

    public void Accept()
    {
        if (Status != QuoteStatus.Sent)
            throw new InvalidOperationException("Only sent quotes can be accepted.");
        Status = QuoteStatus.Accepted;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new QuoteAcceptedEvent(Id, QuoteNumber, OpportunityId, TotalAmount, Currency, OwnerId));
    }

    public void Reject(string? reason = null)
    {
        if (Status != QuoteStatus.Sent)
            throw new InvalidOperationException("Only sent quotes can be rejected.");
        Status = QuoteStatus.Rejected;
        Notes = reason ?? Notes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyTax(string taxRateName, decimal taxRatePercent)
    {
        if (Status != QuoteStatus.Draft)
            throw new InvalidOperationException("Can only modify tax on draft quotes.");
        TaxRateName = taxRateName;
        TaxRatePercent = taxRatePercent;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveTax()
    {
        if (Status != QuoteStatus.Draft)
            throw new InvalidOperationException("Can only modify tax on draft quotes.");
        TaxRateName = null;
        TaxRatePercent = 0;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Expire()
    {
        if (Status != QuoteStatus.Sent)
            throw new InvalidOperationException("Only sent quotes can be expired.");
        Status = QuoteStatus.Expired;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string GenerateQuoteNumber() =>
        $"QT-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";
}
