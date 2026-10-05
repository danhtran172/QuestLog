using MasterPlay.Core.Entities;

namespace MasterPlay.Host.Tests;

/// <summary>DOMAIN_RULES §14.1 — bo cuc khung thong tin so huu do backend quyet dinh.</summary>
public sealed class OwnershipInfoLayoutTests
{
    [Theory]
    [InlineData(10, "Shared", "sharedAccount")]
    [InlineData(6, "Steam offline", "sharedAccount")]
    [InlineData(11, "Picklocked", "download")]
    [InlineData(2, "Cracked", "download")]
    [InlineData(1, "Owned", null)]
    [InlineData(5, "Wishlist", null)]
    public void Layout_follows_the_ownership_system_key(int id, string name, string? expected)
    {
        Assert.Equal(expected, LibraryQuery.OwnershipInfoLayout(new OwnershipStatus { Id = id, Name = name }));
    }

    [Fact]
    public void Missing_ownership_has_no_layout()
    {
        Assert.Null(LibraryQuery.OwnershipInfoLayout(null));
    }
}
