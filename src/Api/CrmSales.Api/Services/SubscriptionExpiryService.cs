using CrmSales.Api.Master;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Api.Services;

public sealed class SubscriptionExpiryService(
    IServiceScopeFactory scopeFactory,
    ILogger<SubscriptionExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunAsync(stoppingToken);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        logger.LogInformation("[SubscriptionExpiry] Running checks at {Time}", DateTime.UtcNow);
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var now = DateTime.UtcNow;

        // Fetch active free plan IDs so we never expire them
        var freePlanIds = await db.SubscriptionPlans
            .Where(p => p.IsFree)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var expiredSubs = await db.CompanySubscriptions
            .Where(s => s.Status == SubscriptionStatus.Active
                     && s.PeriodEnd < now
                     && !freePlanIds.Contains(s.PlanId))
            .ToListAsync(ct);

        foreach (var sub in expiredSubs)
        {
            sub.Status = SubscriptionStatus.Expired;
            var company = await db.Companies.FindAsync([sub.CompanyId], ct);
            company?.UpdateSubscriptionStatus(SubscriptionStatus.Expired);
            logger.LogInformation("[SubscriptionExpiry] Expired subscription for company {Id}", sub.CompanyId);
        }

        var overdueInvoices = await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Sent && i.DueDate < now)
            .ToListAsync(ct);

        foreach (var inv in overdueInvoices)
        {
            inv.MarkOverdue();
            logger.LogInformation("[SubscriptionExpiry] Invoice {Number} marked overdue", inv.InvoiceNumber);
        }

        if (expiredSubs.Count > 0 || overdueInvoices.Count > 0)
            await db.SaveChangesAsync(ct);

        logger.LogInformation("[SubscriptionExpiry] Done — {Subs} subscriptions expired, {Inv} invoices overdue",
            expiredSubs.Count, overdueInvoices.Count);
    }
}
