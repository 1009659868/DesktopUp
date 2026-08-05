export const THEMES = Object.freeze(["nocturne", "ember", "glass"]);
export const DENSITY_LABELS = Object.freeze(["紧凑", "舒展", "画廊"]);

export function clamp(value, minimum, maximum) {
  return Math.min(Math.max(value, minimum), maximum);
}

export function initials(title) {
  return title
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join("")
    .toUpperCase();
}

export function normalizedPoster(poster) {
  // Guard: null/undefined → safe default (C-PCB §4 fallback contract)
  const src = poster ?? {};
  return {
    ...src,
    theme: THEMES.includes(src.theme) ? src.theme : "nocturne",
    accent: /^#[\da-f]{6}$/i.test(src.accent) ? src.accent : "#72e4ff",
    opacity: clamp(Number(src.opacity) || 94, 72, 100),
    density: clamp(Number(src.density) || 1, 0, 2),
    x: Number(src.x) || 0,
    y: Number(src.y) || 0,
    width: clamp(Number(src.width) || 420, 280, 1200),
    height: clamp(Number(src.height) || 800, 60, 2160),
    collapsed: Boolean(src.collapsed),
    locked: src.locked === undefined ? false : Boolean(src.locked),
    items: Array.isArray(src.items) ? src.items : []
  };
}

export function gridColumns(width, density) {
  const target = [76, 94, 116][clamp(Number(density) || 0, 0, 2)];
  return clamp(Math.floor((Math.max(width, 280) - 52) / target), 2, 6);
}

export function moveWithinBounds(rect, stage, nextPosition) {
  const visibleHeader = 76;
  return {
    x: clamp(nextPosition.x, -rect.width + visibleHeader, stage.width - visibleHeader),
    y: clamp(nextPosition.y, 72, stage.height - visibleHeader)
  };
}

export function resizeWithinBounds(rect, nextSize) {
  return {
    width: clamp(nextSize.width, 240, 1200),
    height: clamp(nextSize.height, 60, 2160)
  };
}
