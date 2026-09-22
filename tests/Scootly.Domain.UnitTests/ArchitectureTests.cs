using System.Reflection;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class ArchitectureTests
{
    [Fact]
    public void Domain_Assembly_Hicbir_Dis_Pakete_Referans_Vermemeli()
    {
        var domainAssembly = typeof(Scootly.Domain.Fleet.Vehicle).Assembly;
        var referencedAssemblies = domainAssembly.GetReferencedAssemblies();

        var allowedPrefixes = new[] { "System", "netstandard", "mscorlib" };

        var disallowed = referencedAssemblies
            .Where(a => !allowedPrefixes.Any(prefix => a.Name!.StartsWith(prefix)))
            .Select(a => a.Name)
            .ToList();

        Assert.Empty(disallowed);
    }
}