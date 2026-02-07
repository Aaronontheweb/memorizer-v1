-- 016_add_archived_support.sql: Add support for archived memories
-- ArchetypeEnum values: Document=0, Record=1, Archived=2

-- Update the archetype check constraint to allow Archived (2)
-- First drop the old constraint, then add the new one
ALTER TABLE memories DROP CONSTRAINT IF EXISTS memories_archetype_check;
ALTER TABLE memories ADD CONSTRAINT memories_archetype_check CHECK (archetype IN (0, 1, 2));

-- Update column comment to reflect the new archetype value
COMMENT ON COLUMN memories.archetype IS '0=Document (living, editable), 1=Record (historical, immutable), 2=Archived (obsolete, hidden from searches)';

-- Add partial index for efficient filtering of non-archived memories in searches
-- This index covers the common case where we want to exclude archived content
CREATE INDEX IF NOT EXISTS idx_memories_active_archetype
ON memories(archetype)
WHERE archetype != 2;

-- Add composite index for searching within active memories by owner
-- Improves performance of owner-scoped searches that exclude archived
CREATE INDEX IF NOT EXISTS idx_memories_owner_active
ON memories(owner_type, owner_id)
WHERE archetype != 2;
