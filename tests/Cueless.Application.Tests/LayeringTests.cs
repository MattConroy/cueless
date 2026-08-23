using System.Reflection;

namespace Cueless.Application.Tests;

public class LayeringTests
{
    [Fact]
    public void ApplicationDependsOnDomainAlone()
    {
        var application = Assembly.Load(new AssemblyName("Cueless.Application"));

        var cuelessReferences = application
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null && name.StartsWith("Cueless.", StringComparison.Ordinal))
            .ToArray();

        Assert.All(cuelessReferences, name => Assert.Equal("Cueless.Domain", name));
    }
}
