import test from "node:test";
import assert from "node:assert/strict";
import { buildPosterMarkup } from "../app/src/poster-core.js";

test("poster core renders locked native surface without demo handles", () => {
  const markup = buildPosterMarkup({
    title: "NIGHT OPS",
    subtitle: "TACTICAL ARCHIVE / 01",
    theme: "nocturne",
    accent: "#72e4ff",
    opacity: 94,
    density: 1,
    width: 490,
    height: 392,
    collapsed: false,
    locked: true,
    items: [{ title: "<b>AL</b>", mark: "AL", hue: "cyan" }]
  }, { surface: "native" });

  assert.match(markup, /class="poster theme-nocturne is-locked"/);
  assert.match(markup, /data-native-drag/);
  assert.doesNotMatch(markup, /data-resize-handle/);
  assert.match(markup, /&lt;b&gt;AL&lt;\/b&gt;/);
  assert.doesNotMatch(markup, /<b>AL<\/b>/);
  assert.match(markup, /aria-pressed="true"/);
});

test("poster core renders selected demo surface with geometry and edit handle", () => {
  const markup = buildPosterMarkup({
    id: "night-ops",
    x: 72,
    y: 154,
    width: 490,
    height: 392,
    collapsed: true,
    locked: false,
    theme: "nocturne",
    accent: "#72e4ff",
    opacity: 94,
    density: 1,
    items: []
  }, {
    surface: "demo",
    posterId: "night-ops",
    posterIndex: 1,
    isSelected: true
  });

  assert.match(markup, /data-poster-id="night-ops"/);
  assert.match(markup, /data-drag-handle/);
  assert.match(markup, /data-resize-handle/);
  assert.match(markup, /is-collapsed is-selected/);
  assert.match(markup, /--poster-x:72px/);
  assert.match(markup, /--poster-index:1/);
  assert.match(markup, /00 TITLES/);
});
