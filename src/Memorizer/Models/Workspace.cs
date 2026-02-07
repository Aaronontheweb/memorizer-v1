using System.Text.Json;
using Memorizer.Models.Enums;

namespace Memorizer.Models;

/// <summary>
/// Persistent container for organizing memories.
/// Represents products, teams, or areas of focus.
/// Workspaces are rarely closed and support unlimited nesting.
/// </summary>
public class Workspace
{
    public WorkspaceId Id { get; init; }

    /// <summary>
    /// Parent workspace for nested hierarchy. Null for root workspaces.
    /// </summary>
    public WorkspaceId? ParentId { get; init; }

    /// <summary>
    /// Human-readable name of the workspace.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// URL-safe identifier, unique within parent scope.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Optional description of the workspace purpose.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// True for system-managed workspaces (e.g., Unfiled).
    /// System workspaces cannot be deleted by users.
    /// </summary>
    public bool IsSystem { get; init; }

    /// <summary>
    /// Workspace-specific configuration settings.
    /// </summary>
    public JsonDocument? Settings { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Well-known ID for the Unfiled workspace.
    /// </summary>
    public static readonly WorkspaceId UnfiledId = new(Guid.Empty);
}

/// <summary>
/// Finite work item with lifecycle, completion criteria, and victory conditions.
/// Projects belong to a workspace and can be nested within other projects.
/// Key difference from workspaces: projects have status and completion dates.
/// </summary>
public class Project
{
    public ProjectId Id { get; init; }

    /// <summary>
    /// Parent workspace containing this project.
    /// </summary>
    public required WorkspaceId WorkspaceId { get; init; }

    /// <summary>
    /// Parent project for nested hierarchy. Null for root projects.
    /// </summary>
    public ProjectId? ParentId { get; init; }

    /// <summary>
    /// Human-readable name of the project.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// URL-safe identifier, unique within workspace/parent scope.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Optional description of the project scope.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Lifecycle status: draft → active → on_hold → completed/cancelled → archived
    /// </summary>
    public required ProjectStatusEnum Status { get; init; }

    /// <summary>
    /// Markdown description of completion criteria.
    /// UI/agent can parse this into a checklist for tracking.
    /// </summary>
    public string? VictoryConditions { get; init; }

    /// <summary>
    /// Project-specific configuration settings.
    /// </summary>
    public JsonDocument? Settings { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Timestamp when project reached completed/cancelled status.
    /// </summary>
    public DateTime? CompletedAt { get; init; }
}

/// <summary>
/// A segment in a workspace path (for breadcrumb navigation).
/// </summary>
public readonly record struct WorkspacePathSegment(WorkspaceId Id, string Name);

/// <summary>
/// Result from searching workspaces, includes the workspace and its ancestor path.
/// </summary>
public class WorkspaceSearchResult
{
    /// <summary>
    /// The matching workspace.
    /// </summary>
    public required Workspace Workspace { get; init; }

    /// <summary>
    /// Ancestor path from root to parent (not including this workspace).
    /// Empty for root workspaces.
    /// </summary>
    public required IReadOnlyList<WorkspacePathSegment> Path { get; init; }

    /// <summary>
    /// Depth in the hierarchy (0 = root).
    /// </summary>
    public int Depth => Path.Count;

    /// <summary>
    /// Full path as a display string (e.g., "Engineering > Memorizer").
    /// </summary>
    public string FullPath => Path.Count == 0
        ? Workspace.Name
        : string.Join(" > ", Path.Select(p => p.Name).Append(Workspace.Name));
}

/// <summary>
/// A segment in a project path (can be workspace or project).
/// </summary>
public readonly record struct ProjectPathSegment(
    Guid Id,
    string Name,
    bool IsWorkspace // true = workspace, false = project
);

/// <summary>
/// Full path to a project including workspace ancestry and project ancestry.
/// </summary>
public class ProjectPath
{
    /// <summary>
    /// Workspace ancestors from root to the containing workspace.
    /// </summary>
    public required IReadOnlyList<WorkspacePathSegment> WorkspacePath { get; init; }

    /// <summary>
    /// Project ancestors from root project to parent project (not including this project).
    /// Empty for root-level projects.
    /// </summary>
    public required IReadOnlyList<ProjectPathSegment> ProjectAncestors { get; init; }

    /// <summary>
    /// Full path as a display string (e.g., "Engineering > Sdkbin > Billing System V2").
    /// </summary>
    public string GetFullPath(string projectName)
    {
        var segments = WorkspacePath.Select(w => w.Name)
            .Concat(ProjectAncestors.Select(p => p.Name))
            .Append(projectName);
        return string.Join(" > ", segments);
    }
}

/// <summary>
/// Result from searching projects, includes the project and its full path.
/// </summary>
public class ProjectSearchResult
{
    /// <summary>
    /// The matching project.
    /// </summary>
    public required Project Project { get; init; }

    /// <summary>
    /// Workspace ancestors from root to the containing workspace.
    /// </summary>
    public required IReadOnlyList<WorkspacePathSegment> WorkspacePath { get; init; }

    /// <summary>
    /// Project ancestors from root project to parent project (not including this project).
    /// Empty for root-level projects.
    /// </summary>
    public required IReadOnlyList<ProjectPathSegment> ProjectPath { get; init; }

    /// <summary>
    /// Full path as a display string (e.g., "Engineering > Sdkbin > Billing System V2").
    /// </summary>
    public string FullPath
    {
        get
        {
            var segments = WorkspacePath.Select(w => w.Name)
                .Concat(ProjectPath.Select(p => p.Name))
                .Append(Project.Name);
            return string.Join(" > ", segments);
        }
    }
}
