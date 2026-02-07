using Memorizer.Models;
using Memorizer.Models.Enums;

namespace Memorizer.UnitTests;

public class WorkspaceProjectTests
{
    #region MemoryOwner Tests

    [Fact]
    public void MemoryOwner_Unfiled_ReturnsCorrectValues()
    {
        var unfiled = MemoryOwner.Unfiled;

        Assert.Equal(OwnerTypeEnum.Workspace, unfiled.Type);
        Assert.Equal(Guid.Empty, unfiled.Id);
        Assert.True(unfiled.IsUnfiled);
    }

    [Fact]
    public void MemoryOwner_ForWorkspace_ReturnsCorrectValues()
    {
        var workspaceId = WorkspaceId.New();
        var owner = MemoryOwner.ForWorkspace(workspaceId);

        Assert.Equal(OwnerTypeEnum.Workspace, owner.Type);
        Assert.Equal(workspaceId.Value, owner.Id);
        Assert.False(owner.IsUnfiled);
        Assert.Equal(workspaceId, owner.WorkspaceId);
        Assert.Null(owner.ProjectId);
    }

    [Fact]
    public void MemoryOwner_ForProject_ReturnsCorrectValues()
    {
        var projectId = ProjectId.New();
        var owner = MemoryOwner.ForProject(projectId);

        Assert.Equal(OwnerTypeEnum.Project, owner.Type);
        Assert.Equal(projectId.Value, owner.Id);
        Assert.False(owner.IsUnfiled);
        Assert.Null(owner.WorkspaceId);
        Assert.Equal(projectId, owner.ProjectId);
    }

    [Fact]
    public void MemoryOwner_UnfiledWorkspaceId_IsGuidEmpty()
    {
        Assert.Equal(Guid.Empty, MemoryOwner.UnfiledWorkspaceId);
    }

    [Fact]
    public void MemoryOwner_ToString_FormatsCorrectly()
    {
        var workspaceId = new WorkspaceId(Guid.Parse("12345678-1234-1234-1234-123456789012"));
        var owner = MemoryOwner.ForWorkspace(workspaceId);

        Assert.Equal("Workspace:12345678-1234-1234-1234-123456789012", owner.ToString());
    }

    #endregion

    #region OwnerTypeEnum Tests

    [Theory]
    [InlineData(OwnerTypeEnum.Workspace, "workspace")]
    [InlineData(OwnerTypeEnum.Project, "project")]
    public void OwnerTypeEnum_ToStringValue_ReturnsExpected(OwnerTypeEnum type, string expected)
    {
        Assert.Equal(expected, type.ToStringValue());
    }

    [Theory]
    [InlineData("workspace", OwnerTypeEnum.Workspace)]
    [InlineData("project", OwnerTypeEnum.Project)]
    [InlineData("WORKSPACE", OwnerTypeEnum.Workspace)]
    [InlineData("PROJECT", OwnerTypeEnum.Project)]
    [InlineData(null, OwnerTypeEnum.Workspace)]
    [InlineData("", OwnerTypeEnum.Workspace)]
    [InlineData("unknown", OwnerTypeEnum.Workspace)]
    public void OwnerTypeEnum_ParseOwnerType_ReturnsExpected(string? input, OwnerTypeEnum expected)
    {
        Assert.Equal(expected, OwnerTypeEnumExtensions.ParseOwnerType(input));
    }

    [Fact]
    public void OwnerTypeEnum_HasExplicitValues()
    {
        Assert.Equal(0, (int)OwnerTypeEnum.Workspace);
        Assert.Equal(1, (int)OwnerTypeEnum.Project);
    }

    #endregion

    #region ProjectStatusEnum Tests

    [Theory]
    [InlineData(ProjectStatusEnum.Draft, "draft")]
    [InlineData(ProjectStatusEnum.Active, "active")]
    [InlineData(ProjectStatusEnum.OnHold, "on_hold")]
    [InlineData(ProjectStatusEnum.Completed, "completed")]
    [InlineData(ProjectStatusEnum.Cancelled, "cancelled")]
    [InlineData(ProjectStatusEnum.Archived, "archived")]
    public void ProjectStatusEnum_ToStringValue_ReturnsExpected(ProjectStatusEnum status, string expected)
    {
        Assert.Equal(expected, status.ToStringValue());
    }

