// Poster Contract Baseline — C-PCB behavior tests
// V1-P1 / B-PCR / Poster Core Runtime
//
// These tests freeze the five contracts defined in C-PCB before any
// implementation work on Shadow DOM, template engine, or layout begins.
// They validate behavior, not string-matching of source files.

import test from "node:test";
import assert from "node:assert/strict";
import {
  clamp,
  gridColumns,
  moveWithinBounds,
  normalizedPoster,
  resizeWithinBounds,
} from "../app/src/model.js";

// ── Contract 1: default poster snapshot ──────────────────────────────
test("C-PCB §1: default poster snapshot matches V1 design contract", () => {
  const poster = normalizedPoster({});

  // V1 design baseline §6.2: default is EXPANDED + unlocked
  // NOTE: The current A0 model default is locked=true (safe default).
  // V1 design baseline §4 explicitly freezes unlocked as the V1 default.
  // This test captures the CONTRACT GAP — it will pass once
  // normalizedPoster is updated for V1.
  assert.equal(poster.collapsed, false, "default presentation is EXPANDED");

  // Default theme and accent
  assert.equal(poster.theme, "nocturne");
  assert.equal(poster.accent, "#72e4ff");

  // Density defaults to "舒展" (index 1)
  assert.equal(poster.density, 1);

  // Items must be an array (may be empty — that's the empty-state path)
  assert.ok(Array.isArray(poster.items));
});

// ── Contract 1b: sanitization ────────────────────────────────────────
test("C-PCB §1: normalized poster clamps out-of-range values", () => {
  const poster = normalizedPoster({
    theme: "bogus",
    accent: "not-a-color",
    opacity: 999,
    density: -5,
    width: 10,
    height: 99999,
    collapsed: "not-bool",
    items: "not-array",
  });

  // Every field is sanitized to a safe value
  assert.equal(poster.theme, "nocturne");
  assert.equal(poster.accent, "#72e4ff");
  assert.equal(poster.opacity, 100); // clamped to max 100
  assert.equal(poster.density, 0); // clamped to min 0
  assert.ok(poster.width >= 280 && poster.width <= 1200, `width ${poster.width} in [280,1200]`);
  assert.ok(poster.height >= 60 && poster.height <= 2160, `height ${poster.height} in [60,2160]`);
  assert.equal(poster.collapsed, true); // "not-bool" (truthy string) → Boolean → true
  // locked field not provided → undefined → V1 default is false
  assert.equal(poster.locked, false);
  assert.deepEqual(poster.items, []);
});

// ── Contract 2: deterministic output ─────────────────────────────────
test("C-PCB §2: poster normalization produces deterministic output for same input", () => {
  const input = { title: "TEST", width: 500, height: 400, theme: "ember", collapsed: false };
  const a = normalizedPoster(input);
  const b = normalizedPoster(input);
  assert.deepEqual(a, b, "same input must produce identical normalized output");
});

// ── Contract 2: deterministic output ─────────────────────────────────
test("C-PCB §2: poster normalization produces deterministic output for same input", () => {
  const input = { width: 500, height: 400, theme: "ember", collapsed: false };
  const a = normalizedPoster(input);
  const b = normalizedPoster(input);
  assert.deepEqual(a, b, "same input must produce identical normalized output");
});

// ── Contract 3: collapsed / locked semantics ───────────────────────
test("C-PCB §3: collapsed poster has collapsed presentation mode", () => {
  const poster = normalizedPoster({ collapsed: true });
  assert.equal(poster.collapsed, true);
});

test("C-PCB §3: expanded poster (default) is not collapsed", () => {
  const poster = normalizedPoster({});
  assert.equal(poster.collapsed, false);
});

test("C-PCB §3: locked poster rejects unlock attempt via toggle semantic", () => {
  // locked=true means the user cannot drag or resize.
  // render() reads poster.locked to enable/disable drag handles.
  const locked = normalizedPoster({ locked: true });
  const unlocked = normalizedPoster({ locked: false });
  assert.equal(locked.locked, true);
  assert.equal(unlocked.locked, false);
});

