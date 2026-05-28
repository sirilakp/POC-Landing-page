using FluentAssertions;
using PocLandingPage.Web.Services;
using Xunit;

namespace PocLandingPage.Tests;

public class NullDescriptionGeneratorTests
{
    [Fact]
    public void IsEnabled_returns_false()
    {
        new NullDescriptionGenerator().IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_throws_with_helpful_message()
    {
        var gen = new NullDescriptionGenerator();
        var act = () => gen.GenerateAsync("test");
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*AzureOpenAI:Endpoint*");
    }
}
