using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Memorizer.Models.Enums;

/// <summary>
/// Polymorphic owner type discriminator for memories.
/// Indicates whether a memory belongs to a workspace or project.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OwnerTypeEnum
{
    /// <summary>
    /// Memory is owned by a workspace.
    /// </summary>
    [EnumMember(Value = "workspace")]
    Workspace = 0,

    /// <summary>
    /// Memory is owned by a project.
    /// </summary>
    [EnumMember(Value = "project")]
    Project = 1
}

/// <summary>
/// Extension methods for OwnerTypeEnum.
/// </summary>
public static class OwnerTypeEnumExtensions
{
    /// <summary>
    /// Gets the string value for the enum (as stored in database).
    /// </summary>
    public static string ToStringValue(this OwnerTypeEnum type)
    {
        return type switch
        {
            OwnerTypeEnum.Workspace => "workspace",
            OwnerTypeEnum.Project => "project",
            _ => "workspace"
        };
    }

    /// <summary>
    /// Parses a string value to OwnerTypeEnum.
    /// </summary>
    public static OwnerTypeEnum ParseOwnerType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return OwnerTypeEnum.Workspace;

        return value.ToLowerInvariant() switch
        {
            "workspace" => OwnerTypeEnum.Workspace,
            "project" => OwnerTypeEnum.Project,
            _ => OwnerTypeEnum.Workspace
        };
    }
}
