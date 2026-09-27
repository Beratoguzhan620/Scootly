using NetArchTest.Rules;
using Xunit;

namespace Scootly.Architecture.Tests;

public sealed class LayerDependencyTests
{
    private const string DomainNamespace = "Scootly.Domain";
    private const string ApplicationNamespace = "Scootly.Application";
    private const string InfrastructureNamespace = "Scootly.Infrastructure";
    private const string ApiNamespace = "Scootly.Api";

    [Fact]
    public void Domain_Hicbir_Katmana_Bagimli_Olmamali()
    {
        var domainAssembly = typeof(Scootly.Domain.Fleet.Vehicle).Assembly;

        var result = Types.InAssembly(domainAssembly)
            .Should()
            .NotHaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Domain katmanı şu bağımlılıkları içermemeli ama içeriyor: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_Infrastructure_Veya_Api_Katmanina_Bagimli_Olmamali()
    {
        var applicationAssembly = typeof(Scootly.Application.Riding.Commands.ReserveVehicleCommand).Assembly;

        var result = Types.InAssembly(applicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Application katmanı şu bağımlılıkları içermemeli ama içeriyor: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Infrastructure_Api_Katmanina_Bagimli_Olmamali()
    {
        var infrastructureAssembly = typeof(Scootly.Infrastructure.Persistence.ScootlyDbContext).Assembly;

        var result = Types.InAssembly(infrastructureAssembly)
            .Should()
            .NotHaveDependencyOn(ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Infrastructure katmanı Api'ye bağımlı olmamalı ama şu tipler bağımlı: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}