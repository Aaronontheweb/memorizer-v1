-- Create workspaces table for organizing memories into persistent containers
-- Workspaces represent products, teams, or areas of focus (rarely closed)
-- Supports unlimited nesting via self-referential parent_id

CREATE TABLE IF NOT EXISTS workspaces (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    parent_id UUID REFERENCES workspaces(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    slug TEXT NOT NULL,
    description TEXT,
    is_system BOOLEAN NOT NULL DEFAULT false,  -- True for Unfiled workspace
    settings JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(parent_id, slug)
);

-- Index for efficient hierarchy queries
CREATE INDEX IF NOT EXISTS idx_workspaces_parent_id ON workspaces(parent_id);

-- Index for slug lookups
CREATE INDEX IF NOT EXISTS idx_workspaces_slug ON workspaces(slug);

-- Add comments for documentation
COMMENT ON TABLE workspaces IS 'Persistent containers for organizing memories (products, teams, areas)';
COMMENT ON COLUMN workspaces.parent_id IS 'Self-referential FK for nested workspace hierarchy';
COMMENT ON COLUMN workspaces.slug IS 'URL-safe identifier, unique within parent scope';
COMMENT ON COLUMN workspaces.is_system IS 'True for system-managed workspaces (e.g., Unfiled)';
COMMENT ON COLUMN workspaces.settings IS 'JSON configuration for workspace-specific settings';

-- Create the well-known "Unfiled" workspace with UUID 00000000-0000-0000-0000-000000000000
-- All existing memories will be assigned to this workspace by default
INSERT INTO workspaces (id, name, slug, description, is_system)
VALUES (
    '00000000-0000-0000-0000-000000000000',
    'Unfiled',
    'unfiled',
    'Default workspace for memories not yet assigned to a project or workspace',
    true
) ON CONFLICT (id) DO NOTHING;
