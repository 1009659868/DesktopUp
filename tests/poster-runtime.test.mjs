import test from "node:test";
import assert from "node:assert/strict";
import {
  applyPosterAction,
  buildHostSurfaceSyncArgs,
  buildInteractionRegionArgs,
  buildNativeResizeArgs,
  buildRuntimeSnapshot,
  buildRuntimeSyncArgs,
  createRuntimeState,
  isRuntimeSuspended,
  measuredInteractionRegion,
  runtimeMode,
  posterInteractionMode,
  posterSnapshot,
  selectedPoster
} from "../app/src/poster-runtime.js";
import { createLegacyDefaultSnapshot } from "../app/src/poster-legacy-bridge.js";

test("runtime state starts stopped until the native host surface is ready", () => {
  const state = createRuntimeState(createLegacyDefaultSnapshot());
  assert.equal(state.editMode, false);
  assert.equal(state.manuallyPaused, false);
  assert.equal(state.automaticallyPaused, false);
  assert.equal(isRuntimeSuspended(state), true);
  assert.equal(state.runtimeLifecycle.phase, "starting");
  assert.equal(state.runtimeLifecycle.mode, "stopped");
  assert.equal(state.performanceMode, "auto");
  assert.equal(state.contractVersion, 1);
  assert.equal(selectedPoster(state).id, "night-ops");
  assert.equal(posterInteractionMode(state), "using");
});

test("edit mode is derived from the shared interaction contract", () => {
  let state = createRuntimeState(createLegacyDefaultSnapshot());
  state = applyPosterAction(state, {
    type: "patchPoster",
    posterId: "night-ops",
    patch: { locked: false }
  });
  state = applyPosterAction(state, { type: "toggleEditMode" });
  assert.equal(posterInteractionMode(state), "editing");

  state = applyPosterAction(state, {
    type: "patchPoster",
    posterId: "night-ops",
    patch: { locked: true }
  });
  assert.equal(posterInteractionMode(state), "using", "locked posters stay in USING");
});

test("lock and collapse actions are shared state transitions", () => {
  let state = createRuntimeState(createLegacyDefaultSnapshot());
  state = applyPosterAction(state, {
    type: "patchPoster",
    posterId: "night-ops",
    patch: { collapsed: true }
  });
  assert.equal(selectedPoster(state).collapsed, true);

  state = applyPosterAction(state, { type: "setAllLocked", locked: false });
  assert.equal(state.posters.every((poster) => poster.locked === false), true);
  assert.equal(state.allLocked, false);
  assert.equal(buildRuntimeSyncArgs(state).allLocked, false);

  state = applyPosterAction(state, { type: "setAllLocked", locked: true });
  assert.equal(state.allLocked, true);
  assert.equal(buildRuntimeSyncArgs(state).allLocked, true);
});

test("move and resize go through the same geometry ownership layer", () => {
  let state = createRuntimeState(createLegacyDefaultSnapshot());
  const stage = { width: 1000, height: 700 };


  state = applyPosterAction(state, {
    type: "movePoster",
    posterId: "night-ops",
    stage,
    next: { x: 99999, y: 100 }
  });
  assert.ok(selectedPoster(state).x <= 924, "poster must stay inside the work area");

  state = applyPosterAction(state, {
    type: "resizePoster",
    posterId: "night-ops",
    next: { width: 50, height: 900 }
  });
  assert.equal(selectedPoster(state).width, 240);
  assert.equal(selectedPoster(state).height, 900);
});

test("persisted snapshot excludes ephemeral runtime fields", () => {
  const state = createRuntimeState(createLegacyDefaultSnapshot());
  const snapshot = posterSnapshot(state);
  assert.equal("editMode" in snapshot, false);
  assert.equal("manuallyPaused" in snapshot, false);
  assert.equal("automaticallyPaused" in snapshot, false);
  assert.equal("effectivePaused" in snapshot, false);
  assert.equal("allLocked" in snapshot, false);
  assert.equal("posterVisible" in snapshot, false);
  assert.equal("pauseReasons" in snapshot, false);
  assert.equal("contractVersion" in snapshot, false);
  assert.ok(Array.isArray(snapshot.posters));
});

