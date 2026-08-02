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
  return {
    ...poster,
    theme: THEMES.includes(poster.theme) ? poster.theme : "nocturne",
    accent: /^#[\da-f]{6}$/i.test(poster.accent) ? poster.accent : "#72e4ff",
    opacity: clamp(Number(poster.opacity) || 94, 72, 100),
    density: clamp(Number(poster.density) || 1, 0, 2),
    x: Number(poster.x) || 0,
    y: Number(poster.y) || 0,
    width: clamp(Number(poster.width) || 420, 330, 640),
    height: clamp(Number(poster.height) || 350, 240, 560),
    collapsed: Boolean(poster.collapsed),
    locked: poster.locked === undefined ? true : Boolean(poster.locked),
    items: Array.isArray(poster.items) ? poster.items : []
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
    width: clamp(nextSize.width, 330, 640),
    height: clamp(nextSize.height, 240, 560)
  };
}
