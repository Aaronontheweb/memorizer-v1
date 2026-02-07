-- Create projects table for finite work items with lifecycle and completion criteria
-- Projects belong to a workspace and support unlimited nesting via self-referential parent_id
-- Key difference from workspaces: projects have status, victory conditions, and completion dates

-- Status enum values:
-- 0 = Draft, 1 = Active, 2 = OnHold, 3 = Completed, 4 = Cancelled, 5 = Archived
CREATE TABLE IF NOT EXISTS projects (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    parent_id UUID REFERENCES projects(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    slug TEXT NOT NULL,
    description TEXT,
    status SMALLINT NOT NULL DEFAULT 0 CHECK (status >= 0 AND status <= 5),
    victory_conditions TEXT,  -- Freeform markdown, UI/agent parses into checklist
    settings JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at TIMESTAMPTZ,
    UNIQUE(workspace_id, parent_id, slug)
);

-- Index for workspace membership queries
CREATE INDEX IF NOT EXISTS idx_projects_workspace_id ON projects(workspace_id);

-- Index for hierarchy queries within a workspace
CREATE INDEX IF NOT EXISTS idx_projects_parent_id ON projects(parent_id);

-- Index for status filtering
CREATE INDEX IF NOT EXISTS idx_projects_status ON projects(status);

-- Index for slug lookups
CREATE INDEX IF NOT EXISTS idx_projects_slug ON projects(slug);

-- Partial index for active/in-progress projects (common query pattern)
-- Includes Draft (0), Active (1), OnHold (2)
CREATE INDEX IF NOT EXISTS idx_projects_active ON projects(workspace_id, updated_at DESC)
    WHERE status IN (0, 1, 2);

-- Add comments for documentation
COMMENT ON TABLE projects IS 'Finite work items with lifecycle, completion criteria, and victory conditions';
COMMENT ON COLUMN projects.workspace_id IS 'Parent workspace containing this project';
COMMENT ON COLUMN projects.parent_id IS 'Self-referential FK for nested project hierarchy';
COMMENT ON COLUMN projects.slug IS 'URL-safe identifier, unique within workspace/parent scope';
COMMENT ON COLUMN projects.status IS 'Workflow state: draft → active → on_hold → completed/cancelled → archived';
COMMENT ON COLUMN projects.victory_conditions IS 'Markdown description of completion criteria (parseable into checklist)';
COMMENT ON COLUMN projects.completed_at IS 'Timestamp when project reached completed/cancelled status';
