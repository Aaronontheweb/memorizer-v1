using System.Text.Json;
using Memorizer.Models.ValueTypes;

namespace Memorizer.Models;

public class MemoryVersion
{
    public VersionId VersionId { get; init; }
    public MemoryId MemoryId { get; init; }
    public VersionNumber VersionNumber { get; init; }
    public string Type { get; init; } = string.Empty;
    public JsonDocument Content { get; init; } = JsonDocument.Parse("{}");
    public string Text { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string[]? Tags { get; init; }
    public Confidence Confidence { get; init; }
    public string? Title { get; init; }
    public RelationshipId[] RelationshipIds { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime VersionedAt { get; init; }

    public List<MemoryEvent>? Events { get; set; }
}
