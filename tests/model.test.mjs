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
    height: 999,
    items: null
  });
  assert.equal(poster.theme, "nocturne");
  assert.equal(poster.accent, "#72e4ff");
  assert.equal(poster.opacity, 100);
  assert.equal(poster.density, 0);
  assert.equal(poster.width, 330);
  assert.equal(poster.height, 560);
  assert.equal(poster.locked, true);
  assert.deepEqual(poster.items, []);
});

test("poster lock is normalized and defaults to safe locked state", () => {
  assert.equal(normalizedPoster({ locked: false }).locked, false);
  assert.equal(normalizedPoster({}).locked, true);
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

test("resize respects supported poster range", () => {
  assert.deepEqual(resizeWithinBounds({}, { width: 50, height: 900 }), {
    width: 330,
    height: 560
  });
});

test("initials are stable for one or two word titles", () => {
  assert.equal(initials("Night Ops"), "NO");
  assert.equal(initials("Dredge"), "D");
});
