namespace CrmSales.Settings.Application.Services;

public static class TemplateRenderer
{
    public static string Render(string template, Dictionary<string, string> values)
    {
        foreach (var (key, value) in values)
            template = template.Replace($"{{{{{key}}}}}", value);
        return template;
    }
}
