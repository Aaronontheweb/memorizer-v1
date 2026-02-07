-- Add trigger to move memories to Unfiled workspace when a project is deleted
-- This handles both explicit deletes and CASCADE deletes from parent deletion
-- Ensures memories are never orphaned

CREATE OR REPLACE FUNCTION move_project_memories_to_unfiled()
RETURNS TRIGGER AS $$
BEGIN
    -- Move all memories owned by this project to the Unfiled workspace
    -- owner_type: 0 = Workspace, owner_id: 00000000-0000-0000-0000-000000000000 = Unfiled
    UPDATE memories
    SET owner_type = 0,
        owner_id = '00000000-0000-0000-0000-000000000000'
    WHERE owner_type = 1  -- Project
      AND owner_id = OLD.id;

    RETURN OLD;
END;
$$ LANGUAGE plpgsql;

-- Create trigger that fires BEFORE delete to ensure memories are moved before project is removed
CREATE TRIGGER trg_project_delete_move_memories
    BEFORE DELETE ON projects
    FOR EACH ROW
    EXECUTE FUNCTION move_project_memories_to_unfiled();

COMMENT ON FUNCTION move_project_memories_to_unfiled() IS 'Moves memories to Unfiled workspace when their owning project is deleted';
COMMENT ON TRIGGER trg_project_delete_move_memories ON projects IS 'Ensures memories are moved to Unfiled before project deletion (handles CASCADE too)';
