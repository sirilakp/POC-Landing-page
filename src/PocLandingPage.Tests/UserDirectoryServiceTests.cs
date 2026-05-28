using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using PocLandingPage.Web.Services;
using Xunit;

namespace PocLandingPage.Tests;

public class UserDirectoryServiceTests
{
    private static IMemoryCache NewCache() => new MemoryCache(new MemoryCacheOptions());

    [Fact]
    public async Task ResolveEmailToOidAsync_returns_oid_on_first_lookup()
    {
        var graph = new Mock<IGraphUserLookup>();
        graph.Setup(g => g.FindByEmailAsync("alice@example.com", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new UserLookup("alice-oid", "alice@example.com", "Alice"));

        var svc = new UserDirectoryService(graph.Object, NewCache());

        var oid = await svc.ResolveEmailToOidAsync("alice@example.com");

        oid.Should().Be("alice-oid");
    }

    [Fact]
    public async Task ResolveEmailToOidAsync_caches_subsequent_lookups()
    {
        var graph = new Mock<IGraphUserLookup>();
        graph.Setup(g => g.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new UserLookup("alice-oid", "alice@example.com", "Alice"));

        var svc = new UserDirectoryService(graph.Object, NewCache());

        await svc.ResolveEmailToOidAsync("alice@example.com");
        await svc.ResolveEmailToOidAsync("alice@example.com");
        await svc.ResolveEmailToOidAsync("ALICE@example.com");

        graph.Verify(g => g.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveEmailToOidAsync_returns_null_when_user_not_found()
    {
        var graph = new Mock<IGraphUserLookup>();
        graph.Setup(g => g.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserLookup?)null);

        var svc = new UserDirectoryService(graph.Object, NewCache());

        var oid = await svc.ResolveEmailToOidAsync("ghost@example.com");

        oid.Should().BeNull();
    }

    [Fact]
    public async Task ResolveEmailsAsync_splits_resolved_and_unresolved()
    {
        var graph = new Mock<IGraphUserLookup>();
        graph.Setup(g => g.FindByEmailAsync("alice@example.com", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new UserLookup("alice-oid", "alice@example.com", "Alice"));
        graph.Setup(g => g.FindByEmailAsync("ghost@example.com", It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserLookup?)null);

        var svc = new UserDirectoryService(graph.Object, NewCache());

        var result = await svc.ResolveEmailsAsync(new[] { "alice@example.com", "ghost@example.com" });

        result.Oids.Should().BeEquivalentTo("alice-oid");
        result.Unresolved.Should().BeEquivalentTo("ghost@example.com");
    }

    [Fact]
    public async Task GetUsersByOidAsync_uses_cache_when_available()
    {
        var graph = new Mock<IGraphUserLookup>();
        graph.Setup(g => g.FindByEmailAsync("alice@example.com", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new UserLookup("alice-oid", "alice@example.com", "Alice"));

        var svc = new UserDirectoryService(graph.Object, NewCache());
        await svc.ResolveEmailToOidAsync("alice@example.com");

        var users = await svc.GetUsersByOidAsync(new[] { "alice-oid" });

        users["alice-oid"].Email.Should().Be("alice@example.com");
        users["alice-oid"].DisplayName.Should().Be("Alice");
        graph.Verify(g => g.GetByOidsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetUsersByOidAsync_fetches_uncached_from_graph()
    {
        var graph = new Mock<IGraphUserLookup>();
        graph.Setup(g => g.GetByOidsAsync(It.Is<IEnumerable<string>>(o => o.Contains("bob-oid")), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new[] { new UserLookup("bob-oid", "bob@example.com", "Bob") });

        var svc = new UserDirectoryService(graph.Object, NewCache());

        var users = await svc.GetUsersByOidAsync(new[] { "bob-oid" });

        users["bob-oid"].Email.Should().Be("bob@example.com");
        graph.Verify(g => g.GetByOidsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
