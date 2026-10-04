using Oism.Identity.Domain;

namespace Oism.Identity.UnitTests.Domain;

public sealed class BranchTests
{
    [Fact]
    [Trait("UseCase", "UC-AUTH-04 AC-1")]
    public void Create_NewBranch_IsActiveAtVersionOneWithTrimmedFields()
    {
        var branch = Branch.Create(" CH-01 ", " Cửa hàng 1 ", BranchType.Store, "  ");

        Assert.Equal(("CH-01", "Cửa hàng 1", BranchType.Store), (branch.Code, branch.Name, branch.Type));
        Assert.Null(branch.Address);
        Assert.True(branch.IsActive);
        Assert.Equal(1, branch.Version);
    }

    // BranchUpserted mang version để bên nhận bỏ qua bản cũ: mỗi lần sửa phải tăng đúng 1.
    [Fact]
    [Trait("UseCase", "UC-AUTH-04 AC-4")]
    public void UpdateAndSetActive_EachChange_IncrementsVersionByOne()
    {
        var branch = Branch.Create("CH-01", "Cửa hàng 1", BranchType.Store, null);

        branch.Update("Kho 1", BranchType.Warehouse, "12 Lê Lợi");
        Assert.Equal(("CH-01", "Kho 1", BranchType.Warehouse, "12 Lê Lợi", 2L),
            (branch.Code, branch.Name, branch.Type, branch.Address, branch.Version));

        branch.SetActive(false);
        Assert.False(branch.IsActive);
        Assert.Equal(3, branch.Version);
    }
}