    [Theory]
    [InlineData("draft", ProjectStatusEnum.Draft)]
    [InlineData("active", ProjectStatusEnum.Active)]
    [InlineData("on_hold", ProjectStatusEnum.OnHold)]
    [InlineData("onhold", ProjectStatusEnum.OnHold)]
    [InlineData("on-hold", ProjectStatusEnum.OnHold)]
    [InlineData("completed", ProjectStatusEnum.Completed)]
    [InlineData("complete", ProjectStatusEnum.Completed)]
    [InlineData("done", ProjectStatusEnum.Completed)]
    [InlineData("cancelled", ProjectStatusEnum.Cancelled)]
    [InlineData("canceled", ProjectStatusEnum.Cancelled)]
    [InlineData("archived", ProjectStatusEnum.Archived)]
    [InlineData(null, ProjectStatusEnum.Draft)]
    [InlineData("", ProjectStatusEnum.Draft)]
    [InlineData("unknown", ProjectStatusEnum.Draft)]
    public void ProjectStatusEnum_ParseProjectStatus_ReturnsExpected(string? input, ProjectStatusEnum expected)
    {
        Assert.Equal(expected, ProjectStatusEnumExtensions.ParseProjectStatus(input));
    }

    [Theory]
    [InlineData(ProjectStatusEnum.Completed, true)]
    [InlineData(ProjectStatusEnum.Cancelled, true)]
    [InlineData(ProjectStatusEnum.Archived, true)]
    [InlineData(ProjectStatusEnum.Draft, false)]
    [InlineData(ProjectStatusEnum.Active, false)]
    [InlineData(ProjectStatusEnum.OnHold, false)]
    public void ProjectStatusEnum_IsTerminal_ReturnsExpected(ProjectStatusEnum status, bool expected)
    {
        Assert.Equal(expected, status.IsTerminal());
    }

    [Theory]
    [InlineData(ProjectStatusEnum.Draft, true)]
    [InlineData(ProjectStatusEnum.Active, true)]
    [InlineData(ProjectStatusEnum.OnHold, true)]
    [InlineData(ProjectStatusEnum.Completed, false)]
    [InlineData(ProjectStatusEnum.Cancelled, false)]
    [InlineData(ProjectStatusEnum.Archived, false)]
    public void ProjectStatusEnum_IsActive_ReturnsExpected(ProjectStatusEnum status, bool expected)
    {
        Assert.Equal(expected, status.IsActive());
    }

    [Fact]
    public void ProjectStatusEnum_HasExplicitValues()
    {
        Assert.Equal(0, (int)ProjectStatusEnum.Draft);
        Assert.Equal(1, (int)ProjectStatusEnum.Active);
        Assert.Equal(2, (int)ProjectStatusEnum.OnHold);
        Assert.Equal(3, (int)ProjectStatusEnum.Completed);
        Assert.Equal(4, (int)ProjectStatusEnum.Cancelled);
        Assert.Equal(5, (int)ProjectStatusEnum.Archived);
    }

    #endregion

    #region ArchetypeEnum Tests

    [Theory]
    [InlineData(ArchetypeEnum.Document, "document")]
    [InlineData(ArchetypeEnum.Record, "record")]
    public void ArchetypeEnum_ToStringValue_ReturnsExpected(ArchetypeEnum archetype, string expected)
    {
        Assert.Equal(expected, archetype.ToStringValue());
    }

    [Theory]
    [InlineData("document", ArchetypeEnum.Document)]
    [InlineData("doc", ArchetypeEnum.Document)]
    [InlineData("record", ArchetypeEnum.Record)]
    [InlineData("rec", ArchetypeEnum.Record)]
    [InlineData("log", ArchetypeEnum.Record)]
    [InlineData(null, ArchetypeEnum.Document)]
    [InlineData("", ArchetypeEnum.Document)]
    [InlineData("unknown", ArchetypeEnum.Document)]
    public void ArchetypeEnum_ParseArchetype_ReturnsExpected(string? input, ArchetypeEnum expected)
    {
        Assert.Equal(expected, ArchetypeEnumExtensions.ParseArchetype(input));
    }

    [Theory]
    [InlineData(ArchetypeEnum.Document, true)]
    [InlineData(ArchetypeEnum.Record, false)]
    public void ArchetypeEnum_IsMutable_ReturnsExpected(ArchetypeEnum archetype, bool expected)
    {
        Assert.Equal(expected, archetype.IsMutable());
    }

    [Fact]
    public void ArchetypeEnum_HasExplicitValues()
    {
        Assert.Equal(0, (int)ArchetypeEnum.Document);
        Assert.Equal(1, (int)ArchetypeEnum.Record);
    }

    #endregion

    #region Workspace Model Tests

    [Fact]
    public void Workspace_UnfiledId_IsGuidEmpty()
    {
        Assert.Equal(new WorkspaceId(Guid.Empty), Workspace.UnfiledId);
    }

    #endregion
}
