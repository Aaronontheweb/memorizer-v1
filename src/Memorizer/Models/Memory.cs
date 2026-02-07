using System.Text.Json;
using Memorizer.Models.Enums;
using Memorizer.Models.ValueTypes;
using Pgvector;

namespace Memorizer.Models;

public class Memory
{
    public MemoryId Id { get; init; }

    /// <summary>
    /// Deprecated: Original freeform type field, preserved for backwards compatibility.
    /// Use MemoryType property for structured type access.
    /// </summary>
    /// <remarks>
    /// Maps to 'type_legacy' column in database (renamed from 'type' in migration 014).
    /// </remarks>
    public string? TypeLegacy { get; init; }

    /// <summary>
    /// Backwards-compatible accessor for TypeLegacy.
    /// New code should use MemoryType enum instead.
    /// </summary>
    /// <remarks>
    /// TODO: Add [Obsolete] attribute after migrating all usages to MemoryType.
    /// Currently 16 usages in MemoryTools.cs, MemoryController.cs, TitleGenerationActor.cs, and Services/Memory.cs.
    /// </remarks>
    public string Type
    {
        get => TypeLegacy ?? string.Empty;
        init => TypeLegacy = value;
    }

    public JsonDocument Content { get; init; } = JsonDocument.Parse("{}");
    public string Source { get; init; } = string.Empty;
    public Vector? Embedding { get; init; } // Nullable during dimension migration when embeddings are being regenerated
    public Vector? EmbeddingMetadata { get; init; } = null;
    public string[]? Tags { get; init; }
    public Confidence Confidence { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string? Title { get; init; }
    public string Text { get; init; } = string.Empty;
    public VersionNumber CurrentVersion { get; init; } = VersionNumber.Initial;
    public SimilarityScore? Similarity { get; set; }
    public List<MemoryRelationship>? Relationships { get; set; }

    /// <summary>
    /// Polymorphic owner reference (workspace or project).
    /// All memories belong to either a workspace or a project.
    /// </summary>
    public MemoryOwner Owner { get; init; } = MemoryOwner.Unfiled;

    /// <summary>
    /// Structured memory type classification.
    /// </summary>
    public MemoryTypeEnum MemoryType { get; init; } = MemoryTypeEnum.Reference;

    /// <summary>
    /// Archetype: Document (living, editable) or Record (historical, immutable).
    /// </summary>
    public ArchetypeEnum Archetype { get; init; } = ArchetypeEnum.Document;
}
