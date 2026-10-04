using Oism.Identity.Domain;

namespace Oism.Identity.UnitTests.Domain;

public sealed class UserTests
{
    [Theory]
    [InlineData("  Owner@Shop.VN ", "owner@shop.vn")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    [Trait("UseCase", "UC-AUTH-01 AC-2")]
    public void NormalizeEmail_TrimsLowercasesAndTreatsBlankAsMissing(string? input, string? expected) =>
        Assert.Equal(expected, User.NormalizeEmail(input));

    [Fact]
    [Trait("UseCase", "UC-AUTH-03 AC-1")]
    public void Create_NewUser_IsActiveWithNormalizedContact()
    {
        var now = new DateTimeOffset(2026, 10, 20, 3, 15, 0, TimeSpan.Zero);

        var user = User.Create(" Nguyễn An ", " AN@shop.vn", " 0901234567 ", "hash", UserRole.Staff, branchId: null, now);

        Assert.Equal(("Nguyễn An", "an@shop.vn", "0901234567"), (user.FullName, user.Email, user.Phone));
        Assert.True(user.IsActive);
        Assert.Equal(UserRole.Staff, user.Role);
        Assert.Equal(now, user.CreatedAt);
    }
}
