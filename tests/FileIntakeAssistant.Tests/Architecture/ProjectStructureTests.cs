using System.Xml.Linq;

namespace FileIntakeAssistant.Tests.Architecture;

public sealed class ProjectStructureTests
{
    [Fact]
    public void ProjectReferencesFollowLayeringRules()
    {
        var root = FindRepositoryRoot();

        var appProject = LoadProject(root, "src", "FileIntakeAssistant.App", "FileIntakeAssistant.App.csproj");
        var coreProject = LoadProject(root, "src", "FileIntakeAssistant.Core", "FileIntakeAssistant.Core.csproj");
        var infrastructureProject = LoadProject(root, "src", "FileIntakeAssistant.Infrastructure", "FileIntakeAssistant.Infrastructure.csproj");

        Assert.Equal(
            new[] { "..\\FileIntakeAssistant.Core\\FileIntakeAssistant.Core.csproj", "..\\FileIntakeAssistant.Infrastructure\\FileIntakeAssistant.Infrastructure.csproj" },
            ProjectReferences(appProject));

        Assert.Empty(ProjectReferences(coreProject));

        Assert.Equal(
            new[] { "..\\FileIntakeAssistant.Core\\FileIntakeAssistant.Core.csproj" },
            ProjectReferences(infrastructureProject));
    }

    [Fact]
    public void CoreDoesNotReferenceInfrastructureUiOrProviderPackages()
    {
        var root = FindRepositoryRoot();
        var coreProject = LoadProject(root, "src", "FileIntakeAssistant.Core", "FileIntakeAssistant.Core.csproj");

        Assert.Empty(ProjectReferences(coreProject));

        var packageReferences = PackageReferences(coreProject);
        Assert.DoesNotContain("Microsoft.Data.Sqlite", packageReferences);
        Assert.DoesNotContain("CommunityToolkit.Mvvm", packageReferences);
        Assert.DoesNotContain("Serilog", packageReferences);
    }

    [Fact]
    public void WpfAppTargetsWindowsAndEnablesWpf()
    {
        var root = FindRepositoryRoot();
        var appProject = LoadProject(root, "src", "FileIntakeAssistant.App", "FileIntakeAssistant.App.csproj");

        Assert.Equal("net8.0-windows", PropertyValue(appProject, "TargetFramework"));
        Assert.Equal("true", PropertyValue(appProject, "UseWPF"));
    }

    private static XDocument LoadProject(string root, params string[] pathParts)
    {
        var path = Path.Combine(new[] { root }.Concat(pathParts).ToArray());
        return XDocument.Load(path);
    }

    private static string[] ProjectReferences(XDocument project)
    {
        return project
            .Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .Where(include => include is not null)
            .Select(NormalizePath)
            .OrderBy(include => include, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] PackageReferences(XDocument project)
    {
        return project
            .Descendants("PackageReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .Where(include => include is not null)
            .Select(include => include!)
            .OrderBy(include => include, StringComparer.Ordinal)
            .ToArray();
    }

    private static string? PropertyValue(XDocument project, string propertyName)
    {
        return project
            .Descendants(propertyName)
            .Select(element => element.Value)
            .FirstOrDefault();
    }

    private static string NormalizePath(string? path)
    {
        return path!.Replace('/', '\\');
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FileIntakeAssistant.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root from test output path.");
    }
}
