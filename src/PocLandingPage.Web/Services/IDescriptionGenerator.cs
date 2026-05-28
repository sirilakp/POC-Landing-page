namespace PocLandingPage.Web.Services;

public interface IDescriptionGenerator
{
    bool IsEnabled { get; }
    Task<string> GenerateAsync(string name, string? url = null, string? keywords = null, CancellationToken ct = default);
}

public class NullDescriptionGenerator : IDescriptionGenerator
{
    public bool IsEnabled => false;

    public Task<string> GenerateAsync(string name, string? url = null, string? keywords = null, CancellationToken ct = default) =>
        throw new InvalidOperationException(
            "AI description generation is disabled. Set AzureOpenAI:Endpoint in configuration to enable.");
}
