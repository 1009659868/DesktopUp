import test from "node:test";
import assert from "node:assert/strict";
import {
  clamp,
  gridColumns,
  initials,
  moveWithinBounds,
  normalizedPoster,
  resizeWithinBounds
} from "../app/src/model.js";

test("clamp keeps values inside inclusive bounds", () => {
  assert.equal(clamp(-2, 0, 10), 0);
  assert.equal(clamp(6, 0, 10), 6);
  assert.equal(clamp(20, 0, 10), 10);
});

test("poster normalization rejects invalid theme and visual values", () => {
  const poster = normalizedPoster({
    theme: "unknown",
    accent: "red",
    opacity: 150,
    density: -1,
    width: 10,
    height: 9999,
    items: null
  });
  assert.equal(poster.theme, "nocturne");
  assert.equal(poster.accent, "#72e4ff");
  assert.equal(poster.opacity, 100);
  assert.equal(poster.density, 0);
  assert.equal(poster.width, 240);
  assert.equal(poster.height, 2160);
  assert.equal(poster.locked, false);
  assert.deepEqual(poster.items, []);
});

test("poster lock is normalized and defaults to the V1 unlocked state", () => {
  assert.equal(normalizedPoster({ locked: false }).locked, false);
  assert.equal(normalizedPoster({}).locked, false);
  assert.equal(normalizedPoster({ locked: "yes" }).locked, true);
});

test("grid density produces deterministic column counts", () => {
  assert.equal(gridColumns(420, 0), 4);
  assert.equal(gridColumns(420, 1), 3);
  assert.equal(gridColumns(420, 2), 3);
});

test("movement leaves a visible recovery handle", () => {
  const moved = moveWithinBounds(
    { width: 400, height: 300 },
    { width: 1000, height: 700 },
    { x: -999, y: 999 }
  );
  assert.deepEqual(moved, { x: -324, y: 624 });
});

test("resize preserves large V1 posters inside the storage envelope", () => {
  assert.deepEqual(resizeWithinBounds({}, { width: 50, height: 9000 }), {
    width: 240,
    height: 2160
  });
  assert.equal(resizeWithinBounds({}, { width: 469, height: 1136 }).height, 1136);
});

test("initials are stable for one or two word titles", () => {
  assert.equal(initials("Night Ops"), "NO");
  assert.equal(initials("Dredge"), "D");
});
