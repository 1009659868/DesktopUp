import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import {
  hostEnvelopeForPresentation,
  POSTER_GEOMETRY_POLICY,
  resolveDefaultPosterGeometry
} from "../app/src/poster-geometry.js";

const GEOMETRY_MATRIX = JSON.parse(readFileSync(
  new URL("./fixtures/poster-geometry-v1.json", import.meta.url),
  "utf8"
));

function assertClose(actual, expected, label) {
  assert.ok(Math.abs(actual - expected) <= 1e-12, `${label}: expected ${expected}, received ${actual}`);
}

test("C-PCB-02 freezes the accepted geometry policy values", () => {
  assert.deepEqual(POSTER_GEOMETRY_POLICY.safeInsets, { top: 24, right: 24, bottom: 24 });
  assert.equal(POSTER_GEOMETRY_POLICY.minimumWidth, 240);
  assert.equal(POSTER_GEOMETRY_POLICY.minimumExpandedHeight, 240);
  assert.equal(POSTER_GEOMETRY_POLICY.collapsedContentHeight, 60);
  assert.equal(POSTER_GEOMETRY_POLICY.surfaceGutter, 10);
  assert.equal(POSTER_GEOMETRY_POLICY.collapsedHostHeight, 80);
});

test("JS matches the shared poster-geometry-v1 decision matrix", () => {
  for (const sample of GEOMETRY_MATRIX) {
    const result = resolveDefaultPosterGeometry(sample.workArea);
    assert.equal(result.ok, sample.valid, sample.name);
    if (!sample.valid) {
      assert.equal(result.errors.some((error) => error.code === sample.errorCode), true, sample.name);
      continue;
    }
    assert.deepEqual(result.value.posterRect, sample.expected.posterRect, sample.name);
    assert.deepEqual(result.value.expandedHost, sample.expected.expandedHost, sample.name);
    assert.deepEqual(result.value.collapsedHost, sample.expected.collapsedHost, sample.name);
    assertClose(result.value.normalizedRect.x, sample.expected.normalizedRect.x, `${sample.name} x`);
    assertClose(result.value.normalizedRect.y, sample.expected.normalizedRect.y, `${sample.name} y`);
  }
});

test("collapse and expand retain the same host top-left and request preserve placement", () => {
  const geometry = resolveDefaultPosterGeometry({
    displayId: "primary",
    isPrimary: true,
    width: 1920,
    height: 1080
  }).value;
  const expanded = hostEnvelopeForPresentation(geometry, "expanded");
  const collapsed = hostEnvelopeForPresentation(geometry, "collapsed");

  assert.deepEqual(collapsed.offset, expanded.offset);
  assert.deepEqual(expanded.placementIntent, { kind: "preserve" });
  assert.deepEqual(collapsed.placementIntent, { kind: "preserve" });
  assert.equal(collapsed.logicalSize.height, 60 + 10 * 2);
  assert.equal(expanded.logicalSize.height, 1080 - 24 - 24 + 10 * 2);
});

test("invalid presentation modes and unresolved geometry fail explicitly", () => {
  assert.throws(() => hostEnvelopeForPresentation({}, "expanded"), /resolved poster geometry/);
  assert.throws(() => hostEnvelopeForPresentation({}, "hidden"), /expanded or collapsed/);
});
