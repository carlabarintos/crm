using System.Net;
using System.Text.Json;

namespace CrmSales.Web.Client.Services;

public static class ApiErrors
{
    public static async Task<string> ParseAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                    return detail.GetString()!;
                if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                    return title.GetString()!;
            }
        }
        catch { }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized  => "You are not authorized to perform this action.",
            HttpStatusCode.Forbidden     => "You do not have permission to perform this action.",
            HttpStatusCode.NotFound      => "The requested item was not found.",
            HttpStatusCode.Conflict      => "This action conflicts with existing data.",
            HttpStatusCode.TooManyRequests => "Too many requests. Please try again later.",
            _                            => "An unexpected error occurred. Please try again."
        };
    }
}
