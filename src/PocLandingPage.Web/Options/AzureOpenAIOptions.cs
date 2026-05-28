namespace PocLandingPage.Web.Options;

public class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    public string? Endpoint { get; set; }
    public string Deployment { get; set; } = "gpt-4o-mini";

    public bool IsEnabled => !string.IsNullOrWhiteSpace(Endpoint);
}
