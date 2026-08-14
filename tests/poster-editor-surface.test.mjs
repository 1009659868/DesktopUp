import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const main = readFileSync(new URL("../app/src/main.js", import.meta.url), "utf8");
const sharedStyles = readFileSync(new URL("../app/src/styles.css", import.meta.url), "utf8");
const nativeStyles = readFileSync(new URL("../app/src/poster-surface.css", import.meta.url), "utf8");

test("control-center rendering projects desktop geometry into its own viewport", () => {
  assert.match(main, /projectPosterToEditor\(poster, context\)/);
  assert.match(main, /applyEditorPosterProjection\(\)/);
  assert.match(main, /ResizeObserver\(\(\) => applyEditorPosterProjection\(\)\)/);
  assert.match(sharedStyles, /scale\(var\(--poster-scale, 1\)\)/);
});

test("editor coordinates are reversed before the authoritative state is saved", () => {
  assert.match(main, /movePosterFromEditorDelta\([\s\S]*?gesture\.context\.workArea/);
  assert.match(main, /resizePosterFromEditorDelta\([\s\S]*?gesture\.context\.workArea/);
  assert.match(main, /patch:\s*\{\s*x: gesture\.nextRect\.x,\s*y: gesture\.nextRect\.y/);
  assert.doesNotMatch(main, /parseFloat\(posterElement\.style\.getPropertyValue\("--poster-[xy]"\)\)/);
});

test("control-center preview scale cannot leak into the native poster surface", () => {
  assert.match(
    nativeStyles,
    /\.poster-surface \.poster\s*\{[\s\S]*?transform:\s*none;/
  );
});

test("control-center actions consume shared PSI permissions and retain failed local state", () => {
  assert.match(main, /resolveRuntimePosterPermissions/);
  assert.match(main, /permissions\.selectItem/);
  assert.match(main, /permissions\.explicitActivateItem/);
  assert.match(main, /permissions\.togglePresentation/);
  assert.match(main, /permissions\.toggleLock/);
  assert.match(main, /permissions\.dragFromTitlebar/);
  assert.match(main, /permissions\.resizePoster/);
  assert.match(main, /posterState\.localMutationStatus\(\)/);
  assert.match(main, /rejected-retained/);
});
