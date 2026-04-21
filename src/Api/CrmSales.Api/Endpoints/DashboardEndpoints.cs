using CrmSales.Opportunities.Domain.Repositories;
using CrmSales.Orders.Domain.Repositories;
using CrmSales.Quotes.Domain.Repositories;

namespace CrmSales.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard", async (
            IOpportunityRepository oppRepo,
            IQuoteRepository quoteRepo,
            IOrderRepository orderRepo,
            CancellationToken ct) =>
        {
            // Quote and Order repos have separate DbContexts — run them in parallel
            var quoteTask = quoteRepo.GetSummaryAsync(ct);
            var orderTask = orderRepo.GetSummaryAsync(ct);

            // Opportunity repo shares its DbContext — run sequentially to avoid concurrent access
            var opp     = await oppRepo.GetSummaryAsync(ct);
            var topOpps = await oppRepo.GetTopOpportunitiesAsync(5, ct);

            var quote = await quoteTask;
            var order = await orderTask;

            var closed  = opp.Won + opp.Lost;
            var winRate = closed > 0 ? $"{(decimal)opp.Won / closed * 100:N0}%" : "—";

            var monthly = order.MonthlyRevenue
                .ToDictionary(m => new DateTime(m.Year, m.Month, 1).ToString("MMM yy"), m => m.Revenue);

            var currency = order.Currency != "USD" ? order.Currency
                         : opp.Currency  != "USD" ? opp.Currency
                         : "USD";

            return Results.Ok(new
            {
                opportunities = new
                {
                    totalCount    = opp.Total,
                    openCount     = opp.Total - opp.Won - opp.Lost,
                    wonCount      = opp.Won,
                    lostCount     = opp.Lost,
                    winRate,
                    pipelineValue = opp.PipelineValue,
                    weightedValue = opp.WeightedValue,
                    currency      = opp.Currency,
                    byStage       = opp.ByStage.Select(s => new { stage = s.Stage, count = s.Count, value = s.Value })
                },
                quotes = new
                {
                    totalCount    = quote.Total,
                    draftCount    = quote.Draft,
                    sentCount     = quote.Sent,
                    acceptedCount = quote.Accepted,
                    rejectedCount = quote.Rejected,
                    expiredCount  = quote.Expired,
                    sentValue     = quote.SentValue,
                    acceptedValue = quote.AcceptedValue,
                    currency      = quote.Currency
                },
                orders = new
                {
                    totalCount       = order.Total,
                    pendingCount     = order.Pending,
                    activeCount      = order.Active,
                    deliveredCount   = order.Delivered,
                    cancelledCount   = order.Cancelled,
                    deliveredRevenue = order.DeliveredRevenue,
                    currency         = order.Currency
                },
                monthlyRevenue = monthly,
                topOpportunities = topOpps,
                currency
            });
        })
        .WithTags("Dashboard")
        .RequireAuthorization();

        return app;
    }
}
