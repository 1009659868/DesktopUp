CREATE TABLE posters (
    poster_id TEXT PRIMARY KEY NOT NULL,
    title TEXT NOT NULL,
    subtitle TEXT NOT NULL,
    monitor_device_name TEXT NOT NULL,
    fallback_x_dip REAL NOT NULL,
    fallback_y_dip REAL NOT NULL,
    width_dip REAL NOT NULL CHECK (width_dip >= 240.0),
    expanded_height_dip REAL NOT NULL CHECK (expanded_height_dip >= 240.0),
    horizontal_anchor REAL NOT NULL CHECK (horizontal_anchor >= 0.0 AND horizontal_anchor <= 1.0),
    vertical_anchor REAL NOT NULL CHECK (vertical_anchor >= 0.0 AND vertical_anchor <= 1.0),
    normalized_width REAL NOT NULL CHECK (normalized_width > 0.0 AND normalized_width <= 1.0),
    normalized_expanded_height REAL NOT NULL CHECK (normalized_expanded_height > 0.0 AND normalized_expanded_height <= 1.0),
    is_locked INTEGER NOT NULL CHECK (is_locked IN (0, 1)),
    is_collapsed INTEGER NOT NULL CHECK (is_collapsed IN (0, 1)),
    auto_collapse_enabled INTEGER NOT NULL CHECK (auto_collapse_enabled IN (0, 1)),
    layout_kind TEXT NOT NULL,
    layout_parameters_json TEXT NOT NULL CHECK (json_valid(layout_parameters_json)),
    template_key TEXT NOT NULL,
    state_revision INTEGER NOT NULL CHECK (state_revision >= 0),
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL
) STRICT;

CREATE TABLE app_settings (
    singleton_id INTEGER PRIMARY KEY NOT NULL CHECK (singleton_id = 1),
    dynamic_effects_paused INTEGER NOT NULL CHECK (dynamic_effects_paused IN (0, 1)),
    performance_mode TEXT NOT NULL CHECK (performance_mode IN ('automatic', 'static', '30fps')),
    launch_at_startup_requested INTEGER NOT NULL CHECK (launch_at_startup_requested IN (0, 1)),
    state_revision INTEGER NOT NULL CHECK (state_revision >= 0),
    updated_utc TEXT NOT NULL
) STRICT;

INSERT INTO app_settings (
    singleton_id,
    dynamic_effects_paused,
    performance_mode,
    launch_at_startup_requested,
    state_revision,
    updated_utc)
VALUES (
    1,
    0,
    'automatic',
    0,
    0,
    strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));