using Microsoft.AspNetCore.Mvc;
using ZFome.Application.Abstractions.Commands;
using ZFome.Domain.Exceptions;
using ZFome.Infrastructure.Persistence.Write;

namespace ZFome.Architecture.Tests;

public sealed class ArchitectureDependencyTests
{
    [Fact]
    public void Architecture_tests_load_the_renamed_production_assemblies()
    {
        string[] assemblyNames =
        {
            typeof(DomainException).Assembly.GetName().Name!,
            typeof(ICommand).Assembly.GetName().Name!,
            typeof(ZFomeDbContext).Assembly.GetName().Name!,
            typeof(Program).Assembly.GetName().Name!
        };

        Assert.Equal(
            ["ZFome.Domain", "ZFome.Application", "ZFome.Infrastructure", "ZFome.Api"],
            assemblyNames);
    }

    [Fact]
    public void Domain_has_no_outward_layer_or_persistence_dependencies()
    {
        var forbidden = new[]
        {
            "ZFome.Application",
            "ZFome.Infrastructure",
            "ZFome.Api",
            "Microsoft.EntityFrameworkCore",
            "Dapper",
            "Npgsql"
        };

        Assert.DoesNotContain(
            typeof(DomainException).Assembly.GetReferencedAssemblies(),
            reference => forbidden.Contains(reference.Name, StringComparer.Ordinal));
    }

    [Fact]
    public void Application_does_not_depend_on_api_or_infrastructure()
    {
        var referenced = typeof(ICommand).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(referenced, reference => reference.Name is "ZFome.Api" or "ZFome.Infrastructure");
    }

    [Fact]
    public void Inner_layers_do_not_reference_the_migrations_project()
    {
        Assert.DoesNotContain(
            typeof(DomainException).Assembly.GetReferencedAssemblies(),
            reference => reference.Name == "ZFome.Infrastructure.Migrations");
        Assert.DoesNotContain(
            typeof(ICommand).Assembly.GetReferencedAssemblies(),
            reference => reference.Name == "ZFome.Infrastructure.Migrations");
    }

    [Fact]
    public void Controllers_do_not_receive_persistence_or_infrastructure_types()
    {
        var controllers = typeof(Program).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type));

        foreach (var controller in controllers)
        {
            var dependencies = controller.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.ParameterType)
                .Concat(controller.GetFields().Select(field => field.FieldType));

            Assert.DoesNotContain(dependencies, IsForbiddenControllerDependency);
        }
    }

    [Fact]
    public void Infrastructure_is_the_only_production_layer_referencing_ef_core()
    {
        Assert.Contains(
            typeof(ZFomeDbContext).Assembly.GetReferencedAssemblies(),
            reference => reference.Name == "Microsoft.EntityFrameworkCore");
        Assert.DoesNotContain(
            typeof(Program).Assembly.GetReferencedAssemblies(),
            reference => reference.Name == "Microsoft.EntityFrameworkCore");
    }

    private static bool IsForbiddenControllerDependency(Type type)
    {
        var namespaceName = type.Namespace ?? string.Empty;
        return namespaceName.StartsWith("ZFome.Infrastructure", StringComparison.Ordinal)
            || namespaceName.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || namespaceName.StartsWith("Dapper", StringComparison.Ordinal)
            || namespaceName.StartsWith("Npgsql", StringComparison.Ordinal);
    }
}
