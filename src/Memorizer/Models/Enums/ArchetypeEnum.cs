using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Memorizer.Models.Enums;

/// <summary>
/// Memory archetype discriminator.
/// Determines whether a memory is a living document, a historical record, or archived/obsolete.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ArchetypeEnum
{
    /// <summary>
    /// Living, editable content that evolves over time.
    /// Example: Documentation, specifications, todo lists
    /// </summary>
    [EnumMember(Value = "document")]
    Document = 0,

    /// <summary>
    /// Historical, immutable record of past events or work.
    /// Example: Work logs, session notes, audit records
    /// </summary>
    [EnumMember(Value = "record")]
    Record = 1,

    /// <summary>
    /// Archived/obsolete memory. Excluded from default searches and relationship displays.
    /// Preserved for historical reference, audit trails, and consolidation provenance.
    /// Can be restored to Document or Record status if needed.
    /// </summary>
    [EnumMember(Value = "archived")]
    Archived = 2,

    /// <summary>
    /// System-generated memory for internal use. Hidden from normal user searches.
    /// Used for indexing project/workspace metadata to enable semantic search.
    /// Automatically created and maintained by the system when projects/workspaces are modified.
    /// </summary>
    [EnumMember(Value = "system")]
    System = 3
}

/// <summary>
/// Extension methods for ArchetypeEnum.
/// </summary>
public static class ArchetypeEnumExtensions
{
    /// <summary>
    /// Gets the string value for the enum (as stored in database).
    /// </summary>
    public static string ToStringValue(this ArchetypeEnum archetype)
    {
        return archetype switch
        {
            ArchetypeEnum.Document => "document",
            ArchetypeEnum.Record => "record",
            ArchetypeEnum.Archived => "archived",
            ArchetypeEnum.System => "system",
            _ => "document"
        };
    }

    /// <summary>
    /// Parses a string value to ArchetypeEnum.
    /// </summary>
    public static ArchetypeEnum ParseArchetype(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ArchetypeEnum.Document;

        return value.ToLowerInvariant() switch
        {
            "document" or "doc" => ArchetypeEnum.Document,
            "record" or "rec" or "log" => ArchetypeEnum.Record,
            "archived" or "archive" or "obsolete" => ArchetypeEnum.Archived,
            "system" or "sys" => ArchetypeEnum.System,
            _ => ArchetypeEnum.Document
        };
    }

    /// <summary>
    /// Returns true if the archetype represents mutable content.
    /// </summary>
    public static bool IsMutable(this ArchetypeEnum archetype)
    {
        return archetype == ArchetypeEnum.Document;
    }

    /// <summary>
    /// Returns true if the archetype represents active user-visible content.
    /// Excludes Archived and System memories.
    /// </summary>
    public static bool IsActive(this ArchetypeEnum archetype)
    {
        return archetype is ArchetypeEnum.Document or ArchetypeEnum.Record;
    }

    /// <summary>
    /// Returns true if the archetype represents archived/obsolete content.
    /// </summary>
    public static bool IsArchived(this ArchetypeEnum archetype)
    {
        return archetype == ArchetypeEnum.Archived;
    }

    /// <summary>
    /// Returns true if the archetype represents system-generated internal content.
    /// System memories are used for indexing and are hidden from normal searches.
    /// </summary>
    public static bool IsSystem(this ArchetypeEnum archetype)
    {
        return archetype == ArchetypeEnum.System;
    }

    /// <summary>
    /// Returns true if the archetype should be visible in normal user searches.
    /// Excludes Archived and System memories.
    /// </summary>
    public static bool IsSearchable(this ArchetypeEnum archetype)
    {
        return archetype.IsActive();
    }
}
