using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using PocLandingPage.Web.Options;

namespace PocLandingPage.Web.Services;

public class AzureOpenAIDescriptionGenerator : IDescriptionGenerator
{
    private readonly AzureOpenAIClient _client;
    private readonly string _deployment;

    public AzureOpenAIDescriptionGenerator(AzureOpenAIClient client, IOptions<AzureOpenAIOptions> opts)
    {
        _client = client;
        _deployment = opts.Value.Deployment;
    }

    public bool IsEnabled => true;

    public async Task<string> GenerateAsync(string name, string? url = null, string? keywords = null, CancellationToken ct = default)
    {
        var chat = _client.GetChatClient(_deployment);
        var prompt = $"""
            Write a concise (2-3 sentence) description for a Proof of Concept project.
            Name: {name}
            URL: {url ?? "(none)"}
            Keywords / notes: {keywords ?? "(none)"}
            Audience: internal Inholland staff browsing a POC landing page.
            Tone: factual, plain English, no marketing fluff.
            """;

        var response = await chat.CompleteChatAsync(
            new ChatMessage[]
            {
                new SystemChatMessage("You write short factual project blurbs."),
                new UserChatMessage(prompt),
            },
            cancellationToken: ct);

        return response.Value.Content[0].Text.Trim();
    }
}
