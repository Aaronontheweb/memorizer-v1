using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Memorizer.Models.Enums;

/// <summary>
/// Strongly-typed relationship type between memories.
/// Replaces the string constants in RelationshipType static class.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RelationshipTypeEnum
{
    /// <summary>
    /// Parent relationship - this memory is a parent of the target.
    /// </summary>
    [EnumMember(Value = "Parent")]
    Parent,

    /// <summary>
    /// Child relationship - this memory is a child of the target.
    /// </summary>
    [EnumMember(Value = "Child")]
    Child,

    /// <summary>
    /// Reference relationship - this memory references the target.
    /// </summary>
    [EnumMember(Value = "Reference")]
    Reference,

    /// <summary>
    /// Related relationship - general association between memories.
    /// </summary>
    [EnumMember(Value = "Related")]
    Related,

    /// <summary>
    /// Similar-to relationship - memories with similar content/embeddings.
    /// </summary>
    [EnumMember(Value = "similar-to")]
    SimilarTo,

    /// <summary>
    /// Cause relationship - this memory caused the target.
    /// </summary>
    [EnumMember(Value = "Cause")]
    Cause,

    /// <summary>
    /// Effect relationship - this memory is an effect of the target.
    /// </summary>
    [EnumMember(Value = "Effect")]
    Effect,

    /// <summary>
    /// Duplicate relationship - this memory is a duplicate of the target.
    /// </summary>
    [EnumMember(Value = "Duplicate")]
    Duplicate,

    /// <summary>
    /// Version-of relationship - this memory is a version of the target.
    /// </summary>
    [EnumMember(Value = "VersionOf")]
    VersionOf,

    /// <summary>
    /// Part-of relationship - this memory is part of the target.
    /// </summary>
    [EnumMember(Value = "PartOf")]
    PartOf,

    /// <summary>
    /// Contains relationship - this memory contains the target.
    /// </summary>
    [EnumMember(Value = "Contains")]
    Contains,

    /// <summary>
    /// Precedes relationship - this memory precedes the target in sequence.
    /// </summary>
    [EnumMember(Value = "Precedes")]
    Precedes,

    /// <summary>
    /// Follows relationship - this memory follows the target in sequence.
    /// </summary>
    [EnumMember(Value = "Follows")]
    Follows,

    /// <summary>
    /// Example-of relationship - this memory is an example of the target concept.
    /// </summary>
    [EnumMember(Value = "ExampleOf")]
    ExampleOf,

    /// <summary>
    /// Instance-of relationship - this memory is an instance of the target template.
    /// </summary>
    [EnumMember(Value = "InstanceOf")]
    InstanceOf,

    /// <summary>
    /// Generalizes relationship - this memory generalizes the target.
    /// </summary>
    [EnumMember(Value = "Generalizes")]
    Generalizes,

    /// <summary>
    /// Specializes relationship - this memory specializes the target.
    /// </summary>
    [EnumMember(Value = "Specializes")]
    Specializes,

    /// <summary>
    /// Synonym relationship - memories with the same meaning.
    /// </summary>
    [EnumMember(Value = "Synonym")]
    Synonym,

    /// <summary>
    /// Antonym relationship - memories with opposite meanings.
    /// </summary>
    [EnumMember(Value = "Antonym")]
    Antonym,

    /// <summary>
    /// Refines relationship - this memory refines/improves upon the target.
    /// </summary>
    [EnumMember(Value = "Refines")]
    Refines,

    /// <summary>
    /// Custom relationship type for extensibility.
    /// </summary>
    [EnumMember(Value = "Custom")]
    Custom
}

/// <summary>
/// Extension methods for RelationshipTypeEnum.
/// </summary>
public static class RelationshipTypeEnumExtensions
{
    /// <summary>
    /// Gets the string value for the enum (as stored in database).
    /// </summary>
    public static string ToStringValue(this RelationshipTypeEnum type)
    {
        return type switch
        {
            RelationshipTypeEnum.Parent => "Parent",
            RelationshipTypeEnum.Child => "Child",
            RelationshipTypeEnum.Reference => "Reference",
            RelationshipTypeEnum.Related => "Related",
            RelationshipTypeEnum.SimilarTo => "similar-to",
            RelationshipTypeEnum.Cause => "Cause",
            RelationshipTypeEnum.Effect => "Effect",
            RelationshipTypeEnum.Duplicate => "Duplicate",
            RelationshipTypeEnum.VersionOf => "VersionOf",
            RelationshipTypeEnum.PartOf => "PartOf",
            RelationshipTypeEnum.Contains => "Contains",
            RelationshipTypeEnum.Precedes => "Precedes",
            RelationshipTypeEnum.Follows => "Follows",
            RelationshipTypeEnum.ExampleOf => "ExampleOf",
            RelationshipTypeEnum.InstanceOf => "InstanceOf",
            RelationshipTypeEnum.Generalizes => "Generalizes",
            RelationshipTypeEnum.Specializes => "Specializes",
            RelationshipTypeEnum.Synonym => "Synonym",
            RelationshipTypeEnum.Antonym => "Antonym",
            RelationshipTypeEnum.Refines => "Refines",
            RelationshipTypeEnum.Custom => "Custom",
            _ => "Related" // Default fallback
        };
    }

    /// <summary>
    /// Parses a string value to RelationshipTypeEnum.
    /// Falls back to Custom for unknown values.
    /// </summary>
    public static RelationshipTypeEnum ParseRelationshipType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return RelationshipTypeEnum.Related;

        // Case-insensitive matching
        return value.ToLowerInvariant() switch
        {
            "parent" => RelationshipTypeEnum.Parent,
            "child" => RelationshipTypeEnum.Child,
            "reference" or "ref" => RelationshipTypeEnum.Reference,
            "related" or "related-to" => RelationshipTypeEnum.Related,
            "similar-to" or "similarto" or "similar" => RelationshipTypeEnum.SimilarTo,
            "cause" => RelationshipTypeEnum.Cause,
            "effect" => RelationshipTypeEnum.Effect,
            "duplicate" or "dup" => RelationshipTypeEnum.Duplicate,
            "versionof" or "version-of" => RelationshipTypeEnum.VersionOf,
            "partof" or "part-of" => RelationshipTypeEnum.PartOf,
            "contains" => RelationshipTypeEnum.Contains,
            "precedes" => RelationshipTypeEnum.Precedes,
            "follows" => RelationshipTypeEnum.Follows,
            "exampleof" or "example-of" => RelationshipTypeEnum.ExampleOf,
            "instanceof" or "instance-of" => RelationshipTypeEnum.InstanceOf,
            "generalizes" => RelationshipTypeEnum.Generalizes,
            "specializes" => RelationshipTypeEnum.Specializes,
            "synonym" => RelationshipTypeEnum.Synonym,
            "antonym" => RelationshipTypeEnum.Antonym,
            "refines" => RelationshipTypeEnum.Refines,
            "custom" => RelationshipTypeEnum.Custom,
            _ => RelationshipTypeEnum.Custom // Unknown types map to Custom
        };
    }
}
