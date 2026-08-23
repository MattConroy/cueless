using System.Reflection;

namespace Cueless.Domain.Tests;

public class LayeringTests
{
    [Fact]
    public void DomainDependsOnNoOtherCuelessAssembly()
    {
        var domain = Assembly.Load(new AssemblyName("Cueless.Domain"));

        var cuelessReferences = domain
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null && name.StartsWith("Cueless.", StringComparison.Ordinal));

        Assert.Empty(cuelessReferences);
    }
}
