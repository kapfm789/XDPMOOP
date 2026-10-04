using Oism.Catalog.Domain;

namespace Oism.Catalog.UnitTests.Categories;

public sealed class CategoryTests
{
    // root → child → grandchild; other đứng riêng.
    private readonly Category _root = new("Gốc", parentId: null);
    private readonly Category _child;
    private readonly Category _grandchild;
    private readonly Category _other = new("Khác", parentId: null);
    private readonly Dictionary<Guid, Guid?> _parentOf;

    public CategoryTests()
    {
        _child = new Category("Con", _root.Id);
        _grandchild = new Category("Cháu", _child.Id);
        _parentOf = new[] { _root, _child, _grandchild, _other }.ToDictionary(category => category.Id, category => category.ParentId);
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-01 AC-2")]
    public void CanMoveUnder_Itself_False() =>
        Assert.False(_root.CanMoveUnder(_root.Id, _parentOf));

    [Fact]
    [Trait("UseCase", "UC-PROD-01 AC-2")]
    public void CanMoveUnder_ItsChildOrGrandchild_False()
    {
        Assert.False(_root.CanMoveUnder(_child.Id, _parentOf));
        Assert.False(_root.CanMoveUnder(_grandchild.Id, _parentOf));
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-01 AC-1")]
    public void CanMoveUnder_RootAncestorOrUnrelatedBranch_True()
    {
        Assert.True(_grandchild.CanMoveUnder(null, _parentOf));
        Assert.True(_grandchild.CanMoveUnder(_root.Id, _parentOf));
        Assert.True(_child.CanMoveUnder(_other.Id, _parentOf));
        Assert.True(_other.CanMoveUnder(_grandchild.Id, _parentOf));
    }
}
