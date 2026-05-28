using FluentAssertions;
using PocLandingPage.Web.Models;
using PocLandingPage.Web.Services;
using Xunit;

namespace PocLandingPage.Tests;

public class PocServiceTests
{
    [Fact]
    public async Task GetAllAsync_returns_empty_when_blob_missing()
    {
        var store = new InMemoryBlobStore();
        var svc = new PocService(store);

        var result = await svc.GetAllAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_creates_first_entry_and_writes_blob()
    {
        var store = new InMemoryBlobStore();
        var svc = new PocService(store);

        var added = await svc.AddAsync(new PocEntry { Name = "Iris", Url = "https://iris.example.com" });

        added.Id.Should().NotBeEmpty();
        var all = await svc.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].Name.Should().Be("Iris");
    }

    [Fact]
    public async Task GetVisibleForUserAsync_filters_by_oid()
    {
        var store = new InMemoryBlobStore();
        var svc = new PocService(store);
        await svc.AddAsync(new PocEntry { Name = "Public", Url = "https://a.example.com", AllowAllViewers = true });
        await svc.AddAsync(new PocEntry { Name = "Alice only", Url = "https://b.example.com", AllowedUserIds = { "alice-oid" } });
        await svc.AddAsync(new PocEntry { Name = "Bob only", Url = "https://c.example.com", AllowedUserIds = { "bob-oid" } });

        var visible = await svc.GetVisibleForUserAsync("alice-oid");

        visible.Select(p => p.Name).Should().BeEquivalentTo("Public", "Alice only");
    }

    [Fact]
    public async Task GetVisibleForUserAsync_throws_on_empty_oid()
    {
        var svc = new PocService(new InMemoryBlobStore());
        await FluentActions.Invoking(() => svc.GetVisibleForUserAsync(""))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateAsync_modifies_entry_in_place()
    {
        var store = new InMemoryBlobStore();
        var svc = new PocService(store);
        var added = await svc.AddAsync(new PocEntry { Name = "Old", Url = "https://x.example.com" });

        var updated = await svc.UpdateAsync(added.Id, p =>
        {
            p.Name = "New";
            p.AllowAllViewers = true;
        });

        updated.Should().NotBeNull();
        updated!.Name.Should().Be("New");
        updated.AllowAllViewers.Should().BeTrue();

        var fromStore = await svc.GetByIdAsync(added.Id);
        fromStore!.Name.Should().Be("New");
    }

    [Fact]
    public async Task UpdateAsync_returns_null_when_id_missing()
    {
        var svc = new PocService(new InMemoryBlobStore());
        var result = await svc.UpdateAsync(Guid.NewGuid(), _ => { });
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_removes_entry()
    {
        var store = new InMemoryBlobStore();
        var svc = new PocService(store);
        var added = await svc.AddAsync(new PocEntry { Name = "X", Url = "https://x.example.com" });

        var deleted = await svc.DeleteAsync(added.Id);

        deleted.Should().BeTrue();
        (await svc.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Mutation_retries_on_concurrent_write_then_succeeds()
    {
        var store = new ConcurrentRetryBlobStore();
        var svc = new PocService(store);

        var added = await svc.AddAsync(new PocEntry { Name = "Iris", Url = "https://iris.example.com" });

        added.Should().NotBeNull();
        store.Writes.Should().BeGreaterOrEqualTo(2, "first write should conflict, retry should succeed");
    }

    private class ConcurrentRetryBlobStore : IBlobStore
    {
        private byte[]? _content;
        private string? _etag;
        private int _version;
        private bool _injectedConflict;
        public int Writes { get; private set; }

        public Task<BlobReadResult?> ReadAsync(CancellationToken ct = default)
        {
            if (_content is null) return Task.FromResult<BlobReadResult?>(null);
            return Task.FromResult<BlobReadResult?>(new BlobReadResult(_content, _etag!));
        }

        public Task<string> WriteAsync(byte[] content, string? expectedETag, CancellationToken ct = default)
        {
            Writes++;

            if (!_injectedConflict)
            {
                _injectedConflict = true;
                _content = System.Text.Encoding.UTF8.GetBytes("[]");
                _etag = $"\"v{++_version}\"";
                throw new BlobConcurrencyException("simulated conflict");
            }

            if (_content is not null && expectedETag != _etag)
                throw new BlobConcurrencyException("ETag mismatch.");

            _content = content;
            _etag = $"\"v{++_version}\"";
            return Task.FromResult(_etag);
        }
    }
}
