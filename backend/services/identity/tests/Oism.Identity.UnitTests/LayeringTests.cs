using System.Reflection;

namespace Oism.Identity.UnitTests;

// NFR-MAINT-01: phụ thuộc chỉ hướng vào Domain (docs/architecture/layering.md).
public sealed class LayeringTests
{
    [Fact]
    public void Domain_ReferencedAssemblies_OnlyStandardLibraryAndSharedKernel() =>
        Assert.All(References("Oism.Identity.Domain"), name =>
            Assert.True(name.StartsWith("System") || name is "netstandard" or "Oism.SharedKernel", name));

    [Fact]
    public void Application_ReferencedAssemblies_NoInfrastructureApiOrEfCore() =>
        Assert.DoesNotContain(References("Oism.Identity.Application"), name =>
            name is "Oism.Identity.Infrastructure" or "Oism.Identity.Api" or "Oism.BuildingBlocks"
            || name.StartsWith("Microsoft.EntityFrameworkCore"));

    private static IEnumerable<string> References(string assembly) =>
        Assembly.Load(assembly).GetReferencedAssemblies().Select(reference => reference.Name!);
}
