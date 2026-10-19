ALTER TABLE app_settings ADD COLUMN center_theme TEXT NOT NULL DEFAULT 'light'
    CHECK (center_theme IN ('light', 'ink', 'blue', 'system'));
ALTER TABLE app_settings ADD COLUMN template_catalog_json TEXT NOT NULL DEFAULT '[]'
    CHECK (json_valid(template_catalog_json));