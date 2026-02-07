-- Create provider_settings table for configuring LLM providers
-- Supports bifurcated architecture: separate embedding and memorizer agent providers

CREATE TABLE IF NOT EXISTS provider_settings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    provider_type TEXT NOT NULL,        -- 'embedding' or 'memorizer_agent'
    provider_name TEXT NOT NULL,        -- 'ollama', 'anthropic', 'openai', etc.
    display_name TEXT,                  -- Human-readable name for UI
    config JSONB NOT NULL DEFAULT '{}', -- Provider-specific configuration
    is_active BOOLEAN DEFAULT false,    -- Only one provider per type can be active
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(provider_type, provider_name)
);

-- Index for efficient active provider lookups
CREATE INDEX IF NOT EXISTS idx_provider_settings_active
ON provider_settings(provider_type, is_active)
WHERE is_active = true;

-- Add comments for documentation
COMMENT ON TABLE provider_settings IS 'Stores configuration for LLM providers (embeddings and memorizer agent)';
COMMENT ON COLUMN provider_settings.provider_type IS 'Type of provider: embedding or memorizer_agent';
COMMENT ON COLUMN provider_settings.provider_name IS 'Provider identifier: ollama, anthropic, openai, etc.';
COMMENT ON COLUMN provider_settings.config IS 'JSON configuration specific to provider (apiUrl, model, timeout, etc.)';
COMMENT ON COLUMN provider_settings.is_active IS 'Whether this provider is currently active for its type';

-- NOTE: Provider settings are now seeded from environment variable configuration
-- at application startup via InitializationService.SeedProviderSettingsFromConfig()
-- This ensures backwards compatibility with existing V1 configurations.
