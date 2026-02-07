-- Extend memories table with polymorphic owner, archetype, and memory_type
-- Uses extend-only design: old 'type' column renamed to 'type_legacy'
-- Polymorphic owner pattern: owner_type enum (SMALLINT) + owner_id UUID (both NOT NULL)

-- Enum value mappings (stored as SMALLINT for efficiency):
-- owner_type: 0 = Workspace, 1 = Project
-- archetype: 0 = Document, 1 = Record
-- memory_type: 0 = Checklist, 1 = WorkLog, 2 = Standard, 3 = Specification, 4 = TodoList, 5 = Reference

-- Rename old type column (extend-only design)
ALTER TABLE memories RENAME COLUMN type TO type_legacy;

-- Add polymorphic owner columns (NOT NULL - every memory has an owner)
-- Default to Unfiled workspace (00000000-0000-0000-0000-000000000000) with owner_type=0 (Workspace)
ALTER TABLE memories
    ADD COLUMN owner_type SMALLINT NOT NULL DEFAULT 0 CHECK (owner_type IN (0, 1)),
    ADD COLUMN owner_id UUID NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

-- Add archetype column (document=0, record=1)
ALTER TABLE memories
    ADD COLUMN archetype SMALLINT NOT NULL DEFAULT 0 CHECK (archetype IN (0, 1));

-- Add memory_type enum column
ALTER TABLE memories
    ADD COLUMN memory_type SMALLINT NOT NULL DEFAULT 5 CHECK (memory_type >= 0 AND memory_type <= 5);

-- Indexes for efficient owner queries
CREATE INDEX IF NOT EXISTS idx_memories_owner ON memories(owner_type, owner_id);
CREATE INDEX IF NOT EXISTS idx_memories_memory_type ON memories(memory_type);
CREATE INDEX IF NOT EXISTS idx_memories_archetype ON memories(archetype);

-- Partial index for unfiled memories (owned by the Unfiled workspace)
-- Optimizes common query pattern: "show me memories not yet organized"
-- owner_type=0 (Workspace), owner_id=Unfiled UUID
CREATE INDEX IF NOT EXISTS idx_memories_unfiled ON memories(created_at DESC)
    WHERE owner_type = 0 AND owner_id = '00000000-0000-0000-0000-000000000000';

-- Best-effort type migration from freeform to enum
-- All existing memories default to Unfiled workspace via DEFAULT above
-- memory_type values: 0=Checklist, 1=WorkLog, 2=Standard, 3=Specification, 4=TodoList, 5=Reference
UPDATE memories SET memory_type = CASE
    WHEN type_legacy ILIKE '%checklist%' THEN 0
    WHEN type_legacy ILIKE '%work%log%' OR type_legacy ILIKE '%session%' THEN 1
    WHEN type_legacy ILIKE '%standard%' OR type_legacy ILIKE '%guideline%' THEN 2
    WHEN type_legacy ILIKE '%spec%' OR type_legacy ILIKE '%plan%' OR type_legacy ILIKE '%implementation%' THEN 3
    WHEN type_legacy ILIKE '%todo%' OR type_legacy ILIKE '%task%' THEN 4
    ELSE 5  -- Reference (default)
END;

-- Add comments for documentation
COMMENT ON COLUMN memories.type_legacy IS 'Deprecated: Original freeform type field, preserved for backwards compatibility';
COMMENT ON COLUMN memories.owner_type IS 'Polymorphic owner discriminator: 0=Workspace, 1=Project';
COMMENT ON COLUMN memories.owner_id IS 'UUID of owning workspace or project';
COMMENT ON COLUMN memories.archetype IS '0=Document (living, editable), 1=Record (historical, immutable)';
COMMENT ON COLUMN memories.memory_type IS 'Structured type: 0=Checklist, 1=WorkLog, 2=Standard, 3=Specification, 4=TodoList, 5=Reference';