test("Rust RuntimeSnapshot payload maps into the shared frontend runtime", () => {
  let state = createRuntimeState(createLegacyDefaultSnapshot());
  state = applyPosterAction(state, {
    type: "applyRuntimeSnapshot",
    snapshot: {
      editMode: true,
      manuallyPaused: true,
      automaticallyPaused: true,
      effectivePaused: true,
      allLocked: false,
      posterVisible: true,
      hostSurfaceReady: true,
      pauseReasons: ["fullscreenForeground"],
      runtimeVisibility: {
        automaticallyPaused: true,
        pauseReasons: ["fullscreenForeground"]
      },
      contractVersion: 1,
      hostSurface: { contractVersion: 1, revision: 4, geometryOwner: "nativeHost" },
      displayTopology: { contractVersion: 1, displays: [] },
      hostTopology: { contractVersion: 1, invariantSatisfied: true }
    }
  });

  assert.equal(state.editMode, true);
  assert.equal(state.manuallyPaused, true);
  assert.equal(state.automaticallyPaused, true);
  assert.equal(state.effectivePaused, true);
  assert.equal(state.allLocked, false);
  assert.deepEqual(state.pauseReasons, ["fullscreenForeground"]);
  assert.equal(isRuntimeSuspended(state), true);
  assert.equal(state.hostSurface.revision, 4);
  assert.deepEqual(state.displayTopology.displays, []);
  assert.equal(state.hostTopology.invariantSatisfied, true);
  assert.equal(state.hostSurfaceReady, true);
});

test("runtime snapshots and native sync args use the canonical interface", () => {
  let state = createRuntimeState(createLegacyDefaultSnapshot());
  state = applyPosterAction(state, {
    type: "applyRuntimeSnapshot",
    snapshot: {
      editMode: true,
      manuallyPaused: true,
      automaticallyPaused: false,
      allLocked: false,
      posterVisible: true
    }
  });

  const snapshot = buildRuntimeSnapshot(state);
  assert.equal(snapshot.contractVersion, 1);
  assert.equal(snapshot.editMode, true);
  assert.equal(snapshot.manuallyPaused, true);
  assert.equal(snapshot.automaticallyPaused, false);
  assert.equal(snapshot.effectivePaused, true);
  assert.equal(snapshot.hostSurfaceReady, false);
  assert.deepEqual(buildRuntimeSyncArgs(state), {
    editMode: true,
    manuallyPaused: true,
    allLocked: false,
    performanceMode: "auto"
  });
});

test("performance mode is part of the shared runtime contract", () => {
  let state = createRuntimeState(createLegacyDefaultSnapshot());
  state = applyPosterAction(state, { type: "setPerformanceMode", value: "activeAnimated" });
  assert.equal(state.performanceMode, "activeAnimated");
  assert.equal(buildRuntimeSyncArgs(state).performanceMode, "activeAnimated");

  state = applyPosterAction(state, { type: "setPerformanceMode", value: "not-a-mode" });
  assert.equal(state.performanceMode, "auto");
});

test("native sync arguments are built from the shared layer", () => {
  const state = createRuntimeState(createLegacyDefaultSnapshot());
  const resize = buildNativeResizeArgs(selectedPoster(state), {
    deviceScaleFactor: 1.25,
    surfaceGutter: 10
  });
  assert.equal(resize.deviceScaleFactor, 1.25);
  assert.equal(resize.surfaceGutter, 10);
  assert.equal(resize.width, selectedPoster(state).width);

  const region = buildInteractionRegionArgs(
    { left: 1, top: 2, width: 100, height: 80 },
    { cornerRadius: 16, deviceScaleFactor: 1.25 }
  );
  assert.deepEqual(region, {
    x: 1,
    y: 2,
    width: 100,
    height: 80,
    cornerRadius: 16,
    deviceScaleFactor: 1.25
  });

  const canonical = buildHostSurfaceSyncArgs(
    selectedPoster(state),
    { x: 10, y: 10, width: selectedPoster(state).width, height: selectedPoster(state).height },
    { revision: 2, surfaceGutter: 10, cornerRadius: 18, deviceScaleFactor: 1.25 }
  );
  assert.equal(canonical.request.contractVersion, 1);
  assert.equal(canonical.request.revision, 2);
  assert.equal(canonical.request.placementIntent.kind, "preserve");
});

