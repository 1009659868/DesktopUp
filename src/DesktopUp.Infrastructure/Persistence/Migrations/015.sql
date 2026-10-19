CREATE TABLE content_icon_assets (
    asset_id TEXT PRIMARY KEY NOT NULL,
    format_version INTEGER NOT NULL CHECK(format_version=1),
    width INTEGER NOT NULL CHECK(width BETWEEN 1 AND 256),
    height INTEGER NOT NULL CHECK(height BETWEEN 1 AND 256),
    pixels BLOB NOT NULL CHECK(length(pixels)=width*height*4)
) STRICT;
ALTER TABLE content_poster_items ADD COLUMN icon_asset_id TEXT NULL REFERENCES content_icon_assets(asset_id);