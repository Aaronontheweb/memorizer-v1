using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memorizer.Models;
using Memorizer.Models.Enums;
using Memorizer.Models.ValueTypes;
using Registrator.Net;

namespace Memorizer.Services;

/// <summary>
/// Service for seeding sample data from a JSON file for local development.
/// </summary>
public interface ISeedDataService
{
    /// <summary>
    /// Seeds sample data if enabled and database is fresh.
    /// </summary>
    Task SeedAsync(CancellationToken cancellationToken = default);
}

[AutoRegisterInterfaces(ServiceLifetime.Scoped)]
public class SeedDataService : ISeedDataService
{
    private readonly IStorage _storage;
    private readonly ILogger<SeedDataService> _logger;
    private readonly IConfiguration _configuration;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public SeedDataService(IStorage storage, ILogger<SeedDataService> logger, IConfiguration configuration)
    {
        _storage = storage;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var seedingEnabled = _configuration.GetValue<bool>("Seeding:Enabled");
        if (!seedingEnabled)
        {
            _logger.LogDebug("Seeding is disabled, skipping sample data");
            return;
        }

        // Check if data already exists (fresh instance detection)
        // Must check BOTH workspaces AND memories - after migration from an older schema,
        // the workspaces table will be fresh but memories may already exist from a backup restore
        var existingWorkspaces = await _storage.GetWorkspacesAsync(includeSystem: false, cancellationToken: cancellationToken);
        if (existingWorkspaces.Count > 0)
        {
            _logger.LogInformation("Existing data detected ({Count} workspaces), skipping seed", existingWorkspaces.Count);
            return;
        }

        // Also check for existing memories - crucial for database migrations/restores
        var (_, memoryCount) = await _storage.GetMemoriesPaginated(page: 1, pageSize: 1, cancellationToken: cancellationToken);
        if (memoryCount > 0)
        {
            _logger.LogInformation("Existing memories detected ({Count} memories), skipping seed", memoryCount);
            return;
        }

        _logger.LogInformation("Fresh instance detected, seeding sample data for local development...");

        try
        {
            var seedData = await LoadSeedDataAsync(cancellationToken);
            if (seedData == null)
            {
                _logger.LogWarning("Failed to load seed data file");
                return;
            }

            var workspaceMap = await CreateWorkspacesAsync(seedData.Workspaces, cancellationToken);
            var projectMap = await CreateProjectsAsync(seedData.Projects, workspaceMap, cancellationToken);
            var memoryMap = await CreateMemoriesAsync(seedData.Memories, projectMap, cancellationToken);
            await CreateRelationshipsAsync(seedData.Relationships, memoryMap, cancellationToken);

            _logger.LogInformation(
                "Seed data creation complete! Created: {Workspaces} workspaces, {Projects} projects, {Memories} memories, {Relationships} relationships",
                workspaceMap.Count, projectMap.Count, memoryMap.Count, seedData.Relationships?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed sample data: {Message}", ex.Message);
            // Don't fail startup - seeding is optional for development
        }
    }

    private async Task<SeedDataFile?> LoadSeedDataAsync(CancellationToken cancellationToken)
    {
        // Try loading from embedded resource first
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Memorizer.SeedData.seed-data.json";

        await using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            _logger.LogWarning("Seed data resource not found: {ResourceName}", resourceName);
            return null;
        }

        return await JsonSerializer.DeserializeAsync<SeedDataFile>(stream, JsonOptions, cancellationToken);
    }

    private async Task<Dictionary<string, WorkspaceId>> CreateWorkspacesAsync(
        List<SeedWorkspace>? workspaces,
        CancellationToken cancellationToken)
    {
        var workspaceMap = new Dictionary<string, WorkspaceId>(StringComparer.OrdinalIgnoreCase);

        if (workspaces == null) return workspaceMap;

        foreach (var ws in workspaces)
        {
            await CreateWorkspaceRecursiveAsync(ws, null, "", workspaceMap, cancellationToken);
        }

        return workspaceMap;
    }

    private async Task CreateWorkspaceRecursiveAsync(
        SeedWorkspace ws,
        WorkspaceId? parentId,
        string pathPrefix,
        Dictionary<string, WorkspaceId> workspaceMap,
        CancellationToken cancellationToken)
    {
        var workspace = await _storage.CreateWorkspaceAsync(
            ws.Name,
            ws.Description,
            parentId,
            cancellationToken);

        var path = string.IsNullOrEmpty(pathPrefix) ? ws.Slug : $"{pathPrefix}/{ws.Slug}";
        workspaceMap[path] = workspace.Id;

        _logger.LogInformation("Created workspace: {Name} ({Path})", workspace.Name, path);

        // Create child workspaces
        if (ws.Children != null)
        {
            foreach (var child in ws.Children)
            {
                await CreateWorkspaceRecursiveAsync(child, workspace.Id, path, workspaceMap, cancellationToken);
            }
        }
    }

