using Memorizer.Extensions;
using Memorizer.IntegrationTests.Logging;
using Memorizer.Models;
using Memorizer.Models.Enums;
using Memorizer.Models.ValueTypes;
using Memorizer.Services;
using Memorizer.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Memorizer.IntegrationTests;

/// <summary>
/// Integration tests for workspace and project search functionality.
/// Tests name-based search across hierarchy levels and breadcrumb/path retrieval.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class WorkspaceProjectSearchTests : IDisposable
{
    private readonly IntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly IServiceProvider _services;

    public void Dispose()
    {
        (_services as IDisposable)?.Dispose();
    }

    public WorkspaceProjectSearchTests(IntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _services = CreateServices();
    }

    private IServiceProvider CreateServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Storage"] = _fixture.PostgresConnectionString,
                ["Embeddings:ApiUrl"] = _fixture.OllamaApiUrl,
                ["Embeddings:Model"] = "all-minilm",
                ["Embeddings:Timeout"] = TimeSpan.FromMinutes(1).ToString()
            })
            .Build());

        services.AddHttpClient<IEmbeddingService, EmbeddingService>(client =>
        {
            client.BaseAddress = new Uri(_fixture.OllamaApiUrl);
            client.Timeout = TimeSpan.FromMinutes(1);
        });

        services.AddSingleton(new EmbeddingSettings
        {
            ApiUrl = new Uri(_fixture.OllamaApiUrl),
            Model = "all-minilm",
            Timeout = TimeSpan.FromMinutes(1)
        });

        services.AddMemorizer();
        services.AddLogging(builder => builder.AddXUnit(_output));

        return services.BuildServiceProvider();
    }

    #region Workspace Search Tests

    [Fact]
    public async Task SearchWorkspaces_FindsNestedWorkspaceByName()
    {
        var storage = _services.GetRequiredService<IStorage>();

        // Arrange: Create nested workspace hierarchy
        var root = await storage.CreateWorkspaceAsync("Engineering", "Root workspace", cancellationToken: default);
        var child = await storage.CreateWorkspaceAsync("Memorizer", "Child workspace", parentId: root.Id, cancellationToken: default);
        var grandchild = await storage.CreateWorkspaceAsync("Billing", "Grandchild workspace", parentId: child.Id, cancellationToken: default);

        // Act: Search for the deeply nested workspace
        var results = await storage.SearchWorkspacesAsync("Billing", includeSystem: false, cancellationToken: default);

        // Assert
        Assert.Single(results);
        Assert.Equal(grandchild.Id, results[0].Workspace.Id);
        Assert.Equal(2, results[0].Depth); // 2 ancestors
        Assert.Equal("Engineering > Memorizer > Billing", results[0].FullPath);

        _output.WriteLine($"Found workspace at path: {results[0].FullPath}");

        // Cleanup
        await storage.DeleteWorkspaceAsync(grandchild.Id, default);
        await storage.DeleteWorkspaceAsync(child.Id, default);
        await storage.DeleteWorkspaceAsync(root.Id, default);
    }

    [Fact]
    public async Task SearchWorkspaces_CaseInsensitive()
    {
        var storage = _services.GetRequiredService<IStorage>();

        // Arrange
        var workspace = await storage.CreateWorkspaceAsync("TestWorkspace", cancellationToken: default);

        // Act: Search with different cases
        var lower = await storage.SearchWorkspacesAsync("testworkspace", cancellationToken: default);
        var upper = await storage.SearchWorkspacesAsync("TESTWORKSPACE", cancellationToken: default);
        var mixed = await storage.SearchWorkspacesAsync("TeStWoRkSpAcE", cancellationToken: default);

        // Assert
        Assert.Single(lower);
        Assert.Single(upper);
        Assert.Single(mixed);
        Assert.Equal(workspace.Id, lower[0].Workspace.Id);

        _output.WriteLine("Case-insensitive search works correctly");

        // Cleanup
        await storage.DeleteWorkspaceAsync(workspace.Id, default);
    }

    [Fact]
    public async Task SearchWorkspaces_PartialMatch()
    {
        var storage = _services.GetRequiredService<IStorage>();

        // Arrange
        var workspace = await storage.CreateWorkspaceAsync("MyLongWorkspaceName", cancellationToken: default);

        // Act: Search with partial name
        var results = await storage.SearchWorkspacesAsync("Long", cancellationToken: default);

        // Assert
        Assert.Single(results);
        Assert.Equal(workspace.Id, results[0].Workspace.Id);

        _output.WriteLine($"Partial match found: {results[0].Workspace.Name}");

        // Cleanup
        await storage.DeleteWorkspaceAsync(workspace.Id, default);
    }

    [Fact]
    public async Task SearchWorkspaces_MultipleMatches_ReturnsAll()
    {
        var storage = _services.GetRequiredService<IStorage>();

        // Arrange: Create workspaces with common substring
        var ws1 = await storage.CreateWorkspaceAsync("API Project One", cancellationToken: default);
        var ws2 = await storage.CreateWorkspaceAsync("API Project Two", cancellationToken: default);
        var ws3 = await storage.CreateWorkspaceAsync("Something Else", cancellationToken: default);

        // Act
        var results = await storage.SearchWorkspacesAsync("API", cancellationToken: default);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Workspace.Id == ws1.Id);
        Assert.Contains(results, r => r.Workspace.Id == ws2.Id);

        _output.WriteLine($"Found {results.Count} matching workspaces");

        // Cleanup
        await storage.DeleteWorkspaceAsync(ws1.Id, default);
        await storage.DeleteWorkspaceAsync(ws2.Id, default);
        await storage.DeleteWorkspaceAsync(ws3.Id, default);
    }

    [Fact]
    public async Task SearchWorkspaces_ExcludesSystemByDefault()
    {
        var storage = _services.GetRequiredService<IStorage>();

        // The Unfiled workspace is a system workspace
        // Act: Search should not find it
        var results = await storage.SearchWorkspacesAsync("Unfiled", includeSystem: false, cancellationToken: default);

        // Assert: Should not find system workspaces
        Assert.DoesNotContain(results, r => r.Workspace.IsSystem);

        _output.WriteLine("System workspaces correctly excluded");
    }

    [Fact]
    public async Task SearchWorkspaces_IncludesSystemWhenRequested()
    {
        var storage = _services.GetRequiredService<IStorage>();

        // Act: Search with includeSystem = true
        var results = await storage.SearchWorkspacesAsync("Unfiled", includeSystem: true, cancellationToken: default);

        // Assert: Should find the Unfiled system workspace
        Assert.Contains(results, r => r.Workspace.IsSystem && r.Workspace.Name == "Unfiled");

        _output.WriteLine("System workspace found when includeSystem=true");
    }

    [Fact]
    public async Task GetWorkspacePath_ReturnsAncestorChain()
    {
        var storage = _services.GetRequiredService<IStorage>();

        // Arrange: Create 3-level hierarchy
        var root = await storage.CreateWorkspaceAsync("Root", cancellationToken: default);
        var middle = await storage.CreateWorkspaceAsync("Middle", parentId: root.Id, cancellationToken: default);
        var leaf = await storage.CreateWorkspaceAsync("Leaf", parentId: middle.Id, cancellationToken: default);

        // Act
        var path = await storage.GetWorkspacePathAsync(leaf.Id, cancellationToken: default);

        // Assert: Path should be [Root, Middle] (not including Leaf itself)
        Assert.Equal(2, path.Count);
        Assert.Equal(root.Id, path[0].Id);
        Assert.Equal("Root", path[0].Name);
        Assert.Equal(middle.Id, path[1].Id);
        Assert.Equal("Middle", path[1].Name);

        _output.WriteLine($"Path to Leaf: {string.Join(" > ", path.Select(p => p.Name))} > Leaf");

        // Cleanup
        await storage.DeleteWorkspaceAsync(leaf.Id, default);
        await storage.DeleteWorkspaceAsync(middle.Id, default);
        await storage.DeleteWorkspaceAsync(root.Id, default);
    }

    [Fact]
    public async Task GetWorkspacePath_RootWorkspace_ReturnsEmptyPath()
    {
        var storage = _services.GetRequiredService<IStorage>();

        // Arrange
        var root = await storage.CreateWorkspaceAsync("Root", cancellationToken: default);

        // Act
        var path = await storage.GetWorkspacePathAsync(root.Id, cancellationToken: default);

        // Assert: Root has no ancestors
        Assert.Empty(path);

        _output.WriteLine("Root workspace correctly has empty path");

        // Cleanup
        await storage.DeleteWorkspaceAsync(root.Id, default);
    }

    #endregion

    #region Project Search Tests

    [Fact]
    public async Task SearchProjects_FindsNestedProjectByName()
    {
        var storage = _services.GetRequiredService<IStorage>();

        // Arrange: Create workspace with nested projects
        var workspace = await storage.CreateWorkspaceAsync("Test Workspace", cancellationToken: default);
        var parent = await storage.CreateProjectAsync(workspace.Id, "Epic Feature", cancellationToken: default);
        var child = await storage.CreateProjectAsync(workspace.Id, "Sub Task", parentId: parent.Id, cancellationToken: default);

        // Act
        var results = await storage.SearchProjectsAsync("Sub Task", cancellationToken: default);

        // Assert
        Assert.Single(results);
        Assert.Equal(child.Id, results[0].Project.Id);
        Assert.Single(results[0].ProjectPath); // One project ancestor
        Assert.Equal("Test Workspace > Epic Feature > Sub Task", results[0].FullPath);

        _output.WriteLine($"Found project at path: {results[0].FullPath}");

        // Cleanup
        await storage.DeleteProjectAsync(child.Id, default);
        await storage.DeleteProjectAsync(parent.Id, default);
        await storage.DeleteWorkspaceAsync(workspace.Id, default);
    }

    [Fact]
    public async Task SearchProjects_CaseInsensitive()
    {
        var storage = _services.GetRequiredService<IStorage>();

        // Arrange
        var workspace = await storage.CreateWorkspaceAsync("Test Workspace", cancellationToken: default);
        var project = await storage.CreateProjectAsync(workspace.Id, "MyTestProject", cancellationToken: default);

        // Act
        var lower = await storage.SearchProjectsAsync("mytestproject", cancellationToken: default);
        var upper = await storage.SearchProjectsAsync("MYTESTPROJECT", cancellationToken: default);

        // Assert
        Assert.Single(lower);
        Assert.Single(upper);
        Assert.Equal(project.Id, lower[0].Project.Id);

        _output.WriteLine("Case-insensitive project search works correctly");

        // Cleanup
        await storage.DeleteProjectAsync(project.Id, default);
        await storage.DeleteWorkspaceAsync(workspace.Id, default);
    }

    [Fact]
    public async Task SearchProjects_FiltersbyStatus()
    {
        var storage = _services.GetRequiredService<IStorage>();

        // Arrange
        var workspace = await storage.CreateWorkspaceAsync("Test Workspace", cancellationToken: default);
        var active = await storage.CreateProjectAsync(workspace.Id, "Active Project", cancellationToken: default);
        var completed = await storage.CreateProjectAsync(workspace.Id, "Completed Project", cancellationToken: default);

        // Projects are created in Draft status, so we need to set them to Active/Completed
        await storage.UpdateProjectAsync(active.Id, status: ProjectStatusEnum.Active, cancellationToken: default);
        await storage.UpdateProjectAsync(completed.Id, status: ProjectStatusEnum.Completed, cancellationToken: default);

        // Act: Search with status filter
        var activeResults = await storage.SearchProjectsAsync("Project", statusFilter: ProjectStatusEnum.Active, cancellationToken: default);
        var completedResults = await storage.SearchProjectsAsync("Project", statusFilter: ProjectStatusEnum.Completed, cancellationToken: default);

        // Assert
        Assert.Single(activeResults);
        Assert.Equal(active.Id, activeResults[0].Project.Id);
        Assert.Single(completedResults);
        Assert.Equal(completed.Id, completedResults[0].Project.Id);

        _output.WriteLine($"Status filter works: {activeResults.Count} active, {completedResults.Count} completed");

        // Cleanup
        await storage.DeleteProjectAsync(active.Id, default);
        await storage.DeleteProjectAsync(completed.Id, default);
        await storage.DeleteWorkspaceAsync(workspace.Id, default);
    }

    [Fact]
    public async Task SearchProjects_NoStatusFilter_ReturnsAll()
    {
        var storage = _services.GetRequiredService<IStorage>();

        // Arrange
        var workspace = await storage.CreateWorkspaceAsync("Test Workspace", cancellationToken: default);
        var active = await storage.CreateProjectAsync(workspace.Id, "Search Active", cancellationToken: default);
        var completed = await storage.CreateProjectAsync(workspace.Id, "Search Completed", cancellationToken: default);
        await storage.UpdateProjectAsync(completed.Id, status: ProjectStatusEnum.Completed, cancellationToken: default);

        // Act: Search without status filter
        var results = await storage.SearchProjectsAsync("Search", statusFilter: null, cancellationToken: default);

        // Assert
        Assert.Equal(2, results.Count);

        _output.WriteLine($"Found {results.Count} projects when no status filter applied");

        // Cleanup
        await storage.DeleteProjectAsync(active.Id, default);
        await storage.DeleteProjectAsync(completed.Id, default);
        await storage.DeleteWorkspaceAsync(workspace.Id, default);
    }

    [Fact]
    public async Task GetProjectPath_ReturnsFullHierarchy()
    {
        var storage = _services.GetRequiredService<IStorage>();

        // Arrange: Create nested workspace with nested project
        var rootWs = await storage.CreateWorkspaceAsync("Engineering", cancellationToken: default);
        var childWs = await storage.CreateWorkspaceAsync("Memorizer", parentId: rootWs.Id, cancellationToken: default);
        var parentProj = await storage.CreateProjectAsync(childWs.Id, "V2 Release", cancellationToken: default);
        var childProj = await storage.CreateProjectAsync(childWs.Id, "Search Feature", parentId: parentProj.Id, cancellationToken: default);

        // Act
        var path = await storage.GetProjectPathAsync(childProj.Id, cancellationToken: default);

        // Assert
        Assert.Equal(2, path.WorkspacePath.Count); // Engineering, Memorizer
        Assert.Equal("Engineering", path.WorkspacePath[0].Name);
        Assert.Equal("Memorizer", path.WorkspacePath[1].Name);
        Assert.Single(path.ProjectAncestors); // V2 Release
        Assert.Equal("V2 Release", path.ProjectAncestors[0].Name);

        var fullPath = path.GetFullPath("Search Feature");
        Assert.Equal("Engineering > Memorizer > V2 Release > Search Feature", fullPath);

        _output.WriteLine($"Full path: {fullPath}");

        // Cleanup
        await storage.DeleteProjectAsync(childProj.Id, default);
        await storage.DeleteProjectAsync(parentProj.Id, default);
        await storage.DeleteWorkspaceAsync(childWs.Id, default);
        await storage.DeleteWorkspaceAsync(rootWs.Id, default);
    }

    [Fact]
    public async Task GetProjectPath_RootProject_HasEmptyProjectPath()
    {
        var storage = _services.GetRequiredService<IStorage>();

        // Arrange
        var workspace = await storage.CreateWorkspaceAsync("Test", cancellationToken: default);
        var project = await storage.CreateProjectAsync(workspace.Id, "Root Project", cancellationToken: default);

        // Act
        var path = await storage.GetProjectPathAsync(project.Id, cancellationToken: default);

        // Assert
        Assert.Single(path.WorkspacePath); // Just "Test"
        Assert.Empty(path.ProjectAncestors); // No parent projects

        _output.WriteLine("Root project correctly has empty project ancestor path");

        // Cleanup
        await storage.DeleteProjectAsync(project.Id, default);
        await storage.DeleteWorkspaceAsync(workspace.Id, default);
    }

    [Fact]
    public async Task SearchProjects_AcrossMultipleWorkspaces()
    {
        var storage = _services.GetRequiredService<IStorage>();

        // Arrange: Create projects with same name in different workspaces
        var ws1 = await storage.CreateWorkspaceAsync("Workspace A", cancellationToken: default);
        var ws2 = await storage.CreateWorkspaceAsync("Workspace B", cancellationToken: default);
        var proj1 = await storage.CreateProjectAsync(ws1.Id, "Common Project", cancellationToken: default);
        var proj2 = await storage.CreateProjectAsync(ws2.Id, "Common Project", cancellationToken: default);

        // Act
        var results = await storage.SearchProjectsAsync("Common", cancellationToken: default);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.WorkspacePath.Any(w => w.Name == "Workspace A"));
        Assert.Contains(results, r => r.WorkspacePath.Any(w => w.Name == "Workspace B"));

        _output.WriteLine($"Found projects in different workspaces: {string.Join(", ", results.Select(r => r.FullPath))}");

        // Cleanup
        await storage.DeleteProjectAsync(proj1.Id, default);
        await storage.DeleteProjectAsync(proj2.Id, default);
        await storage.DeleteWorkspaceAsync(ws1.Id, default);
        await storage.DeleteWorkspaceAsync(ws2.Id, default);
    }

    #endregion
}
