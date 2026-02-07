using Memorizer.Extensions;
using Memorizer.Models;
using Memorizer.Models.Enums;
using Memorizer.Models.ValueTypes;
using Memorizer.Services;
using Memorizer.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Memorizer.IntegrationTests;

/// <summary>
/// Integration tests for archived memory functionality.
/// Tests that archived memories are correctly filtered from project/workspace counts
/// and that GetArchivedMemoriesAsync works with project filtering.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class ArchivedMemoryTests : IDisposable
{
    private readonly IntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly IServiceProvider _serviceProvider;

    public ArchivedMemoryTests(IntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Storage"] = _fixture.PostgresConnectionString,
                ["Embeddings:ApiUrl"] = _fixture.OllamaApiUrl,
                ["Embeddings:Model"] = "all-minilm",
                ["Embeddings:Timeout"] = TimeSpan.FromMinutes(1).ToString()
            })
            .Build();

        services.AddSingleton<IConfiguration>(config);

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
        services.AddLogging();
    }

    [Fact]
    public async Task GetArchivedMemoriesAsync_WithProjectFilter_ReturnsOnlyArchivedMemoriesInProject()
    {
        // Arrange
        var storage = _serviceProvider.GetRequiredService<IStorage>();
        var createdMemories = new List<MemoryId>();
        WorkspaceId? workspaceId = null;
        ProjectId? projectId = null;

        try
        {
            // Create a workspace and project
            var workspace = await storage.CreateWorkspaceAsync(
                $"ArchivedTest-Workspace-{Guid.NewGuid():N}",
                "Test workspace for archived memory tests"
            );
            workspaceId = workspace.Id;

            var project = await storage.CreateProjectAsync(
                workspace.Id,
                $"ArchivedTest-Project-{Guid.NewGuid():N}",
                "Test project for archived memory tests"
            );
            projectId = project.Id;

            var projectOwner = MemoryOwner.ForProject(projectId.Value);

            // Create memories in the project - some will be archived, some won't
            var activeMemory1 = await storage.StoreMemory(
                type: "test-type",
                content: "Active memory 1 in project",
                source: "test",
                tags: new[] { "archived-test" },
                confidence: new Confidence(1.0),
                title: "Active Memory 1",
                owner: projectOwner
            );
            createdMemories.Add(activeMemory1.Id);

            var activeMemory2 = await storage.StoreMemory(
                type: "test-type",
                content: "Active memory 2 in project",
                source: "test",
                tags: new[] { "archived-test" },
                confidence: new Confidence(1.0),
                title: "Active Memory 2",
                owner: projectOwner
            );
            createdMemories.Add(activeMemory2.Id);

            var memoryToArchive = await storage.StoreMemory(
                type: "test-type",
                content: "Memory to be archived in project",
                source: "test",
                tags: new[] { "archived-test" },
                confidence: new Confidence(1.0),
                title: "Archived Memory",
                owner: projectOwner
            );
            createdMemories.Add(memoryToArchive.Id);

            // Archive one of the memories
            await storage.UpdateMemoryArchetypeAsync(memoryToArchive.Id, ArchetypeEnum.Archived);

            // Act - Get archived memories filtered by project
            var (archivedMemories, totalCount) = await storage.GetArchivedMemoriesAsync(
                page: 1,
                pageSize: 50,
                projectId: projectId
            );

            // Assert
            Assert.Equal(1, totalCount);
            Assert.Single(archivedMemories);
            Assert.Equal(memoryToArchive.Id, archivedMemories[0].Id);
            Assert.Equal(ArchetypeEnum.Archived, archivedMemories[0].Archetype);

            _output.WriteLine($"Successfully retrieved archived memories filtered by project. Found {totalCount} archived memory.");
        }
        finally
        {
            // Clean up
            foreach (var memoryId in createdMemories)
            {
                await storage.Delete(memoryId, CancellationToken.None);
            }
            if (projectId.HasValue)
            {
                await storage.DeleteProjectAsync(projectId.Value);
            }
            if (workspaceId.HasValue)
            {
                await storage.DeleteWorkspaceAsync(workspaceId.Value);
            }
        }
    }

    [Fact]
    public async Task GetMemoryCountByOwnerAsync_ExcludesArchivedMemories()
    {
        // Arrange
        var storage = _serviceProvider.GetRequiredService<IStorage>();
        var createdMemories = new List<MemoryId>();
        WorkspaceId? workspaceId = null;
        ProjectId? projectId = null;

        try
        {
            // Create a workspace and project
            var workspace = await storage.CreateWorkspaceAsync(
                $"CountTest-Workspace-{Guid.NewGuid():N}",
                "Test workspace for memory count tests"
            );
            workspaceId = workspace.Id;

            var project = await storage.CreateProjectAsync(
                workspace.Id,
                $"CountTest-Project-{Guid.NewGuid():N}",
                "Test project for memory count tests"
            );
            projectId = project.Id;

            var projectOwner = MemoryOwner.ForProject(projectId.Value);

            // Create 3 active memories
            for (int i = 0; i < 3; i++)
            {
                var memory = await storage.StoreMemory(
                    type: "test-type",
                    content: $"Active memory {i + 1}",
                    source: "test",
                    tags: new[] { "count-test" },
                    confidence: new Confidence(1.0),
                    title: $"Active Memory {i + 1}",
                    owner: projectOwner
                );
                createdMemories.Add(memory.Id);
            }

            // Create 2 memories that will be archived
            var memoriesToArchive = new List<MemoryId>();
            for (int i = 0; i < 2; i++)
            {
                var memory = await storage.StoreMemory(
                    type: "test-type",
                    content: $"Memory to archive {i + 1}",
                    source: "test",
                    tags: new[] { "count-test" },
                    confidence: new Confidence(1.0),
                    title: $"To Archive {i + 1}",
                    owner: projectOwner
                );
                createdMemories.Add(memory.Id);
                memoriesToArchive.Add(memory.Id);
            }

            // Archive the 2 memories
            foreach (var memoryId in memoriesToArchive)
            {
                await storage.UpdateMemoryArchetypeAsync(memoryId, ArchetypeEnum.Archived);
            }

            // Act - Get memory count for the project
            var count = await storage.GetMemoryCountByOwnerAsync(projectOwner);

            // Assert - Should only count the 3 active memories, not the 2 archived ones
            Assert.Equal(3, count);

            _output.WriteLine($"Memory count correctly excludes archived memories. Active count: {count}, Total created: 5");
        }
        finally
        {
            // Clean up
            foreach (var memoryId in createdMemories)
            {
                await storage.Delete(memoryId, CancellationToken.None);
            }
            if (projectId.HasValue)
            {
                await storage.DeleteProjectAsync(projectId.Value);
            }
            if (workspaceId.HasValue)
            {
                await storage.DeleteWorkspaceAsync(workspaceId.Value);
            }
        }
    }

    [Fact]
    public async Task GetMemoriesByOwnerAsync_ExcludesArchivedMemories()
    {
        // Arrange
        var storage = _serviceProvider.GetRequiredService<IStorage>();
        var createdMemories = new List<MemoryId>();
        WorkspaceId? workspaceId = null;
        ProjectId? projectId = null;

        try
        {
            // Create a workspace and project
            var workspace = await storage.CreateWorkspaceAsync(
                $"ListTest-Workspace-{Guid.NewGuid():N}",
                "Test workspace for memory list tests"
            );
            workspaceId = workspace.Id;

            var project = await storage.CreateProjectAsync(
                workspace.Id,
                $"ListTest-Project-{Guid.NewGuid():N}",
                "Test project for memory list tests"
            );
            projectId = project.Id;

            var projectOwner = MemoryOwner.ForProject(projectId.Value);

            // Create 3 active memories
            var activeMemoryIds = new List<MemoryId>();
            for (int i = 0; i < 3; i++)
            {
                var memory = await storage.StoreMemory(
                    type: "test-type",
                    content: $"Active memory {i + 1}",
                    source: "test",
                    tags: new[] { "list-test" },
                    confidence: new Confidence(1.0),
                    title: $"Active Memory {i + 1}",
                    owner: projectOwner
                );
                createdMemories.Add(memory.Id);
                activeMemoryIds.Add(memory.Id);
            }

            // Create 2 memories that will be archived
            var archivedMemoryIds = new List<MemoryId>();
            for (int i = 0; i < 2; i++)
            {
                var memory = await storage.StoreMemory(
                    type: "test-type",
                    content: $"Memory to archive {i + 1}",
                    source: "test",
                    tags: new[] { "list-test" },
                    confidence: new Confidence(1.0),
                    title: $"To Archive {i + 1}",
                    owner: projectOwner
                );
                createdMemories.Add(memory.Id);
                archivedMemoryIds.Add(memory.Id);
            }

            // Archive the 2 memories
            foreach (var memoryId in archivedMemoryIds)
            {
                await storage.UpdateMemoryArchetypeAsync(memoryId, ArchetypeEnum.Archived);
            }

            // Act - Get memories for the project owner
            var memories = await storage.GetMemoriesByOwnerAsync(projectOwner, page: 1, pageSize: 50);

            // Assert - Should only return the 3 active memories, not the 2 archived ones
            Assert.Equal(3, memories.Count);

            // Verify only active memory IDs are returned
            var returnedIds = memories.Select(m => m.Id).ToHashSet();
            foreach (var activeId in activeMemoryIds)
            {
                Assert.Contains(activeId, returnedIds);
            }
            foreach (var archivedId in archivedMemoryIds)
            {
                Assert.DoesNotContain(archivedId, returnedIds);
            }

            _output.WriteLine($"GetMemoriesByOwnerAsync correctly excludes archived memories. Returned {memories.Count} active memories.");
        }
        finally
        {
            // Clean up
            foreach (var memoryId in createdMemories)
            {
                await storage.Delete(memoryId, CancellationToken.None);
            }
            if (projectId.HasValue)
            {
                await storage.DeleteProjectAsync(projectId.Value);
            }
            if (workspaceId.HasValue)
            {
                await storage.DeleteWorkspaceAsync(workspaceId.Value);
            }
        }
    }

    [Fact]
    public async Task GetArchivedMemoriesAsync_WithoutFilter_ReturnsAllArchivedMemories()
    {
        // Arrange
        var storage = _serviceProvider.GetRequiredService<IStorage>();
        var createdMemories = new List<MemoryId>();
        var archivedMemoryIds = new List<MemoryId>();

        try
        {
            // Create some memories and archive them (in Unfiled)
            for (int i = 0; i < 3; i++)
            {
                var memory = await storage.StoreMemory(
                    type: "test-type",
                    content: $"Unfiled memory to archive {i + 1}",
                    source: "test",
                    tags: new[] { "archived-all-test" },
                    confidence: new Confidence(1.0),
                    title: $"Unfiled Archived {i + 1}"
                );
                createdMemories.Add(memory.Id);
                archivedMemoryIds.Add(memory.Id);
            }

            // Archive all of them
            foreach (var memoryId in archivedMemoryIds)
            {
                await storage.UpdateMemoryArchetypeAsync(memoryId, ArchetypeEnum.Archived);
            }

            // Act - Get all archived memories without filter
            var (archivedMemories, totalCount) = await storage.GetArchivedMemoriesAsync(
                page: 1,
                pageSize: 50,
                projectId: null
            );

            // Assert - Should return at least our 3 archived memories
            Assert.True(totalCount >= 3, $"Expected at least 3 archived memories, got {totalCount}");

            // Verify our archived memories are in the results
            var returnedIds = archivedMemories.Select(m => m.Id).ToHashSet();
            foreach (var archivedId in archivedMemoryIds)
            {
                Assert.Contains(archivedId, returnedIds);
            }

            _output.WriteLine($"GetArchivedMemoriesAsync without filter returned {totalCount} total archived memories.");
        }
        finally
        {
            // Clean up
            foreach (var memoryId in createdMemories)
            {
                await storage.Delete(memoryId, CancellationToken.None);
            }
        }
    }

    public void Dispose()
    {
        (_serviceProvider as IDisposable)?.Dispose();
    }
}