    private async Task<Dictionary<string, ProjectId>> CreateProjectsAsync(
        List<SeedProject>? projects,
        Dictionary<string, WorkspaceId> workspaceMap,
        CancellationToken cancellationToken)
    {
        var projectMap = new Dictionary<string, ProjectId>(StringComparer.OrdinalIgnoreCase);

        if (projects == null) return projectMap;

        foreach (var proj in projects)
        {
            if (!workspaceMap.TryGetValue(proj.Workspace, out var workspaceId))
            {
                _logger.LogWarning("Workspace not found for project {Project}: {Workspace}", proj.Name, proj.Workspace);
                continue;
            }

            var project = await _storage.CreateProjectAsync(
                workspaceId,
                proj.Name,
                proj.Description,
                cancellationToken: cancellationToken);

            // Update status if not draft
            var status = ParseProjectStatus(proj.Status);
            if (status != ProjectStatusEnum.Draft)
            {
                await _storage.UpdateProjectAsync(project.Id, status: status, cancellationToken: cancellationToken);
            }

            var path = $"{proj.Workspace}/{proj.Slug}";
            projectMap[path] = project.Id;

            _logger.LogInformation("Created project: {Name} ({Path}) - {Status}", project.Name, path, status);
        }

        return projectMap;
    }

    private async Task<Dictionary<string, MemoryId>> CreateMemoriesAsync(
        List<SeedMemory>? memories,
        Dictionary<string, ProjectId> projectMap,
        CancellationToken cancellationToken)
    {
        var memoryMap = new Dictionary<string, MemoryId>(StringComparer.OrdinalIgnoreCase);

        if (memories == null) return memoryMap;

        foreach (var mem in memories)
        {
            MemoryOwner? owner = null;

            if (!string.IsNullOrEmpty(mem.Project))
            {
                if (projectMap.TryGetValue(mem.Project, out var projectId))
                {
                    owner = MemoryOwner.ForProject(projectId);
                }
                else
                {
                    _logger.LogWarning("Project not found for memory {Memory}: {Project}", mem.Title, mem.Project);
                }
            }
            // If project is null, memory will be unfiled

            var memory = await _storage.StoreMemory(
                type: mem.Type,
                content: mem.Content,
                source: "system",
                tags: mem.Tags?.ToArray(),
                confidence: new Confidence(mem.Confidence),
                title: mem.Title,
                owner: owner,
                cancellationToken: cancellationToken);

            memoryMap[mem.Id] = memory.Id;

            var location = string.IsNullOrEmpty(mem.Project) ? "unfiled" : mem.Project;
            _logger.LogDebug("Created memory: {Title} ({Location})", mem.Title, location);
        }

        _logger.LogInformation("Created {Count} memories", memoryMap.Count);
        return memoryMap;
    }

    private async Task CreateRelationshipsAsync(
        List<SeedRelationship>? relationships,
        Dictionary<string, MemoryId> memoryMap,
        CancellationToken cancellationToken)
    {
        if (relationships == null || relationships.Count == 0) return;

        foreach (var rel in relationships)
        {
            if (!memoryMap.TryGetValue(rel.From, out var fromId))
            {
                _logger.LogWarning("Memory not found for relationship: {From}", rel.From);
                continue;
            }

            if (!memoryMap.TryGetValue(rel.To, out var toId))
            {
                _logger.LogWarning("Memory not found for relationship: {To}", rel.To);
                continue;
            }

            await _storage.CreateRelationship(fromId, toId, rel.Type, cancellationToken);
            _logger.LogDebug("Created relationship: {From} --[{Type}]--> {To}", rel.From, rel.Type, rel.To);
        }

        _logger.LogInformation("Created {Count} relationships", relationships.Count);
    }

    private static ProjectStatusEnum ParseProjectStatus(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "draft" => ProjectStatusEnum.Draft,
            "active" => ProjectStatusEnum.Active,
            "on_hold" or "onhold" => ProjectStatusEnum.OnHold,
            "completed" => ProjectStatusEnum.Completed,
            "cancelled" => ProjectStatusEnum.Cancelled,
            "archived" => ProjectStatusEnum.Archived,
            _ => ProjectStatusEnum.Draft
        };
    }
}

#region Seed Data Models

public class SeedDataFile
{
    public List<SeedWorkspace>? Workspaces { get; set; }
    public List<SeedProject>? Projects { get; set; }
    public List<SeedMemory>? Memories { get; set; }
    public List<SeedRelationship>? Relationships { get; set; }
}

public class SeedWorkspace
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public List<SeedWorkspace>? Children { get; set; }
}

public class SeedProject
{
    public required string Workspace { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
}

public class SeedMemory
{
    public required string Id { get; set; }
    public string? Project { get; set; }
    public required string Title { get; set; }
    public required string Type { get; set; }
    public required string Content { get; set; }
    public List<string>? Tags { get; set; }
    public double Confidence { get; set; } = 0.8;
}

public class SeedRelationship
{
    public required string From { get; set; }
    public required string To { get; set; }
    public required string Type { get; set; }
}

#endregion
