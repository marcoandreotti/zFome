namespace ZFome.Architecture.Tests;

public sealed class ForbiddenPackageTests
{
    [Theory]
    [InlineData("MediatR")]
    [InlineData("AutoMapper")]
    public void Forbidden_packages_are_not_referenced(string packageName)
    {
        var root = FindRepositoryRoot();
        var files = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(root, "project.assets.json", SearchOption.AllDirectories));

        Assert.DoesNotContain(files, path =>
            File.ReadAllText(path).Contains(packageName, StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ZFome.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("ZFome.sln was not found from the test output path.");
    }
}