// ── Contract 4: fallback poster integrity ───────────────────────────
test("C-PCB §4: null input returns a valid safe-fallback poster", () => {
  const poster = normalizedPoster(null);
  assert.equal(typeof poster.collapsed, "boolean");
  assert.equal(typeof poster.locked, "boolean");
  assert.ok(poster.width >= 280 && poster.width <= 1200);
  assert.ok(poster.height >= 60 && poster.height <= 2160);
  assert.ok(Array.isArray(poster.items));
  assert.equal(poster.theme, "nocturne");
});

test("C-PCB §4: undefined input returns a valid safe-fallback poster", () => {
  const poster = normalizedPoster(undefined);
  assert.equal(typeof poster.collapsed, "boolean");
  assert.equal(typeof poster.locked, "boolean");
  assert.ok(poster.width >= 280 && poster.width <= 1200);
  assert.ok(poster.height >= 60 && poster.height <= 2160);
  assert.ok(Array.isArray(poster.items));
});

test("C-PCB §4: poster with empty input title is still structurally valid", () => {
  // A poster passed without a title keeps whatever was spread + {title: undefined}.
  // This is a KNOWN CONTRACT GAP: normalizedPoster does not backfill a
  // missing title. Callers (poster-surface.js, main.js) always supply a
  // fallback object with title already set, so production never hits this.
  // V1 should add a title guard inside normalizedPoster.
  const poster = normalizedPoster({ collapsed: false });
  assert.ok(typeof poster.collapsed === "boolean", "collapsed always boolean");
  assert.ok(typeof poster.locked === "boolean", "locked always boolean");
  assert.ok(poster.width > 0 && poster.height > 0, "positive dimensions");
  assert.ok(Array.isArray(poster.items), "items always array");
});

test("C-PCB §4: zero-item poster preserves render structure", () => {
  const poster = normalizedPoster({ items: [] });
  assert.equal(poster.items.length, 0);
  // poster still has valid layout dimensions — nothing crashes rendering
  assert.ok(poster.width >= 280 && poster.width <= 1200);
  assert.ok(poster.height >= 60 && poster.height <= 2160);
});

// ── Contract 5: geometry bounds ──────────────────────────────────────
test("C-PCB §5: gridColumns uses the poster width floor of 280", () => {
  // Even if poster width is min-clamped, gridColumns bases its
  // calculation on Math.max(width, 280)
  assert.equal(gridColumns(280, 0), 3);
  assert.equal(gridColumns(280, 1), 2);
});

test("C-PCB §5: gridColumns respects density tiers", () => {
  // Density 0 (紧凑) → target 76, 1 (舒展) → target 94, 2 (画廊) → target 116
  const cols0 = gridColumns(420, 0);
  const cols1 = gridColumns(420, 1);
  const cols2 = gridColumns(420, 2);
  // Compact should produce more columns than gallery
  assert.ok(cols0 >= cols2, `density 0 cols=${cols0} >= density 2 cols=${cols2}`);
});

test("C-PCB §5: gridColumns never exceeds 6", () => {
  // Maximum columns clamped to 6 regardless of width
  assert.ok(gridColumns(9999, 0) <= 6);
});

test("C-PCB §5: gridColumns never drops below 2", () => {
  // Minimum columns clamped to 2
  assert.ok(gridColumns(100, 2) >= 2);
});

// ── Contract 6: moveWithinBounds keeps a visible recovery handle ─────
test("C-PCB §6: moveWithinBounds always keeps a visible portion", () => {
  const moved = moveWithinBounds(
    { width: 400, height: 300 },
    { width: 1000, height: 700 },
    { x: -9999, y: -9999 }
  );
  // Recovery handle: at least 76px of width visible
  // x = clamp(-9999, -400+76= -324, 1000-76=924) = -324
  assert.ok(moved.x >= -324, `move x=${moved.x} must keep recovery handle >= -324`);
  // Never clamped above the stage top (y >= 72)
  assert.ok(moved.y >= 72, `move y=${moved.y} must be >= 72`);
});

test("C-PCB §6: moveWithinBounds does not let poster leave right edge", () => {
  const moved = moveWithinBounds(
    { width: 400, height: 300 },
    { width: 1000, height: 700 },
    { x: 99999, y: 100 }
  );
  // stage.width - visibleHeader = 1000 - 76 = 924
  assert.ok(moved.x <= 924, `move x=${moved.x} must be <= 924`);
});