test("measured interaction regions fit the target poster before the first native resize", () => {
  const region = measuredInteractionRegion({
    regionId: "poster-night-ops-header",
    cornerRadius: 16,
    surfaceRect: { width: 490, height: 392 },
    surfaceSize: { width: 490, height: 392 },
    posterRect: { left: 10, top: 10, width: 470, height: 372 },
    elementRect: { left: 10, top: 10, width: 470, height: 43 },
    visualRect: { x: 10, y: 10, width: 420, height: 560 }
  });

  assert.deepEqual(region, {
    regionId: "poster-night-ops-header",
    rect: { x: 10, y: 10, width: 420, height: 43 },
    cornerRadius: 16
  });
});

test("measured interaction regions reverse presentation scale and skip hidden elements", () => {
  const transformed = measuredInteractionRegion({
    regionId: "poster-night-ops-header",
    cornerRadius: 16,
    surfaceRect: { width: 880, height: 1160 },
    surfaceSize: { width: 440, height: 580 },
    posterRect: { left: 20, top: 20, width: 840, height: 1120 },
    elementRect: { left: 20, top: 20, width: 840, height: 86 },
    visualRect: { x: 10, y: 10, width: 420, height: 560 }
  });
  assert.deepEqual(transformed?.rect, { x: 10, y: 10, width: 420, height: 43 });

  const hidden = measuredInteractionRegion({
    regionId: "poster-night-ops-hidden-tile",
    cornerRadius: 6,
    surfaceRect: { width: 440, height: 136 },
    surfaceSize: { width: 440, height: 136 },
    posterRect: { left: 10, top: 10, width: 420, height: 116 },
    elementRect: { left: 0, top: 0, width: 0, height: 0 },
    visualRect: { x: 10, y: 10, width: 420, height: 116 }
  });
  assert.equal(hidden, null);
});

test("runtime lifecycle rejects stale generations and out-of-order event counts", () => {
  let state = createRuntimeState(createLegacyDefaultSnapshot());
  state = applyPosterAction(state, {
    type: "applyRuntimeSnapshot",
    snapshot: {
      runtimeLifecycle: {
        contractVersion: 1,
        generation: 5,
        eventCount: 10,
        phase: "running",
        mode: "paused",
        manuallyPaused: true,
        automaticallyPaused: false,
        effectivePaused: true,
        posterVisible: true,
        hostSurfaceReady: true,
        pauseReasons: [],
        lastTrigger: "manualPause"
      }
    }
  });

  const current = state;
  state = applyPosterAction(state, {
    type: "applyRuntimeSnapshot",
    snapshot: {
      runtimeLifecycle: {
        ...current.runtimeLifecycle,
        generation: 4,
        eventCount: 99,
        mode: "activeStatic",
        manuallyPaused: false,
        effectivePaused: false
      }
    }
  });
  assert.equal(state, current, "older generation must be ignored");

  state = applyPosterAction(state, {
    type: "applyRuntimeSnapshot",
    snapshot: {
      runtimeLifecycle: {
        ...current.runtimeLifecycle,
        eventCount: 9,
        mode: "activeStatic",
        manuallyPaused: false,
        effectivePaused: false
      }
    }
  });
  assert.equal(state, current, "older event in the same generation must be ignored");

  state = applyPosterAction(state, {
    type: "applyRuntimeSnapshot",
    snapshot: {
      runtimeLifecycle: {
        ...current.runtimeLifecycle,
        eventCount: 11,
        mode: "activeStatic",
        manuallyPaused: false,
        effectivePaused: false,
        lastTrigger: "manualResume"
      }
    }
  });
  assert.notEqual(state, current);
  assert.equal(runtimeMode(state), "activeStatic");
});

test("frontend safety cannot be bypassed by an inconsistent animated request", () => {
  const state = createRuntimeState({
    ...createLegacyDefaultSnapshot(),
    performanceMode: "activeAnimated",
    posterVisible: true,
    hostSurfaceReady: true,
    manuallyPaused: true,
    effectivePaused: true,
    runtimeLifecycle: {
      generation: 1,
      eventCount: 1,
      phase: "running",
      mode: "activeAnimated",
      manuallyPaused: true,
      automaticallyPaused: false,
      effectivePaused: true,
      posterVisible: true,
      hostSurfaceReady: true,
      pauseReasons: []
    }
  });
  assert.equal(runtimeMode(state), "paused");
  assert.equal(isRuntimeSuspended(state), true);
});
