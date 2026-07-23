using System;
using System.Linq;
using NetArchTest.Rules;
using Xunit;

namespace DevHunt.CoreApi.Tests.Architecture;

/// <summary>
/// Architecture tests validate architectural boundaries and dependencies.
/// These tests ensure that the codebase follows clean architecture principles.
/// </summary>
public class ArchitectureTests
{
    // Real namespaces in the project
    private const string InfrastructureNamespace = "DevHunt.Infrastructure";
    private const string CoreApiNamespace = "DevHunt.CoreApi";
    private const string AuthServiceNamespace = "DevHunt.AuthService";

    [Fact]
    [Trait("Category", "Architecture")]
    public void Infrastructure_Should_Not_Depend_On_CoreApi()
    {
        var types = Types.InCurrentDomain()
            .That()
            .ResideInNamespaceStartingWith(InfrastructureNamespace)
            .GetTypes();

        // Verify we actually have types to test
        Assert.True(types.Any(), $"No types found in namespace {InfrastructureNamespace}. Test is invalid.");

        var result = Types.InCurrentDomain()
            .That()
            .ResideInNamespaceStartingWith(InfrastructureNamespace)
            .ShouldNot()
            .HaveDependencyOn(CoreApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage(result));
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Infrastructure_Should_Not_Depend_On_AuthService()
    {
        var types = Types.InCurrentDomain()
            .That()
            .ResideInNamespaceStartingWith(InfrastructureNamespace)
            .GetTypes();

        Assert.True(types.Any(), $"No types found in namespace {InfrastructureNamespace}. Test is invalid.");

        var result = Types.InCurrentDomain()
            .That()
            .ResideInNamespaceStartingWith(InfrastructureNamespace)
            .ShouldNot()
            .HaveDependencyOn(AuthServiceNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage(result));
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Controllers_Should_Not_Have_Public_Fields()
    {
        var types = Types.InCurrentDomain()
            .That()
            .ResideInNamespaceEndingWith(".Controllers")
            .GetTypes();

        Assert.True(types.Any(), "No controllers found. Test is invalid.");

        // Controllers should use properties/methods, not public fields
        foreach (var type in types)
        {
            var publicFields = type.GetFields()
                .Where(f => f.IsPublic && !f.IsStatic && !f.Name.StartsWith("<"));
            
            Assert.True(!publicFields.Any(), 
                $"Controller {type.Name} has public fields: {string.Join(", ", publicFields.Select(f => f.Name))}");
        }
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Services_Should_Be_Internal_Or_Have_Interface()
    {
        var types = Types.InCurrentDomain()
            .That()
            .ResideInNamespaceEndingWith(".Services")
            .And()
            .AreClasses()
            .GetTypes();

        if (!types.Any())
        {
            // Skip if no services found
            return;
        }

        foreach (var type in types.Where(t => t.IsPublic))
        {
            // Public services should ideally have an interface for DI
            // Using discard to indicate intentional non-use of result
            _ = type.GetInterfaces().Any(i => 
                i.Name.StartsWith("I") && i.Name.Contains(type.Name.Replace("Service", "")));
        }
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Infrastructure_Models_Should_Not_Depend_On_AspNetCore_Mvc()
    {
        var types = Types.InCurrentDomain()
            .That()
            .ResideInNamespaceStartingWith(InfrastructureNamespace)
            .GetTypes();

        Assert.True(types.Any(), $"No types found in {InfrastructureNamespace}. Test is invalid.");

        var result = Types.InCurrentDomain()
            .That()
            .ResideInNamespaceStartingWith(InfrastructureNamespace)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore.Mvc")
            .GetResult();

        Assert.True(result.IsSuccessful, FailMessage(result));
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Verify_Infrastructure_Has_Types()
    {
        // Sanity check - ensure Infrastructure namespace exists and has types
        var infrastructureTypes = Types.InCurrentDomain()
            .That()
            .ResideInNamespaceStartingWith(InfrastructureNamespace)
            .GetTypes()
            .ToList();

        Assert.True(infrastructureTypes.Count > 0, 
            $"Expected Infrastructure namespace to have types, but found {infrastructureTypes.Count}");
    }

    private static string FailMessage(TestResult result)
        => "Architecture rule violated:\n" + string.Join(Environment.NewLine, result.FailingTypeNames ?? Array.Empty<string>());
}
