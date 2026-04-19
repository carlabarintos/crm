using CrmSales.Opportunities.Domain.Events;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Opportunities.Domain.Entities;

public enum OpportunityStage
{
    Prospecting = 1,
    Qualification = 2,
    Proposal = 3,
    ClosedWon = 4,
    ClosedLost = 5
}

public sealed class Opportunity : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public string AccountName { get; private set; }
    public Guid? ContactId { get; private set; }
    public string ContactName { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? ContactPhone { get; private set; }
    public OpportunityStage Stage { get; private set; }
    public decimal EstimatedValue { get; private set; }
    public string Currency { get; private set; }
    public decimal Probability { get; private set; }
    public DateTime? ExpectedCloseDate { get; private set; }
    public string? Description { get; private set; }
    public Guid OwnerId { get; private set; }
    public bool IsClosed => Stage is OpportunityStage.ClosedWon or OpportunityStage.ClosedLost;
    public bool IsWon => Stage == OpportunityStage.ClosedWon;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<Activity> _activities = [];
    public IReadOnlyCollection<Activity> Activities => _activities.AsReadOnly();

    private Opportunity()
    {
        Name = string.Empty; AccountName = string.Empty;
        ContactName = string.Empty; Currency = string.Empty;
    }

    public static Opportunity Create(
        string name, string accountName, string contactName,
        string? contactEmail, string? contactPhone,
        decimal estimatedValue, string currency,
        DateTime? expectedCloseDate, string? description, Guid ownerId,
        Guid? contactId = null)
    {
        var opp = new Opportunity
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            AccountName = accountName.Trim(),
            ContactId = contactId,
            ContactName = contactName.Trim(),
            ContactEmail = contactEmail?.Trim(),
            ContactPhone = contactPhone?.Trim(),
            Stage = OpportunityStage.Prospecting,
            EstimatedValue = estimatedValue,
            Currency = currency,
            Probability = 10,
            ExpectedCloseDate = expectedCloseDate,
            Description = description?.Trim(),
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        opp.RaiseDomainEvent(new OpportunityCreatedEvent(opp.Id, opp.Name, opp.AccountName, opp.OwnerId));
        return opp;
    }

    public void UpdateDetails(string name, string accountName, string contactName,
        string? contactEmail, string? contactPhone, decimal estimatedValue,
        string currency, DateTime? expectedCloseDate, string? description,
        Guid? contactId = null)
    {
        Name = name.Trim();
        AccountName = accountName.Trim();
        ContactId = contactId;
        ContactName = contactName.Trim();
        ContactEmail = contactEmail?.Trim();
        ContactPhone = contactPhone?.Trim();
        EstimatedValue = estimatedValue;
        Currency = currency;
        ExpectedCloseDate = expectedCloseDate;
        Description = description?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void ProgressStage(OpportunityStage newStage)
    {
        if (IsClosed)
            throw new InvalidOperationException("Cannot change stage of a closed opportunity.");
        var oldStage = Stage;
        Stage = newStage;
        Probability = newStage switch
        {
            OpportunityStage.Prospecting => 10,
            OpportunityStage.Qualification => 30,
            OpportunityStage.Proposal => 60,
            OpportunityStage.ClosedWon => 100,
            OpportunityStage.ClosedLost => 0,
            _ => Probability
        };
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new OpportunityStageChangedEvent(Id, oldStage, newStage));
        if (newStage == OpportunityStage.ClosedWon)
            RaiseDomainEvent(new OpportunityWonEvent(Id, Name, AccountName, EstimatedValue, Currency));
    }

    public void AddActivity(string type, string notes, Guid performedById)
    {
        var activity = Activity.Create(Id, type, notes, performedById);
        _activities.Add(activity);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reassign(Guid newOwnerId)
    {
        OwnerId = newOwnerId;
        UpdatedAt = DateTime.UtcNow;
    }
}
