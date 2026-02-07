using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Memorizer.Models.Enums;

/// <summary>
/// Project lifecycle status workflow.
/// Projects progress through: draft -> active -> (on_hold) -> completed/cancelled -> archived
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectStatusEnum
{
    /// <summary>
    /// Project is being planned/defined, not yet started.
    /// </summary>
    [EnumMember(Value = "draft")]
    Draft = 0,

    /// <summary>
    /// Project is actively being worked on.
    /// </summary>
    [EnumMember(Value = "active")]
    Active = 1,

    /// <summary>
    /// Project is temporarily paused (blocked, deprioritized, etc.).
    /// </summary>
    [EnumMember(Value = "on_hold")]
    OnHold = 2,

    /// <summary>
    /// Project has been successfully completed (victory conditions met).
    /// </summary>
    [EnumMember(Value = "completed")]
    Completed = 3,

    /// <summary>
    /// Project was cancelled/abandoned before completion.
    /// </summary>
    [EnumMember(Value = "cancelled")]
    Cancelled = 4,

    /// <summary>
    /// Project has been archived (no longer actively referenced).
    /// </summary>
    [EnumMember(Value = "archived")]
    Archived = 5
}

/// <summary>
/// Extension methods for ProjectStatusEnum.
/// </summary>
public static class ProjectStatusEnumExtensions
{
    /// <summary>
    /// Gets the string value for the enum (as stored in database).
    /// </summary>
    public static string ToStringValue(this ProjectStatusEnum status)
    {
        return status switch
        {
            ProjectStatusEnum.Draft => "draft",
            ProjectStatusEnum.Active => "active",
            ProjectStatusEnum.OnHold => "on_hold",
            ProjectStatusEnum.Completed => "completed",
            ProjectStatusEnum.Cancelled => "cancelled",
            ProjectStatusEnum.Archived => "archived",
            _ => "draft"
        };
    }

    /// <summary>
    /// Parses a string value to ProjectStatusEnum.
    /// </summary>
    public static ProjectStatusEnum ParseProjectStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ProjectStatusEnum.Draft;

        return value.ToLowerInvariant() switch
        {
            "draft" => ProjectStatusEnum.Draft,
            "active" => ProjectStatusEnum.Active,
            "on_hold" or "onhold" or "on-hold" => ProjectStatusEnum.OnHold,
            "completed" or "complete" or "done" => ProjectStatusEnum.Completed,
            "cancelled" or "canceled" => ProjectStatusEnum.Cancelled,
            "archived" or "archive" => ProjectStatusEnum.Archived,
            _ => ProjectStatusEnum.Draft
        };
    }

    /// <summary>
    /// Returns true if the project is in a terminal state (completed, cancelled, or archived).
    /// </summary>
    public static bool IsTerminal(this ProjectStatusEnum status)
    {
        return status is ProjectStatusEnum.Completed or ProjectStatusEnum.Cancelled or ProjectStatusEnum.Archived;
    }

    /// <summary>
    /// Returns true if the project is actively being worked on (draft, active, or on_hold).
    /// </summary>
    public static bool IsActive(this ProjectStatusEnum status)
    {
        return status is ProjectStatusEnum.Draft or ProjectStatusEnum.Active or ProjectStatusEnum.OnHold;
    }
}
