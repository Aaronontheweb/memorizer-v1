using System.Text.Json.Serialization;
using Memorizer.Models.Enums;

namespace Memorizer.Models;

/// <summary>
/// Polymorphic owner reference for memories.
/// Encapsulates the owner_type + owner_id pattern from the database.
/// </summary>
public readonly record struct MemoryOwner
{
    /// <summary>
    /// The type of entity that owns this memory.
    /// </summary>
    public required OwnerTypeEnum Type { get; init; }

    /// <summary>
    /// The ID of the owning entity (workspace or project).
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Well-known UUID for the Unfiled workspace.
    /// All memories not assigned to a specific workspace/project belong here.
    /// </summary>
    public static readonly Guid UnfiledWorkspaceId = Guid.Empty;

    /// <summary>
    /// Represents the Unfiled workspace owner (default for unorganized memories).
    /// </summary>
    public static MemoryOwner Unfiled => new() { Type = OwnerTypeEnum.Workspace, Id = UnfiledWorkspaceId };

    /// <summary>
    /// Well-known UUID for the System Memories workspace.
    /// All system-generated index memories (for project/workspace search) belong here.
    /// </summary>
    public static readonly Guid SystemMemoriesWorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Represents the System Memories workspace owner (for system-generated index memories).
    /// </summary>
    public static MemoryOwner SystemMemories => new() { Type = OwnerTypeEnum.Workspace, Id = SystemMemoriesWorkspaceId };

    /// <summary>
    /// Creates a MemoryOwner for a specific workspace.
    /// </summary>
    public static MemoryOwner ForWorkspace(WorkspaceId id) => new() { Type = OwnerTypeEnum.Workspace, Id = id.Value };

    /// <summary>
    /// Creates a MemoryOwner for a specific project.
    /// </summary>
    public static MemoryOwner ForProject(ProjectId id) => new() { Type = OwnerTypeEnum.Project, Id = id.Value };

    /// <summary>
    /// Returns true if this owner is the Unfiled workspace.
    /// </summary>
    [JsonIgnore]
    public bool IsUnfiled => Type == OwnerTypeEnum.Workspace && Id == UnfiledWorkspaceId;

    /// <summary>
    /// Returns true if this owner is the System Memories workspace.
    /// </summary>
    [JsonIgnore]
    public bool IsSystemMemories => Type == OwnerTypeEnum.Workspace && Id == SystemMemoriesWorkspaceId;

    /// <summary>
    /// Returns the WorkspaceId if this is a workspace owner, null otherwise.
    /// </summary>
    [JsonIgnore]
    public WorkspaceId? WorkspaceId => Type == OwnerTypeEnum.Workspace ? new WorkspaceId(Id) : null;

    /// <summary>
    /// Returns the ProjectId if this is a project owner, null otherwise.
    /// </summary>
    [JsonIgnore]
    public ProjectId? ProjectId => Type == OwnerTypeEnum.Project ? new ProjectId(Id) : null;

    public override string ToString() => $"{Type}:{Id}";
}
