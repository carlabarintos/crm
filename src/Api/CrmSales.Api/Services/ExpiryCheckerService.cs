using CrmSales.Api.Auditing;
using CrmSales.Api.Master;
using CrmSales.Opportunities.Domain.Entities;
using CrmSales.Opportunities.Domain.Repositories;
using CrmSales.Quotes.Domain.Entities;
using CrmSales.Quotes.Domain.Repositories;
using CrmSales.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Api.Services;

public sealed class ExpiryCheckerService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiryCheckerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run immediately on first startup, then every 24 hours
        await RunChecksAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunChecksAsync(stoppingToken);
    }

    private async Task RunChecksAsync(CancellationToken ct)
    {
        logger.LogInformation("[ExpiryChecker] Starting expiry checks at {Time}", DateTime.UtcNow);

        List<Company> companies;
        await using (var masterScope = scopeFactory.CreateAsyncScope())
        {
            var masterDb = masterScope.ServiceProvider.GetRequiredService<MasterDbContext>();
            companies = await masterDb.Companies.Where(c => c.IsActive).ToListAsync(ct);
        }

        foreach (var company in companies)
        {
            try
            {
                await ProcessTenantAsync(company.Slug, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[ExpiryChecker] Error processing tenant {Slug}", company.Slug);
            }
        }

        logger.LogInformation("[ExpiryChecker] Expiry checks complete");
    }

    private async Task ProcessTenantAsync(string tenantSlug, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // Override tenant context so the schema interceptor targets the correct schema
        var tenantCtx = sp.GetRequiredService<ITenantContext>();
        tenantCtx.TenantId = tenantSlug;

        var quoteRepo = sp.GetRequiredService<IQuoteRepository>();
        var oppRepo   = sp.GetRequiredService<IOpportunityRepository>();
        var audit     = sp.GetRequiredService<IAuditService>();

        var today = DateTime.UtcNow.Date;

        // Expire overdue Sent quotes
        var allQuotes = await quoteRepo.GetAllAsync(ct);
        var toExpire = allQuotes
            .Where(q => q.Status == QuoteStatus.Sent && q.ExpiryDate.HasValue && q.ExpiryDate.Value.Date < today)
            .ToList();

        foreach (var quote in toExpire)
        {
            try
            {
                quote.Expire();
                await quoteRepo.UpdateAsync(quote, ct);
                await audit.LogAsync(tenantSlug, "quote.expired", "Quote", quote.Id.ToString(),
                    $"Quote {quote.QuoteNumber} automatically expired (expiry date {quote.ExpiryDate:d})",
                    "system", ct);
                logger.LogInformation("[ExpiryChecker] Expired quote {Number} for tenant {Slug}", quote.QuoteNumber, tenantSlug);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[ExpiryChecker] Failed to expire quote {Id}", quote.Id);
            }
        }

        // Close-lost overdue open opportunities
        var allOpps = await oppRepo.GetAllAsync(ct);
        var toCloseLost = allOpps
            .Where(o => !o.IsClosed && o.ExpectedCloseDate.HasValue && o.ExpectedCloseDate.Value.Date < today)
            .ToList();

        foreach (var opp in toCloseLost)
        {
            try
            {
                opp.ProgressStage(OpportunityStage.ClosedLost);
                await oppRepo.UpdateAsync(opp, ct);
                await audit.LogAsync(tenantSlug, "opportunity.expired", "Opportunity", opp.Id.ToString(),
                    $"Opportunity \"{opp.Name}\" automatically closed lost (expected close date {opp.ExpectedCloseDate:d})",
                    "system", ct);
                logger.LogInformation("[ExpiryChecker] Closed lost opportunity {Name} for tenant {Slug}", opp.Name, tenantSlug);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[ExpiryChecker] Failed to close-lost opportunity {Id}", opp.Id);
            }
        }
    }
}
