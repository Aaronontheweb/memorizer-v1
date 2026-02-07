-- 018_add_data_migrations_tracking.sql: Track one-time data migrations
-- Data migrations are distinct from schema migrations:
-- - Schema migrations alter the database structure (CREATE/ALTER TABLE, indexes, etc.)
-- - Data migrations perform one-time data operations (backfilling, seeding, transformations)
-- This table tracks which data migrations have been executed to ensure they only run once.

CREATE TABLE IF NOT EXISTS data_migrations (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL UNIQUE,
    description TEXT,
    executed_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Add index for fast lookups by migration name
CREATE INDEX IF NOT EXISTS idx_data_migrations_name ON data_migrations(name);

-- Add comment explaining the table's purpose
COMMENT ON TABLE data_migrations IS 'Tracks one-time data migrations to ensure idempotency. Each named migration runs exactly once.';
COMMENT ON COLUMN data_migrations.name IS 'Unique identifier for the data migration (e.g., "seed_project_system_memories_v1")';
COMMENT ON COLUMN data_migrations.description IS 'Human-readable description of what the migration does';
COMMENT ON COLUMN data_migrations.executed_at IS 'Timestamp when the migration was executed';
