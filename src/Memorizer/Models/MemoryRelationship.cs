using Memorizer.Models.ValueTypes;

namespace Memorizer.Models;

public class MemoryRelationship
{
    public RelationshipId Id { get; init; }
    public MemoryId FromMemoryId { get; init; }
    public MemoryId ToMemoryId { get; init; }
    public string Type { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public VersionNumber? CreatedInVersion { get; init; }
    public VersionNumber? DeletedInVersion { get; init; }
    public string? RelatedMemoryTitle { get; set; }
    public string? RelatedMemoryType { get; set; }

    /// <summary>
    /// Similarity score (0.0 to 1.0) for 'similar-to' relationships.
    /// Null for other relationship types.
    /// </summary>
    public SimilarityScore? Score { get; init; }

    /// <summary>
    /// Indicates whether the target memory (ToMemoryId) is archived.
    /// Used to signal when relationships point to obsolete content.
    /// </summary>
    public bool TargetArchived { get; init; }
} 