-- Create the "System Memories" workspace for storing system-generated index memories
-- This is a hidden system workspace (is_system=true) that won't appear in UI listings
-- UUID: 11111111-1111-1111-1111-111111111111

INSERT INTO workspaces (id, name, slug, description, is_system)
VALUES (
    '11111111-1111-1111-1111-111111111111',
    'System Memories',
    'system-memories',
    'Internal workspace for system-generated index memories. Not visible to users.',
    true
) ON CONFLICT (id) DO NOTHING;

COMMENT ON TABLE workspaces IS 'Persistent containers for organizing memories. System workspaces (is_system=true) include Unfiled and System Memories.';
