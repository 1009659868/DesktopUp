import test from "node:test";
import assert from "node:assert/strict";
import {
  containPosterInWorkArea,
  movePosterFromEditorDelta,
  projectPosterToEditor,
  resizePosterFromEditorDelta
} from "../app/src/poster-editor-geometry.js";

const WORK_AREA = Object.freeze({ width: 2560, height: 1392 });
const EDITOR_STAGE = Object.freeze({ width: 1136, height: 900 });

function assertClose(actual, expected, message) {
  assert.ok(Math.abs(actual - expected) < 1e-9, `${message}: ${actual} !== ${expected}`);
}

test("near-full-height desktop posters remain fully visible in the control-center viewport", () => {
  const poster = {
    x: WORK_AREA.width - 563,
    y: 24,
    width: 563,
    height: 1344,
    collapsed: false
  };
  const projection = projectPosterToEditor(poster, {
    stage: EDITOR_STAGE,
    workArea: WORK_AREA
  });

  assert.ok(projection.scale < 1);
  assert.ok(projection.positionScaleX > 0);
  assert.ok(projection.positionScaleY > 0);
  assert.ok(projection.x >= projection.viewport.x);
  assert.ok(projection.y >= projection.viewport.y);
  assert.ok(projection.x + projection.renderedWidth <= projection.viewport.x + projection.viewport.width);
  assert.ok(projection.y + projection.renderedHeight <= projection.viewport.y + projection.viewport.height);
});

test("legacy-size posters keep a readable one-to-one editor scale", () => {
  const projection = projectPosterToEditor({
    x: 72,
    y: 154,
    width: 490,
    height: 392,
    collapsed: false
  }, {
    stage: EDITOR_STAGE,
    workArea: { width: 1920, height: 1080 }
  });

  assert.equal(projection.scale, 1);
  assert.equal(projection.renderedWidth, 490);
  assert.equal(projection.renderedHeight, 392);
});

test("editor movement reverses to desktop coordinates without persistence snap-back", () => {
  const poster = {
    x: WORK_AREA.width - 563,
    y: 24,
    width: 563,
    height: 1344,
    collapsed: false
  };
  const projection = projectPosterToEditor(poster, {
    stage: EDITOR_STAGE,
    workArea: WORK_AREA
  });
  const moved = movePosterFromEditorDelta(
    poster,
    { x: -80, y: 8 },
    projection,
    WORK_AREA
  );
  const movedProjection = projectPosterToEditor(moved, {
    stage: EDITOR_STAGE,
    workArea: WORK_AREA,
    scale: projection.scale
  });

  assertClose(movedProjection.x - projection.x, -80, "horizontal editor delta");
  assertClose(movedProjection.y - projection.y, 8, "vertical editor delta");
  assert.ok(moved.x >= 0 && moved.x <= WORK_AREA.width - moved.width);
  assert.ok(moved.y >= 0 && moved.y <= WORK_AREA.height - moved.height);
});

test("editor resize deltas use the frozen preview scale and retain desktop containment", () => {
  const poster = { x: 100, y: 100, width: 490, height: 392, collapsed: false };
  const projection = projectPosterToEditor(poster, {
    stage: EDITOR_STAGE,
    workArea: WORK_AREA
  });
  const resized = resizePosterFromEditorDelta(
    poster,
    { x: 50, y: 80 },
    projection,
    WORK_AREA
  );

  assert.equal(projection.scale, 1);
  assert.deepEqual(resized, { ...poster, width: 540, height: 472 });
  assert.deepEqual(
    containPosterInWorkArea({ ...resized, x: 9999, y: -20 }, WORK_AREA),
    { ...resized, x: WORK_AREA.width - resized.width, y: 0 }
  );
});

test("collapsed posters use their visible editor height instead of the expanded storage height", () => {
  const projection = projectPosterToEditor({
    x: WORK_AREA.width - 563,
    y: 24,
    width: 563,
    height: 1344,
    collapsed: true
  }, {
    stage: EDITOR_STAGE,
    workArea: WORK_AREA
  });

  assert.equal(projection.scale, 1);
  assert.equal(projection.renderedHeight, 116);
});
