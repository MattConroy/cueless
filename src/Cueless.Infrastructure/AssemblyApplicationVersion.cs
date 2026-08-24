using System.Reflection;
using Cueless.Application;

namespace Cueless.Infrastructure;

public sealed class AssemblyApplicationVersion : IApplicationVersion
{
    public string Current { get; } =
        typeof(AssemblyApplicationVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? string.Empty;
}
