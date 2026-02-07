using Memorizer.Models;
using Memorizer.Models.Enums;
using Memorizer.Models.ValueTypes;
using Memorizer.Services;
using Npgsql;
using Xunit.Abstractions;

namespace Memorizer.IntegrationTests;

/// <summary>
/// Integration tests for workspace and project migrations (012, 013, 014).
/// Verifies that the schema is created correctly and that basic CRUD operations work.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class WorkspaceProjectMigrationIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public WorkspaceProjectMigrationIntegrationTests(IntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// Gets the current embedding dimension from the database schema.
    /// </summary>
    private async Task<int> GetEmbeddingDimensionAsync(NpgsqlConnection conn)
    {
        await using var cmd = new NpgsqlCommand(@"
            SELECT atttypmod
            FROM pg_attribute
            WHERE attrelid = 'memories'::regclass
            AND attname = 'embedding'", conn);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    /// <summary>
    /// Generates a dummy embedding vector string for the given dimension.
    /// </summary>
    private static string GenerateDummyEmbedding(int dimension)
    {
        var values = string.Join(",", Enumerable.Repeat("0.1", dimension));
        return $"[{values}]";
    }

    #region Migration 012 - Workspaces Table Tests

    [Fact]
    public async Task Migration012_WorkspacesTable_Exists()
    {
        await using var conn = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT EXISTS (
                SELECT FROM information_schema.tables
                WHERE table_name = 'workspaces'
            )", conn);

        var exists = (bool)(await cmd.ExecuteScalarAsync() ?? false);
        Assert.True(exists, "workspaces table should exist");
        _output.WriteLine("workspaces table exists");
    }

    [Fact]
    public async Task Migration012_WorkspacesTable_HasExpectedColumns()
    {
        await using var conn = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_name = 'workspaces'
            ORDER BY ordinal_position", conn);

        var columns = new Dictionary<string, (string DataType, string IsNullable)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            var dataType = reader.GetString(1);
            var isNullable = reader.GetString(2);
            columns[name] = (dataType, isNullable);
            _output.WriteLine($"  {name}: {dataType} (nullable: {isNullable})");
        }

        // Verify required columns exist
        Assert.True(columns.ContainsKey("id"), "id column should exist");
        Assert.True(columns.ContainsKey("parent_id"), "parent_id column should exist");
        Assert.True(columns.ContainsKey("name"), "name column should exist");
        Assert.True(columns.ContainsKey("slug"), "slug column should exist");
        Assert.True(columns.ContainsKey("description"), "description column should exist");
        Assert.True(columns.ContainsKey("is_system"), "is_system column should exist");
        Assert.True(columns.ContainsKey("settings"), "settings column should exist");
        Assert.True(columns.ContainsKey("created_at"), "created_at column should exist");
        Assert.True(columns.ContainsKey("updated_at"), "updated_at column should exist");

        _output.WriteLine("All expected workspace columns exist");
    }

    [Fact]
    public async Task Migration012_UnfiledWorkspace_Exists()
    {
        await using var conn = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await conn.OpenAsync();

        var unfiledId = Guid.Empty;

        await using var cmd = new NpgsqlCommand(@"
            SELECT id, name, slug, is_system
            FROM workspaces
            WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", unfiledId);

        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Unfiled workspace should exist");

        var id = reader.GetGuid(0);
        var name = reader.GetString(1);
        var slug = reader.GetString(2);
        var isSystem = reader.GetBoolean(3);

        Assert.Equal(unfiledId, id);
        Assert.Equal("Unfiled", name);
        Assert.Equal("unfiled", slug);
        Assert.True(isSystem, "Unfiled workspace should be marked as system");

        _output.WriteLine($"Unfiled workspace exists: id={id}, name={name}, slug={slug}, is_system={isSystem}");
    }

    #endregion

    #region Migration 013 - Projects Table Tests

    [Fact]
    public async Task Migration013_ProjectsTable_Exists()
    {
        await using var conn = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT EXISTS (
                SELECT FROM information_schema.tables
                WHERE table_name = 'projects'
            )", conn);

        var exists = (bool)(await cmd.ExecuteScalarAsync() ?? false);
        Assert.True(exists, "projects table should exist");
        _output.WriteLine("projects table exists");
    }

    [Fact]
    public async Task Migration013_ProjectsTable_HasExpectedColumns()
    {
        await using var conn = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_name = 'projects'
            ORDER BY ordinal_position", conn);

        var columns = new Dictionary<string, (string DataType, string IsNullable)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            var dataType = reader.GetString(1);
            var isNullable = reader.GetString(2);
            columns[name] = (dataType, isNullable);
            _output.WriteLine($"  {name}: {dataType} (nullable: {isNullable})");
        }

        // Verify required columns exist
        Assert.True(columns.ContainsKey("id"), "id column should exist");
        Assert.True(columns.ContainsKey("workspace_id"), "workspace_id column should exist");
        Assert.True(columns.ContainsKey("parent_id"), "parent_id column should exist");
        Assert.True(columns.ContainsKey("name"), "name column should exist");
        Assert.True(columns.ContainsKey("slug"), "slug column should exist");
        Assert.True(columns.ContainsKey("description"), "description column should exist");
        Assert.True(columns.ContainsKey("status"), "status column should exist");
        Assert.True(columns.ContainsKey("victory_conditions"), "victory_conditions column should exist");
        Assert.True(columns.ContainsKey("settings"), "settings column should exist");
        Assert.True(columns.ContainsKey("created_at"), "created_at column should exist");
        Assert.True(columns.ContainsKey("updated_at"), "updated_at column should exist");
        Assert.True(columns.ContainsKey("completed_at"), "completed_at column should exist");

        // Verify status column is smallint
        Assert.Equal("smallint", columns["status"].DataType);

        _output.WriteLine("All expected project columns exist");
    }

    [Fact]
    public async Task Migration013_ProjectsTable_StatusConstraint_EnforcesValidValues()
    {
        await using var conn = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await conn.OpenAsync();

        // First create a workspace to use as foreign key
        var workspaceId = Guid.NewGuid();
        await using (var createWs = new NpgsqlCommand(@"
            INSERT INTO workspaces (id, name, slug)
            VALUES (@id, 'Test Workspace', 'test-workspace')", conn))
        {
            createWs.Parameters.AddWithValue("id", workspaceId);
            await createWs.ExecuteNonQueryAsync();
        }

        try
        {
            // Try to insert a project with invalid status (999)
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO projects (id, workspace_id, name, slug, status)
                VALUES (@id, @workspace_id, 'Test Project', 'test-project', 999)", conn);
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("workspace_id", workspaceId);

            var ex = await Assert.ThrowsAsync<PostgresException>(async () => await cmd.ExecuteNonQueryAsync());
            Assert.Contains("check", ex.Message.ToLower());
            _output.WriteLine($"Status constraint enforced: {ex.MessageText}");
        }
        finally
        {
            // Cleanup
            await using var cleanup = new NpgsqlCommand("DELETE FROM workspaces WHERE id = @id", conn);
            cleanup.Parameters.AddWithValue("id", workspaceId);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    #endregion

    #region Migration 014 - Memories Table Extensions Tests

    [Fact]
    public async Task Migration014_MemoriesTable_HasOwnerColumns()
    {
        await using var conn = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT column_name, data_type
            FROM information_schema.columns
            WHERE table_name = 'memories'
            AND column_name IN ('owner_type', 'owner_id', 'type_legacy', 'memory_type', 'archetype')
            ORDER BY column_name", conn);

        var columns = new Dictionary<string, string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            var dataType = reader.GetString(1);
            columns[name] = dataType;
            _output.WriteLine($"  {name}: {dataType}");
        }

        // Verify polymorphic owner columns
        Assert.True(columns.ContainsKey("owner_type"), "owner_type column should exist");
        Assert.Equal("smallint", columns["owner_type"]);

        Assert.True(columns.ContainsKey("owner_id"), "owner_id column should exist");
        Assert.Equal("uuid", columns["owner_id"]);

        // Verify type_legacy (renamed from type)
        Assert.True(columns.ContainsKey("type_legacy"), "type_legacy column should exist");

        // Verify new enum columns
        Assert.True(columns.ContainsKey("memory_type"), "memory_type column should exist");
        Assert.Equal("smallint", columns["memory_type"]);

        Assert.True(columns.ContainsKey("archetype"), "archetype column should exist");
        Assert.Equal("smallint", columns["archetype"]);

        _output.WriteLine("All memory extension columns exist with correct types");
    }

    [Fact]
    public async Task Migration014_ExistingMemories_HaveDefaultOwner()
    {
        await using var conn = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await conn.OpenAsync();

        // Check that any existing memories have the default owner (Unfiled workspace)
        await using var cmd = new NpgsqlCommand(@"
            SELECT COUNT(*)
            FROM memories
            WHERE owner_type != 0 OR owner_id != '00000000-0000-0000-0000-000000000000'", conn);

        var nonDefaultCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());

        // All memories should default to Unfiled workspace (owner_type=0, owner_id=Guid.Empty)
        Assert.Equal(0, nonDefaultCount);
        _output.WriteLine("All existing memories have default owner (Unfiled workspace)");
    }

    [Fact]
    public async Task Migration014_MemoriesTable_OwnerTypeConstraint_EnforcesValidValues()
    {
        await using var conn = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await conn.OpenAsync();

        var dimension = await GetEmbeddingDimensionAsync(conn);
        var dummyEmbedding = GenerateDummyEmbedding(dimension);

        // Try to insert a memory with invalid owner_type (99)
        await using var cmd = new NpgsqlCommand($@"
            INSERT INTO memories (id, type_legacy, text, content, source, embedding, embedding_metadata, tags, confidence, created_at, updated_at, owner_type, owner_id, archetype, memory_type)
            VALUES (@id, 'test', 'Test memory text', '{{}}'::jsonb, 'test', '{dummyEmbedding}'::vector, '{dummyEmbedding}'::vector, ARRAY[]::text[], 1.0, now(), now(), 99, @owner_id, 0, 0)", conn);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("owner_id", Guid.Empty);

        var ex = await Assert.ThrowsAsync<PostgresException>(async () => await cmd.ExecuteNonQueryAsync());
        Assert.Contains("check", ex.Message.ToLower());
        _output.WriteLine($"owner_type constraint enforced: {ex.MessageText}");
    }

    #endregion

    #region Workspace CRUD Tests

    [Fact]
    public async Task Workspace_CanCreate_AndRetrieve()
    {
        await using var conn = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await conn.OpenAsync();

        var workspaceId = Guid.NewGuid();
        var name = $"Test Workspace {workspaceId:N}";
        var slug = $"test-workspace-{workspaceId:N}";
        var description = "A test workspace for integration testing";

        try
        {
            // Create
            await using (var createCmd = new NpgsqlCommand(@"
                INSERT INTO workspaces (id, name, slug, description)
                VALUES (@id, @name, @slug, @description)", conn))
            {
                createCmd.Parameters.AddWithValue("id", workspaceId);
                createCmd.Parameters.AddWithValue("name", name);
                createCmd.Parameters.AddWithValue("slug", slug);
                createCmd.Parameters.AddWithValue("description", description);
                var affected = await createCmd.ExecuteNonQueryAsync();
                Assert.Equal(1, affected);
            }

            // Retrieve
            await using var readCmd = new NpgsqlCommand(@"
                SELECT id, name, slug, description, is_system, created_at, updated_at
                FROM workspaces WHERE id = @id", conn);
            readCmd.Parameters.AddWithValue("id", workspaceId);

            await using var reader = await readCmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Should retrieve the created workspace");

            Assert.Equal(workspaceId, reader.GetGuid(0));
            Assert.Equal(name, reader.GetString(1));
            Assert.Equal(slug, reader.GetString(2));
            Assert.Equal(description, reader.GetString(3));
            Assert.False(reader.GetBoolean(4)); // is_system should default to false
            Assert.True(reader.GetDateTime(5) > DateTime.MinValue); // created_at should be populated
            Assert.True(reader.GetDateTime(6) > DateTime.MinValue); // updated_at should be populated

            _output.WriteLine($"Created and retrieved workspace: {name}");
        }
        finally
        {
            // Cleanup
            await using var cleanup = new NpgsqlCommand("DELETE FROM workspaces WHERE id = @id", conn);
            cleanup.Parameters.AddWithValue("id", workspaceId);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Workspace_CanCreateNested_WithParent()
    {
        await using var conn = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await conn.OpenAsync();

        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();

        try
        {
            // Create parent
            await using (var createParent = new NpgsqlCommand(@"
                INSERT INTO workspaces (id, name, slug)
                VALUES (@id, 'Parent Workspace', 'parent-workspace')", conn))
            {
                createParent.Parameters.AddWithValue("id", parentId);
                await createParent.ExecuteNonQueryAsync();
            }

            // Create child
            await using (var createChild = new NpgsqlCommand(@"
                INSERT INTO workspaces (id, parent_id, name, slug)
                VALUES (@id, @parent_id, 'Child Workspace', 'child-workspace')", conn))
            {
                createChild.Parameters.AddWithValue("id", childId);
                createChild.Parameters.AddWithValue("parent_id", parentId);
                await createChild.ExecuteNonQueryAsync();
            }

            // Verify parent-child relationship
            await using var readCmd = new NpgsqlCommand(@"
                SELECT parent_id FROM workspaces WHERE id = @id", conn);
            readCmd.Parameters.AddWithValue("id", childId);

            var retrievedParentId = (Guid)(await readCmd.ExecuteScalarAsync() ?? Guid.Empty);
            Assert.Equal(parentId, retrievedParentId);

            _output.WriteLine("Created nested workspace hierarchy");
        }
        finally
        {
            // Cleanup (child first due to FK)
            await using (var cleanupChild = new NpgsqlCommand("DELETE FROM workspaces WHERE id = @id", conn))
            {
                cleanupChild.Parameters.AddWithValue("id", childId);
                await cleanupChild.ExecuteNonQueryAsync();
            }
            await using (var cleanupParent = new NpgsqlCommand("DELETE FROM workspaces WHERE id = @id", conn))
            {
                cleanupParent.Parameters.AddWithValue("id", parentId);
                await cleanupParent.ExecuteNonQueryAsync();
            }
        }
    }

    #endregion

    #region Project CRUD Tests

    [Fact]
    public async Task Project_CanCreate_AndRetrieve()
    {
        await using var conn = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await conn.OpenAsync();

        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        try
        {
            // Create workspace first
            await using (var createWs = new NpgsqlCommand(@"
                INSERT INTO workspaces (id, name, slug)
                VALUES (@id, 'Test Workspace', 'test-workspace')", conn))
            {
                createWs.Parameters.AddWithValue("id", workspaceId);
                await createWs.ExecuteNonQueryAsync();
            }

            // Create project
            await using (var createProj = new NpgsqlCommand(@"
                INSERT INTO projects (id, workspace_id, name, slug, status, description, victory_conditions)
                VALUES (@id, @workspace_id, 'Test Project', 'test-project', @status, 'Test description', 'Ship the feature')", conn))
            {
                createProj.Parameters.AddWithValue("id", projectId);
                createProj.Parameters.AddWithValue("workspace_id", workspaceId);
                createProj.Parameters.AddWithValue("status", (short)ProjectStatusEnum.Active);
                await createProj.ExecuteNonQueryAsync();
            }

            // Retrieve
            await using var readCmd = new NpgsqlCommand(@"
                SELECT id, workspace_id, name, slug, status, description, victory_conditions
                FROM projects WHERE id = @id", conn);
            readCmd.Parameters.AddWithValue("id", projectId);

            await using var reader = await readCmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());

            Assert.Equal(projectId, reader.GetGuid(0));
            Assert.Equal(workspaceId, reader.GetGuid(1));
            Assert.Equal("Test Project", reader.GetString(2));
            Assert.Equal("test-project", reader.GetString(3));
            Assert.Equal((short)ProjectStatusEnum.Active, reader.GetInt16(4));
            Assert.Equal("Test description", reader.GetString(5));
            Assert.Equal("Ship the feature", reader.GetString(6));

            _output.WriteLine("Created and retrieved project with Active status");
        }
        finally
        {
            // Cleanup
            await using (var cleanupProj = new NpgsqlCommand("DELETE FROM projects WHERE id = @id", conn))
            {
                cleanupProj.Parameters.AddWithValue("id", projectId);
                await cleanupProj.ExecuteNonQueryAsync();
            }
            await using (var cleanupWs = new NpgsqlCommand("DELETE FROM workspaces WHERE id = @id", conn))
            {
                cleanupWs.Parameters.AddWithValue("id", workspaceId);
                await cleanupWs.ExecuteNonQueryAsync();
            }
        }
    }

    [Fact]
    public async Task Project_AllStatusValues_CanBeStored()
    {
        await using var conn = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await conn.OpenAsync();

        var workspaceId = Guid.NewGuid();
        var projectIds = new List<Guid>();

        try
        {
            // Create workspace
            await using (var createWs = new NpgsqlCommand(@"
                INSERT INTO workspaces (id, name, slug)
                VALUES (@id, 'Status Test Workspace', 'status-test')", conn))
            {
                createWs.Parameters.AddWithValue("id", workspaceId);
                await createWs.ExecuteNonQueryAsync();
            }

            // Test all status values
            var statuses = new[]
            {
                ProjectStatusEnum.Draft,
                ProjectStatusEnum.Active,
                ProjectStatusEnum.OnHold,
                ProjectStatusEnum.Completed,
                ProjectStatusEnum.Cancelled,
                ProjectStatusEnum.Archived
            };

            foreach (var status in statuses)
            {
                var projectId = Guid.NewGuid();
                projectIds.Add(projectId);

                await using var createProj = new NpgsqlCommand(@"
                    INSERT INTO projects (id, workspace_id, name, slug, status)
                    VALUES (@id, @workspace_id, @name, @slug, @status)", conn);
                createProj.Parameters.AddWithValue("id", projectId);
                createProj.Parameters.AddWithValue("workspace_id", workspaceId);
                createProj.Parameters.AddWithValue("name", $"Project {status}");
                createProj.Parameters.AddWithValue("slug", $"project-{status.ToStringValue()}");
                createProj.Parameters.AddWithValue("status", (short)status);
                await createProj.ExecuteNonQueryAsync();

                // Verify
                await using var readCmd = new NpgsqlCommand("SELECT status FROM projects WHERE id = @id", conn);
                readCmd.Parameters.AddWithValue("id", projectId);
                var storedStatus = (short)(await readCmd.ExecuteScalarAsync() ?? -1);
                Assert.Equal((short)status, storedStatus);

                _output.WriteLine($"Stored and retrieved status: {status} = {(short)status}");
            }

            _output.WriteLine("All project status values can be stored and retrieved");
        }
        finally
        {
            // Cleanup
            foreach (var projectId in projectIds)
            {
                await using var cleanupProj = new NpgsqlCommand("DELETE FROM projects WHERE id = @id", conn);
                cleanupProj.Parameters.AddWithValue("id", projectId);
                await cleanupProj.ExecuteNonQueryAsync();
            }
            await using var cleanupWs = new NpgsqlCommand("DELETE FROM workspaces WHERE id = @id", conn);
            cleanupWs.Parameters.AddWithValue("id", workspaceId);
            await cleanupWs.ExecuteNonQueryAsync();
        }
    }

    #endregion

    #region Memory Owner Tests

    [Fact]
    public async Task Memory_CanBeAssigned_ToWorkspace()
    {
        await using var conn = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await conn.OpenAsync();

        var workspaceId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();
        var dimension = await GetEmbeddingDimensionAsync(conn);
        var dummyEmbedding = GenerateDummyEmbedding(dimension);

        try
        {
            // Create workspace
            await using (var createWs = new NpgsqlCommand(@"
                INSERT INTO workspaces (id, name, slug)
                VALUES (@id, 'Memory Test Workspace', 'memory-test-workspace')", conn))
            {
                createWs.Parameters.AddWithValue("id", workspaceId);
                await createWs.ExecuteNonQueryAsync();
            }

            // Create memory assigned to workspace
            await using (var createMem = new NpgsqlCommand($@"
                INSERT INTO memories (id, type_legacy, text, content, source, embedding, embedding_metadata, tags, confidence, created_at, updated_at, owner_type, owner_id, archetype, memory_type)
                VALUES (@id, 'test', 'Test memory for workspace', '{{}}'::jsonb, 'test', '{dummyEmbedding}'::vector, '{dummyEmbedding}'::vector, ARRAY[]::text[], 1.0, now(), now(), @owner_type, @owner_id, 0, 0)", conn))
            {
                createMem.Parameters.AddWithValue("id", memoryId);
                createMem.Parameters.AddWithValue("owner_type", (short)OwnerTypeEnum.Workspace);
                createMem.Parameters.AddWithValue("owner_id", workspaceId);
                await createMem.ExecuteNonQueryAsync();
            }

            // Verify
            await using var readCmd = new NpgsqlCommand(@"
                SELECT owner_type, owner_id FROM memories WHERE id = @id", conn);
            readCmd.Parameters.AddWithValue("id", memoryId);

            await using var reader = await readCmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());

            Assert.Equal((short)OwnerTypeEnum.Workspace, reader.GetInt16(0));
            Assert.Equal(workspaceId, reader.GetGuid(1));

            _output.WriteLine("Memory assigned to workspace successfully");
        }
        finally
        {
            await using (var cleanupMem = new NpgsqlCommand("DELETE FROM memories WHERE id = @id", conn))
            {
                cleanupMem.Parameters.AddWithValue("id", memoryId);
                await cleanupMem.ExecuteNonQueryAsync();
            }
            await using (var cleanupWs = new NpgsqlCommand("DELETE FROM workspaces WHERE id = @id", conn))
            {
                cleanupWs.Parameters.AddWithValue("id", workspaceId);
                await cleanupWs.ExecuteNonQueryAsync();
            }
        }
    }

    [Fact]
    public async Task Memory_CanBeAssigned_ToProject()
    {
        await using var conn = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await conn.OpenAsync();

        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();
        var dimension = await GetEmbeddingDimensionAsync(conn);
        var dummyEmbedding = GenerateDummyEmbedding(dimension);

        try
        {
            // Create workspace and project
            await using (var createWs = new NpgsqlCommand(@"
                INSERT INTO workspaces (id, name, slug)
                VALUES (@id, 'Project Memory Test Workspace', 'proj-mem-test')", conn))
            {
                createWs.Parameters.AddWithValue("id", workspaceId);
                await createWs.ExecuteNonQueryAsync();
            }

            await using (var createProj = new NpgsqlCommand(@"
                INSERT INTO projects (id, workspace_id, name, slug, status)
                VALUES (@id, @workspace_id, 'Test Project', 'test-project', 1)", conn))
            {
                createProj.Parameters.AddWithValue("id", projectId);
                createProj.Parameters.AddWithValue("workspace_id", workspaceId);
                await createProj.ExecuteNonQueryAsync();
            }

            // Create memory assigned to project
            await using (var createMem = new NpgsqlCommand($@"
                INSERT INTO memories (id, type_legacy, text, content, source, embedding, embedding_metadata, tags, confidence, created_at, updated_at, owner_type, owner_id, archetype, memory_type)
                VALUES (@id, 'test', 'Test memory for project', '{{}}'::jsonb, 'test', '{dummyEmbedding}'::vector, '{dummyEmbedding}'::vector, ARRAY[]::text[], 1.0, now(), now(), @owner_type, @owner_id, 0, 0)", conn))
            {
                createMem.Parameters.AddWithValue("id", memoryId);
                createMem.Parameters.AddWithValue("owner_type", (short)OwnerTypeEnum.Project);
                createMem.Parameters.AddWithValue("owner_id", projectId);
                await createMem.ExecuteNonQueryAsync();
            }

            // Verify
            await using var readCmd = new NpgsqlCommand(@"
                SELECT owner_type, owner_id FROM memories WHERE id = @id", conn);
            readCmd.Parameters.AddWithValue("id", memoryId);

            await using var reader = await readCmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());

            Assert.Equal((short)OwnerTypeEnum.Project, reader.GetInt16(0));
            Assert.Equal(projectId, reader.GetGuid(1));

            _output.WriteLine("Memory assigned to project successfully");
        }
        finally
        {
            await using (var cleanupMem = new NpgsqlCommand("DELETE FROM memories WHERE id = @id", conn))
            {
                cleanupMem.Parameters.AddWithValue("id", memoryId);
                await cleanupMem.ExecuteNonQueryAsync();
            }
            await using (var cleanupProj = new NpgsqlCommand("DELETE FROM projects WHERE id = @id", conn))
            {
                cleanupProj.Parameters.AddWithValue("id", projectId);
                await cleanupProj.ExecuteNonQueryAsync();
            }
            await using (var cleanupWs = new NpgsqlCommand("DELETE FROM workspaces WHERE id = @id", conn))
            {
                cleanupWs.Parameters.AddWithValue("id", workspaceId);
                await cleanupWs.ExecuteNonQueryAsync();
            }
        }
    }

    [Fact]
    public async Task Memory_Archetype_AllValues_CanBeStored()
    {
        await using var conn = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await conn.OpenAsync();

        var memoryIds = new List<Guid>();
        var dimension = await GetEmbeddingDimensionAsync(conn);
        var dummyEmbedding = GenerateDummyEmbedding(dimension);

        try
        {
            var archetypes = new[] { ArchetypeEnum.Document, ArchetypeEnum.Record };

            foreach (var archetype in archetypes)
            {
                var memoryId = Guid.NewGuid();
                memoryIds.Add(memoryId);

                await using var createMem = new NpgsqlCommand($@"
                    INSERT INTO memories (id, type_legacy, text, content, source, embedding, embedding_metadata, tags, confidence, created_at, updated_at, owner_type, owner_id, archetype, memory_type)
                    VALUES (@id, 'test', 'Test archetype memory', '{{}}'::jsonb, 'test', '{dummyEmbedding}'::vector, '{dummyEmbedding}'::vector, ARRAY[]::text[], 1.0, now(), now(), 0, '00000000-0000-0000-0000-000000000000', @archetype, 0)", conn);
                createMem.Parameters.AddWithValue("id", memoryId);
                createMem.Parameters.AddWithValue("archetype", (short)archetype);
                await createMem.ExecuteNonQueryAsync();

                // Verify
                await using var readCmd = new NpgsqlCommand("SELECT archetype FROM memories WHERE id = @id", conn);
                readCmd.Parameters.AddWithValue("id", memoryId);
                var storedArchetype = (short)(await readCmd.ExecuteScalarAsync() ?? -1);
                Assert.Equal((short)archetype, storedArchetype);

                _output.WriteLine($"Stored and retrieved archetype: {archetype} = {(short)archetype}");
            }

            _output.WriteLine("All archetype values can be stored and retrieved");
        }
        finally
        {
            foreach (var memoryId in memoryIds)
            {
                await using var cleanup = new NpgsqlCommand("DELETE FROM memories WHERE id = @id", conn);
                cleanup.Parameters.AddWithValue("id", memoryId);
                await cleanup.ExecuteNonQueryAsync();
            }
        }
    }

    #endregion

    #region Index Tests

    [Fact]
    public async Task Indexes_Exist_ForPerformance()
    {
        await using var conn = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT indexname, tablename
            FROM pg_indexes
            WHERE schemaname = 'public'
            AND tablename IN ('workspaces', 'projects', 'memories')
            AND indexname LIKE 'idx_%'
            ORDER BY tablename, indexname", conn);

        var indexes = new List<(string IndexName, string TableName)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var indexName = reader.GetString(0);
            var tableName = reader.GetString(1);
            indexes.Add((indexName, tableName));
            _output.WriteLine($"  {tableName}: {indexName}");
        }

        // Verify key indexes exist
        Assert.Contains(indexes, i => i.IndexName == "idx_workspaces_slug" && i.TableName == "workspaces");
        Assert.Contains(indexes, i => i.IndexName == "idx_projects_workspace_id" && i.TableName == "projects");
        Assert.Contains(indexes, i => i.IndexName == "idx_projects_status" && i.TableName == "projects");
        Assert.Contains(indexes, i => i.IndexName == "idx_memories_owner" && i.TableName == "memories");

        _output.WriteLine("All expected indexes exist");
    }

    #endregion
}
