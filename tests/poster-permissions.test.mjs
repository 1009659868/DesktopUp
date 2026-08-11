import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import {
  POSTER_PERMISSION_CONTRACT_VERSION,
  POSTER_TITLEBAR_CONTRACT,
  resolvePosterPermissions
} from "../app/src/poster-permissions.js";

const PERMISSION_MATRIX = JSON.parse(readFileSync(
  new URL("./fixtures/poster-permissions-v1.json", import.meta.url),
  "utf8"
));

function allowedActions(value) {
  return Object.entries(value.permissions)
    .filter(([, allowed]) => allowed)
    .map(([action]) => action);
}

test("C-PCB-03 freezes the custom titlebar role contract", () => {
  assert.equal(POSTER_PERMISSION_CONTRACT_VERSION, 1);
  assert.deepEqual(POSTER_TITLEBAR_CONTRACT.requiredSlots, [
    "title",
    "templateIdentity",
    "itemCount",
    "presentationToggle",
    "lockToggle"
  ]);
  assert.deepEqual(POSTER_TITLEBAR_CONTRACT.dragOrigins, [
    "background",
    "title",
    "templateIdentity",
    "itemCount"
  ]);
  assert.equal(POSTER_TITLEBAR_CONTRACT.dragExclusions.includes("button"), true);
  assert.equal(POSTER_TITLEBAR_CONTRACT.dragExclusions.includes("item"), true);
  assert.equal(POSTER_TITLEBAR_CONTRACT.pointerDragThresholdPx, 5);
});

test("JS matches the shared poster-permissions-v1 decision matrix", () => {
  for (const sample of PERMISSION_MATRIX) {
    const result = resolvePosterPermissions(sample.input);
    assert.equal(result.ok, sample.valid, sample.name);
    if (!sample.valid) {
      assert.equal(result.errors.some((error) => error.code === sample.errorCode), true, sample.name);
      continue;
    }
    assert.equal(result.value.effectiveInteractionMode, sample.effectiveInteractionMode, sample.name);
    assert.equal(result.value.runtimeInteractive, sample.allowedActions.length > 0, sample.name);
    assert.deepEqual(allowedActions(result.value), sample.allowedActions, sample.name);
  }
});

test("locked editing resolves to USING without blocking daily controls or launch", () => {
  const result = resolvePosterPermissions({
    presentationMode: "expanded",
    interactionMode: "editing",
    runtimeMode: "activeStatic",
    isLocked: true
  });
  assert.equal(result.ok, true);
  assert.equal(result.value.effectiveInteractionMode, "using");
  assert.equal(result.value.permissions.toggleLock, true);
  assert.equal(result.value.permissions.togglePresentation, true);
  assert.equal(result.value.permissions.singleActivateItem, true);
  assert.equal(result.value.permissions.dragFromTitlebar, false);
  assert.equal(result.value.permissions.resizePoster, false);
  assert.equal(result.value.permissions.reorderItem, false);
});

test("editing separates single-click selection from explicit item activation", () => {
  const result = resolvePosterPermissions({
    presentationMode: "expanded",
    interactionMode: "editing",
    runtimeMode: "activeAnimated",
    isLocked: false
  });
  assert.equal(result.ok, true);
  assert.equal(result.value.permissions.singleActivateItem, false);
  assert.equal(result.value.permissions.explicitActivateItem, true);
  assert.equal(result.value.permissions.selectItem, true);
  assert.equal(result.value.permissions.reorderItem, true);
  assert.equal(result.value.permissions.resizePoster, true);
});

test("runtime suspension and malformed lock input fail closed", () => {
  const paused = resolvePosterPermissions({
    presentationMode: "expanded",
    interactionMode: "using",
    runtimeMode: "paused",
    isLocked: false
  });
  assert.equal(paused.ok, true);
  assert.deepEqual(allowedActions(paused.value), []);

  const malformed = resolvePosterPermissions({
    presentationMode: "expanded",
    interactionMode: "using",
    runtimeMode: "activeStatic",
    isLocked: "false"
  });
  assert.equal(malformed.ok, false);
  assert.equal(malformed.errors[0].code, "invalid_lock_state");
});
