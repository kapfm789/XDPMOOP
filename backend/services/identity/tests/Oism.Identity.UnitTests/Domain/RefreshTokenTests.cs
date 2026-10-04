using Oism.Identity.Domain;

namespace Oism.Identity.UnitTests.Domain;

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 3, 15, 0, TimeSpan.Zero);

    [Fact]
    [Trait("UseCase", "UC-AUTH-02 AC-1")]
    public void Issue_NewToken_KeepsOnlyTheHashAndLivesSevenDays()
    {
        var userId = Guid.NewGuid();

        var record = RefreshToken.Issue(userId, Now, out var token);
        var other = RefreshToken.Issue(userId, Now, out var otherToken);

        Assert.Equal(userId, record.UserId);
        Assert.Equal(RefreshToken.Hash(token), record.TokenHash);
        Assert.NotEqual(token, record.TokenHash);
        Assert.NotEqual(token, otherToken);
        Assert.NotEqual(record.TokenHash, other.TokenHash);
        Assert.Equal(Now.AddDays(7), record.ExpiresAt);
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-02 AC-3")]
    public void IsUsable_UntilExpiryOrRevocation()
    {
        var record = RefreshToken.Issue(Guid.NewGuid(), Now, out _);

        Assert.True(record.IsUsable(Now.AddDays(7).AddSeconds(-1)));
        Assert.False(record.IsUsable(Now.AddDays(7)));

        record.Revoke(Now.AddHours(1));
        record.Revoke(Now.AddHours(2));

        Assert.False(record.IsUsable(Now.AddHours(3)));
        Assert.Equal(Now.AddHours(1), record.RevokedAt);
        Assert.False(record.WasReplaced);
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-02 AC-4")]
    public void ReplaceWith_NextToken_MarksItAsRotated()
    {
        var current = RefreshToken.Issue(Guid.NewGuid(), Now, out _);
        var next = RefreshToken.Issue(current.UserId, Now.AddHours(1), out _);

        current.ReplaceWith(next, Now.AddHours(1));

        Assert.True(current.WasReplaced);
        Assert.Equal(next.Id, current.ReplacedById);
        Assert.False(current.IsUsable(Now.AddHours(1)));
        Assert.True(next.IsUsable(Now.AddHours(1)));
    }
}
