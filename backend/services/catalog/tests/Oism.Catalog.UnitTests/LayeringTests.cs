using System.Reflection;

namespace Oism.Catalog.UnitTests;

// NFR-MAINT-01: phụ thuộc chỉ hướng vào Domain (docs/architecture/layering.md).
public sealed class LayeringTests
{
    [Fact]
    public void Domain_ReferencedAssemblies_OnlyStandardLibraryAndSharedKernel() =>
        Assert.All(References("Oism.Catalog.Domain"), name =>
            Assert.True(name.StartsWith("System") || name is "netstandard" or "Oism.SharedKernel", name));

    [Fact]
    public void Application_ReferencedAssemblies_NoInfrastructureApiOrEfCore() =>
        Assert.DoesNotContain(References("Oism.Catalog.Application"), name =>
            name is "Oism.Catalog.Infrastructure" or "Oism.Catalog.Api" or "Oism.BuildingBlocks"
            || name.StartsWith("Microsoft.EntityFrameworkCore"));

    private static IEnumerable<string> References(string assembly) =>
        Assembly.Load(assembly).GetReferencedAssemblies().Select(reference => reference.Name!);
}
