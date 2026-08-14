import { clamp, resizeWithinBounds } from "./model.js";

export const POSTER_EDITOR_INSETS = Object.freeze({
  top: 112,
  right: 28,
  bottom: 54,
  left: 28,
  inner: 12
});

const COLLAPSED_POSTER_HEIGHT = 116;
const MINIMUM_SCALE = 0.01;
const FIT_SCALE_RESERVE = 0.96;

function positive(value, fallback = 1) {
  const number = Number(value);
  return Number.isFinite(number) && number > 0 ? number : fallback;
}

function normalizedAxis(position, span) {
  if (span <= 0) return 0;
  return clamp(Number(position) / span || 0, 0, 1);
}

function editorViewport(stage, insets) {
  const width = positive(stage?.width);
  const height = positive(stage?.height);
  const left = Math.max(0, Number(insets.left) || 0);
  const top = Math.max(0, Number(insets.top) || 0);
  const right = Math.max(0, Number(insets.right) || 0);
  const bottom = Math.max(0, Number(insets.bottom) || 0);
  const inner = Math.max(0, Number(insets.inner) || 0);
  return {
    x: left + inner,
    y: top + inner,
    width: Math.max(1, width - left - right - inner * 2),
    height: Math.max(1, height - top - bottom - inner * 2)
  };
}

export function projectPosterToEditor(poster, {
  stage,
  workArea,
  insets = POSTER_EDITOR_INSETS,
  scale: fixedScale
} = {}) {
  const width = positive(poster?.width);
  const expandedHeight = positive(poster?.height);
  const visualHeight = poster?.collapsed
    ? Math.min(expandedHeight, COLLAPSED_POSTER_HEIGHT)
    : expandedHeight;
  const workWidth = positive(workArea?.width, width);
  const workHeight = positive(workArea?.height, expandedHeight);
  const viewport = editorViewport(stage, insets);
  const availableScale = Math.min(1, viewport.width / width, viewport.height / visualHeight);
  const fitScale = availableScale < 1 ? availableScale * FIT_SCALE_RESERVE : availableScale;
  const scale = Math.max(
    MINIMUM_SCALE,
    Number.isFinite(Number(fixedScale)) ? Number(fixedScale) : fitScale
  );
  const renderedWidth = width * scale;
  const renderedHeight = visualHeight * scale;
  const desktopSpanX = Math.max(0, workWidth - width);
  const desktopSpanY = Math.max(0, workHeight - expandedHeight);
  const editorSpanX = Math.max(0, viewport.width - renderedWidth);
  const editorSpanY = Math.max(0, viewport.height - renderedHeight);
  const normalizedX = normalizedAxis(poster?.x, desktopSpanX);
  const normalizedY = normalizedAxis(poster?.y, desktopSpanY);

  return {
    x: viewport.x + normalizedX * editorSpanX,
    y: viewport.y + normalizedY * editorSpanY,
    scale,
    renderedWidth,
    renderedHeight,
    positionScaleX: desktopSpanX > 0 ? editorSpanX / desktopSpanX : 0,
    positionScaleY: desktopSpanY > 0 ? editorSpanY / desktopSpanY : 0,
    viewport
  };
}

export function containPosterInWorkArea(rect, workArea) {
  const width = positive(rect?.width);
  const height = positive(rect?.height);
  const workWidth = positive(workArea?.width, width);
  const workHeight = positive(workArea?.height, height);
  return {
    ...rect,
    x: clamp(Number(rect?.x) || 0, 0, Math.max(0, workWidth - width)),
    y: clamp(Number(rect?.y) || 0, 0, Math.max(0, workHeight - height)),
    width,
    height
  };
}

export function movePosterFromEditorDelta(rect, delta, projection, workArea) {
  const scaleX = Number(projection?.positionScaleX) || 0;
  const scaleY = Number(projection?.positionScaleY) || 0;
  return containPosterInWorkArea({
    ...rect,
    x: Number(rect?.x) + (scaleX > 0 ? Number(delta?.x) / scaleX : 0),
    y: Number(rect?.y) + (scaleY > 0 ? Number(delta?.y) / scaleY : 0)
  }, workArea);
}

export function resizePosterFromEditorDelta(rect, delta, projection, workArea) {
  const scale = Math.max(MINIMUM_SCALE, Number(projection?.scale) || 1);
  const size = resizeWithinBounds(rect, {
    width: Number(rect?.width) + Number(delta?.x) / scale,
    height: Number(rect?.height) + Number(delta?.y) / scale
  });
  return containPosterInWorkArea({ ...rect, ...size }, workArea);
}
