using CrmSales.Opportunities.Domain.Repositories;
using CrmSales.Orders.Domain.Repositories;
using CrmSales.Quotes.Domain.Repositories;

namespace CrmSales.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard", async (
            int? year, int? month,
            IOpportunityRepository oppRepo,
            IQuoteRepository quoteRepo,
            IOrderRepository orderRepo,
            CancellationToken ct) =>
        {
            // Quote and Order repos have separate DbContexts — run them in parallel
            var quoteTask = quoteRepo.GetSummaryAsync(year, month, ct);
            var orderTask = orderRepo.GetSummaryAsync(year, month, ct);

            // Opportunity repo shares its DbContext — run sequentially to avoid concurrent access
            var opp     = await oppRepo.GetSummaryAsync(year, month, ct);
            var topOpps = await oppRepo.GetTopOpportunitiesAsync(5, ct);

            var quote = await quoteTask;
            var order = await orderTask;

            var closed  = opp.Won + opp.Lost;
            var winRate = closed > 0 ? $"{(decimal)opp.Won / closed * 100:N0}%" : "—";

            // order.MonthlyRevenue is always all-time — slice for the bar chart, group for yearly totals
            var allMonthly = order.MonthlyRevenue;

            IEnumerable<MonthlyRevenueData> monthlySlice;
            if (year.HasValue)
            {
                monthlySlice = allMonthly.Where(m => m.Year == year.Value);
            }
            else
            {
                var cutoff = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-11);
                monthlySlice = allMonthly.Where(m => new DateTime(m.Year, m.Month, 1) >= cutoff);
            }

            var monthly = monthlySlice
                .OrderBy(m => m.Year).ThenBy(m => m.Month)
                .ToDictionary(m => new DateTime(m.Year, m.Month, 1).ToString("MMM yy"), m => m.Revenue);

            var yearlyRevenue = allMonthly
                .GroupBy(m => m.Year)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key.ToString(), g => g.Sum(m => m.Revenue));

            var availableYears = allMonthly
                .Select(m => m.Year).Distinct()
                .OrderByDescending(y => y).ToList();

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
                yearlyRevenue,
                availableYears,
                topOpportunities = topOpps,
                currency
            });
        })
        .WithTags("Dashboard")
        .RequireAuthorization();

        return app;
    }
}
