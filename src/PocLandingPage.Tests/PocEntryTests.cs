using FluentAssertions;
using PocLandingPage.Web.Models;
using Xunit;

namespace PocLandingPage.Tests;

public class PocEntryTests
{
    [Fact]
    public void IsVisibleTo_returns_true_when_AllowAllViewers()
    {
        var poc = new PocEntry { AllowAllViewers = true };
        poc.IsVisibleTo("any-oid").Should().BeTrue();
    }

    [Fact]
    public void IsVisibleTo_returns_true_when_user_in_allowed_list()
    {
        var poc = new PocEntry { AllowedUserIds = { "abc-123" } };
        poc.IsVisibleTo("abc-123").Should().BeTrue();
    }

    [Fact]
    public void IsVisibleTo_is_case_insensitive_on_oid()
    {
        var poc = new PocEntry { AllowedUserIds = { "ABC-123" } };
        poc.IsVisibleTo("abc-123").Should().BeTrue();
    }

    [Fact]
    public void IsVisibleTo_returns_false_when_not_in_list_and_not_open()
    {
        var poc = new PocEntry { AllowedUserIds = { "other" } };
        poc.IsVisibleTo("abc").Should().BeFalse();
    }
}
