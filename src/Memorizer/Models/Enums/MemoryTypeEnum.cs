using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Memorizer.Models.Enums;

/// <summary>
/// Strongly-typed memory type taxonomy.
/// Replaces freeform string types with a structured enum.
/// </summary>
/// <remarks>
/// The enum values use EnumMember attributes to maintain backward compatibility
/// with existing database values (lowercase with hyphens).
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MemoryTypeEnum
{
    // Core V2 taxonomy types (from Memory Type Taxonomy Design)

    /// <summary>
    /// Repeatable task list to clone and execute.
    /// Example: Lead qualification, deployment checklist, weekly review
    /// </summary>
    [EnumMember(Value = "checklist")]
    Checklist,

    /// <summary>
    /// Record of specific work done. Immutable once written.
    /// Example: "Fixed racy test in PaymentService", debugging session notes
    /// </summary>
    [EnumMember(Value = "work-log")]
    WorkLog,

    /// <summary>
    /// Distilled best practice from experience. Evolves as understanding improves.
    /// Example: "How to debug race conditions", coding guidelines
    /// </summary>
    [EnumMember(Value = "standard")]
    Standard,

    /// <summary>
    /// Plan for how to execute work. Completed when work done.
    /// Example: Feature implementation plan, requirements doc
    /// </summary>
    [EnumMember(Value = "specification")]
    Specification,

    /// <summary>
    /// Track ongoing tasks. Living document with items added/removed.
    /// Example: Sprint backlog, personal tasks
    /// </summary>
    [EnumMember(Value = "todo-list")]
    TodoList,

    /// <summary>
    /// General knowledge, facts, how-tos. Stable, rarely edited.
    /// Example: API docs, config examples, tool usage
    /// </summary>
    [EnumMember(Value = "reference")]
    Reference,

    // Legacy/existing types for backward compatibility

    /// <summary>
    /// How-to guide or tutorial content.
    /// </summary>
    [EnumMember(Value = "how-to")]
    HowTo,

    /// <summary>
    /// General document content.
    /// </summary>
    [EnumMember(Value = "document")]
    Document,

    /// <summary>
    /// Conversation or chat transcript.
    /// </summary>
    [EnumMember(Value = "conversation")]
    Conversation,

    /// <summary>
    /// Custom type for extensibility. Use with CustomTypeName property.
    /// </summary>
    [EnumMember(Value = "custom")]
    Custom
}

/// <summary>
/// Extension methods for MemoryTypeEnum.
/// </summary>
public static class MemoryTypeEnumExtensions
{
    /// <summary>
    /// Gets the string value for the enum (as stored in database).
    /// </summary>
    public static string ToStringValue(this MemoryTypeEnum type)
    {
        return type switch
        {
            MemoryTypeEnum.Checklist => "checklist",
            MemoryTypeEnum.WorkLog => "work-log",
            MemoryTypeEnum.Standard => "standard",
            MemoryTypeEnum.Specification => "specification",
            MemoryTypeEnum.TodoList => "todo-list",
            MemoryTypeEnum.Reference => "reference",
            MemoryTypeEnum.HowTo => "how-to",
            MemoryTypeEnum.Document => "document",
            MemoryTypeEnum.Conversation => "conversation",
            MemoryTypeEnum.Custom => "custom",
            _ => "reference" // Default fallback
        };
    }

    /// <summary>
    /// Parses a string value to MemoryTypeEnum.
    /// Falls back to Custom for unknown values.
    /// </summary>
    public static MemoryTypeEnum ParseMemoryType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return MemoryTypeEnum.Reference;

        return value.ToLowerInvariant() switch
        {
            "checklist" => MemoryTypeEnum.Checklist,
            "work-log" or "worklog" => MemoryTypeEnum.WorkLog,
            "standard" => MemoryTypeEnum.Standard,
            "specification" or "spec" => MemoryTypeEnum.Specification,
            "todo-list" or "todolist" or "todo" => MemoryTypeEnum.TodoList,
            "reference" or "ref" => MemoryTypeEnum.Reference,
            "how-to" or "howto" => MemoryTypeEnum.HowTo,
            "document" or "doc" => MemoryTypeEnum.Document,
            "conversation" or "chat" => MemoryTypeEnum.Conversation,
            "custom" => MemoryTypeEnum.Custom,
            _ => MemoryTypeEnum.Custom // Unknown types map to Custom
        };
    }
}
