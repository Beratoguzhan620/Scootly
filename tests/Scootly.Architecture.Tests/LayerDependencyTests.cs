using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Scootly.Architecture.Tests;

public sealed class LayerDependencyTests
{
    private const string DomainNamespace = "Scootly.Domain";
    private const string ApplicationNamespace = "Scootly.Application";
    private const string InfrastructureNamespace = "Scootly.Infrastructure";
    private const string ApiNamespace = "Scootly.Api";
    private const string WorkerNamespace = "Scootly.Worker";

    private static readonly Assembly DomainAssembly = typeof(Scootly.Domain.Fleet.Vehicle).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Scootly.Application.Riding.Commands.ReserveVehicleCommand).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Scootly.Infrastructure.Persistence.ScootlyDbContext).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Scootly.Api.Controllers.VehiclesController).Assembly;
    private static readonly Assembly WorkerAssembly = typeof(Scootly.Worker.WorkerOptions).Assembly;

    [Fact]
    public void Domain_Hicbir_Katmana_Bagimli_Olmamali()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace, WorkerNamespace)
            .GetResult();

        AssertSuccessful(result, "Domain katmanı diğer katmanlara bağımlı olmamalı");
    }

    [Fact]
    public void Domain_Hicbir_Framework_Paketine_Bagimli_Olmamali()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Microsoft.Extensions")
            .GetResult();

        AssertSuccessful(result, "Domain katmanı framework paketlerine bağımlı olmamalı");
    }

    [Fact]
    public void Application_Infrastructure_Veya_Sunum_Katmanlarina_Bagimli_Olmamali()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(InfrastructureNamespace, ApiNamespace, WorkerNamespace)
            .GetResult();

        AssertSuccessful(result, "Application katmanı dış katmanlara bağımlı olmamalı");
    }

    [Fact]
    public void Application_Somut_Veri_Erisim_Ve_Mesajlasma_Teknolojilerine_Bagimli_Olmamali()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "RabbitMQ",
                "StackExchange.Redis",
                "Microsoft.AspNetCore")
            .GetResult();

        AssertSuccessful(result, "Application katmanı teknolojiden bağımsız olmalı (bkz. ADR 0001)");
    }

    [Fact]
    public void Infrastructure_Sunum_Katmanlarina_Bagimli_Olmamali()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .Should()
            .NotHaveDependencyOnAny(ApiNamespace, WorkerNamespace)
            .GetResult();

        AssertSuccessful(result, "Infrastructure, Api veya Worker'a bağımlı olmamalı");
    }

    [Fact]
    public void Api_Ve_Worker_Birbirine_Bagimli_Olmamali()
    {
        var apiResult = Types.InAssembly(ApiAssembly).Should().NotHaveDependencyOn(WorkerNamespace).GetResult();
        var workerResult = Types.InAssembly(WorkerAssembly).Should().NotHaveDependencyOn(ApiNamespace).GetResult();

        AssertSuccessful(apiResult, "Api, Worker'a bağımlı olmamalı");
        AssertSuccessful(workerResult, "Worker, Api'ye bağımlı olmamalı");
    }

    [Fact]
    public void Controllerlar_DbContext_Somut_Tipine_Bagimli_Olmamali()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .ResideInNamespace("Scootly.Api.Controllers")
            .Should()
            .NotHaveDependencyOn("Scootly.Infrastructure.Persistence")
            .GetResult();

        AssertSuccessful(result, "Controller'lar ScootlyDbContext yerine Application soyutlamalarını kullanmalı");
    }

    private static void AssertSuccessful(NetArchTest.Rules.TestResult result, string message)
    {
        Assert.True(result.IsSuccessful, $"{message}. İhlal eden tipler: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
