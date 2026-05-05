using CrmSales.Api.Master;
using CrmSales.Settings.Application.Services;

namespace CrmSales.Api.Services;

public class InvoiceEmailService(IEmailService emailService, ILogger<InvoiceEmailService> logger)
{
    public async Task SendInvoiceAsync(Invoice invoice, Company company, SubscriptionPlan? plan,
        string toEmail, string paymentInstructions, CancellationToken ct = default)
    {
        var planName = plan?.Name ?? "";
        var body = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#111">
              <div style="background:#1e40af;color:#fff;padding:24px 32px;border-radius:8px 8px 0 0">
                <h1 style="margin:0;font-size:22px">CRM Sales</h1>
                <p style="margin:4px 0 0;font-size:14px;opacity:.8">Invoice</p>
              </div>
              <div style="background:#f8fafc;padding:24px 32px;border-left:1px solid #e2e8f0;border-right:1px solid #e2e8f0">
                <table style="width:100%;font-size:13px">
                  <tr>
                    <td><strong>Invoice #:</strong></td><td>{invoice.InvoiceNumber}</td>
                    <td><strong>Date:</strong></td><td>{invoice.IssuedAt:MMMM d, yyyy}</td>
                  </tr>
                  <tr>
                    <td><strong>Billed To:</strong></td><td>{company.Name}</td>
                    <td><strong>Due:</strong></td><td>{invoice.DueDate:MMMM d, yyyy}</td>
                  </tr>
                </table>
              </div>
              <div style="padding:24px 32px;border:1px solid #e2e8f0;border-top:none">
                <table style="width:100%;border-collapse:collapse;font-size:13px">
                  <thead>
                    <tr style="background:#f1f5f9">
                      <th style="padding:10px;text-align:left;border-bottom:2px solid #e2e8f0">Description</th>
                      <th style="padding:10px;text-align:right;border-bottom:2px solid #e2e8f0">Amount (PHP)</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      <td style="padding:12px 10px;border-bottom:1px solid #e2e8f0">
                        {invoice.Description}
                        {(string.IsNullOrEmpty(planName) ? "" : $"<br><span style='color:#6b7280;font-size:11px'>{planName}</span>")}
                      </td>
                      <td style="padding:12px 10px;text-align:right;border-bottom:1px solid #e2e8f0">&#8369;{invoice.AmountPhp:N2}</td>
                    </tr>
                  </tbody>
                  <tfoot>
                    <tr style="background:#f8fafc">
                      <td style="padding:12px 10px;font-weight:bold">TOTAL</td>
                      <td style="padding:12px 10px;text-align:right;font-weight:bold;font-size:16px">&#8369;{invoice.AmountPhp:N2}</td>
                    </tr>
                  </tfoot>
                </table>
              </div>
              <div style="padding:20px 32px;background:#eff6ff;border:1px solid #bfdbfe;border-top:none;border-radius:0 0 8px 8px">
                <p style="margin:0 0 8px;font-weight:bold;font-size:13px">Payment Instructions</p>
                <p style="margin:0;font-size:13px;white-space:pre-line">{paymentInstructions}</p>
                <p style="margin:8px 0 0;font-size:13px"><strong>Reference:</strong> {invoice.InvoiceNumber}</p>
              </div>
            </div>
            """;

        await emailService.SendAsync(
            toEmail,
            company.Name,
            $"Invoice {invoice.InvoiceNumber} — ₱{invoice.AmountPhp:N2} due {invoice.DueDate:MMM d, yyyy}",
            body,
            ct);

        logger.LogInformation("Invoice {Number} emailed to {Email}", invoice.InvoiceNumber, toEmail);
    }
}
