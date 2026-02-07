-- 017_add_system_archetype.sql: Add support for system memories
-- ArchetypeEnum values: Document=0, Record=1, Archived=2, System=3
-- System memories are internal indexing content hidden from normal user searches.
-- Used for semantic search of projects/workspaces by their metadata.

-- Update the archetype check constraint to allow System (3)
ALTER TABLE memories DROP CONSTRAINT IF EXISTS memories_archetype_check;
ALTER TABLE memories ADD CONSTRAINT memories_archetype_check CHECK (archetype IN (0, 1, 2, 3));

-- Update column comment to reflect the new archetype value
COMMENT ON COLUMN memories.archetype IS '0=Document (living, editable), 1=Record (historical, immutable), 2=Archived (obsolete, hidden from searches), 3=System (internal indexing, hidden from user searches)';

-- Update the active archetype index to also exclude System memories
-- System memories should not appear in normal searches alongside user content
DROP INDEX IF EXISTS idx_memories_active_archetype;
CREATE INDEX IF NOT EXISTS idx_memories_active_archetype
ON memories(archetype)
WHERE archetype IN (0, 1);

-- Update owner-scoped index to exclude System memories for normal searches
DROP INDEX IF EXISTS idx_memories_owner_active;
CREATE INDEX IF NOT EXISTS idx_memories_owner_active
ON memories(owner_type, owner_id)
WHERE archetype IN (0, 1);

-- Add index specifically for system memory lookups by owner and type_legacy
-- Used to find/update system memories for projects/workspaces efficiently
CREATE INDEX IF NOT EXISTS idx_memories_system_by_owner
ON memories(owner_type, owner_id, type_legacy)
WHERE archetype = 3;
