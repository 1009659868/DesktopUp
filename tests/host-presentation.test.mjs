import test from "node:test";
import assert from "node:assert/strict";
import {
  hostPresentationCssVariables,
  resolveHostPresentation
} from "../app/src/host-presentation.js";

test("native display scale stays separate from CSS responsive presentation", () => {
  const presentation = resolveHostPresentation({
    logicalSize: { width: 510, height: 412 },
    monitorScaleFactor: 1,
    deviceScaleFactor: 1.5,
    rendererScaleFactor: 1.5,
    responsiveScale: 1.25,
    userScale: 0.8,
    effectiveScaleFactor: 1.5
  });
  assert.deepEqual(presentation, {
    logicalWidth: 510,
    logicalHeight: 412,
    responsiveScale: 1.25,
    userScale: 0.8,
    visualScale: 1
  });
});

test("host presentation exports deterministic CSS variables", () => {
  assert.deepEqual(hostPresentationCssVariables({
    logicalSize: { width: 510, height: 136 },
    responsiveScale: 0.8,
    userScale: 1
  }), {
    "--host-logical-width": "510px",
    "--host-logical-height": "136px",
    "--host-visual-scale": "0.8"
  });
});

test("invalid diagnostic values fall back without breaking the surface", () => {
  assert.deepEqual(resolveHostPresentation({
    logicalSize: { width: Number.NaN, height: 0 },
    responsiveScale: Number.NaN,
    userScale: -1
  }), {
    logicalWidth: 1,
    logicalHeight: 1,
    responsiveScale: 1,
    userScale: 1,
    visualScale: 1
  });
});
