import { moveWithinBounds, normalizedPoster, resizeWithinBounds } from "./model.js";
import {
  HOST_CONTRACT_VERSION,
  SURFACE_REGISTRY_VERSION,
  createHostSurfaceRequest
} from "./host-contract.js";

export const PERFORMANCE_MODES = Object.freeze([
  "auto",
  "activeStatic",
  "activeAnimated",
  "paused",
  "stopped"
]);

export const RUNTIME_LIFECYCLE_CONTRACT_VERSION = 1;
const RUNTIME_PHASES = Object.freeze(["starting", "running", "exiting"]);
const RUNTIME_MODES = Object.freeze([
  "activeStatic",
  "activeAnimated",
  "paused",
  "stopped"
]);

function requestedRuntimeMode(input) {
  if (input.phase === "exiting" || !input.posterVisible || !input.hostSurfaceReady) {
    return "stopped";
  }
  if (input.manuallyPaused || input.automaticallyPaused || input.performanceMode === "paused") {
    return "paused";
  }
  if (input.performanceMode === "stopped") return "stopped";
  if (input.performanceMode === "activeAnimated") return "activeAnimated";
  if (input.performanceMode === "activeStatic") return "activeStatic";
  return Number(input.posterFps) > 0 ? "activeAnimated" : "activeStatic";
}

function normalizedRuntimeLifecycle(snapshot = {}, fallback = {}) {
  const raw = snapshot.runtimeLifecycle ?? {};
  const previous = fallback.runtimeLifecycle ?? {};
  const visibility = snapshot.runtimeVisibility ?? fallback.runtimeVisibility ?? {};
  const manuallyPaused = Boolean(
    raw.manuallyPaused ?? snapshot.manuallyPaused ?? fallback.manuallyPaused ?? false
  );
  const automaticallyPaused = Boolean(
    raw.automaticallyPaused
      ?? snapshot.automaticallyPaused
      ?? visibility.automaticallyPaused
      ?? fallback.automaticallyPaused
      ?? false
  );
  const posterVisible = Boolean(
    raw.posterVisible ?? snapshot.posterVisible ?? fallback.posterVisible ?? true
  );
  const hostSurfaceReady = Boolean(
    raw.hostSurfaceReady ?? snapshot.hostSurfaceReady ?? fallback.hostSurfaceReady ?? false
  );
  const pauseReasons =
    raw.pauseReasons ?? snapshot.pauseReasons ?? visibility.pauseReasons ?? fallback.pauseReasons ?? [];
  const phase = RUNTIME_PHASES.includes(raw.phase)
    ? raw.phase
    : hostSurfaceReady ? "running" : "starting";
  const performanceMode = PERFORMANCE_MODES.includes(snapshot.performanceMode)
    ? snapshot.performanceMode
    : fallback.performanceMode ?? "auto";
  const posterFps = Number(snapshot.posterFps ?? fallback.posterFps ?? 0);
  const derivedMode = requestedRuntimeMode({
    phase,
    posterVisible,
    hostSurfaceReady,
    manuallyPaused,
    automaticallyPaused,
    performanceMode,
    posterFps
  });

  return {
    contractVersion:
      raw.contractVersion ?? previous.contractVersion ?? RUNTIME_LIFECYCLE_CONTRACT_VERSION,
    generation: Number.isSafeInteger(raw.generation)
      ? raw.generation
      : previous.generation ?? 0,
    eventCount: Number.isSafeInteger(raw.eventCount)
      ? raw.eventCount
      : previous.eventCount ?? 0,
    phase,
    mode: RUNTIME_MODES.includes(raw.mode) ? raw.mode : derivedMode,
    manuallyPaused,
    automaticallyPaused,
    effectivePaused: Boolean(
      raw.effectivePaused
        ?? snapshot.effectivePaused
        ?? (manuallyPaused || automaticallyPaused)
    ),
    posterVisible,
    hostSurfaceReady,
    pauseReasons: Array.isArray(pauseReasons) ? pauseReasons : [],
    lastTrigger: raw.lastTrigger ?? previous.lastTrigger ?? "frontendFallback"
  };
}

function withLocalLifecycle(state, patch = {}) {
  const next = { ...state, ...patch };
  const lifecycle = normalizedRuntimeLifecycle({
    ...next,
    runtimeLifecycle: {
      ...state.runtimeLifecycle,
      ...patch,
      mode: undefined,
      phase: state.runtimeLifecycle?.phase
    }
  }, state);
  lifecycle.mode = requestedRuntimeMode({
    ...next,
    ...lifecycle,
    performanceMode: next.performanceMode,
    posterFps: next.posterFps
  });
  return { ...next, runtimeLifecycle: lifecycle };
}

export function deriveAllLocked(posters) {
  return Array.isArray(posters)
    && posters.length > 0
    && posters.every((poster) => poster.locked);
}

export function createRuntimeState(snapshot) {
  const posters = Array.isArray(snapshot?.posters)
    ? snapshot.posters.map(normalizedPoster)
    : [];
  const visibility = snapshot?.runtimeVisibility ?? {};
  const runtimeLifecycle = normalizedRuntimeLifecycle(snapshot ?? {});

  return {
    contractVersion: snapshot?.contractVersion ?? HOST_CONTRACT_VERSION,
    selectedPosterId: snapshot?.selectedPosterId ?? posters[0]?.id ?? null,
    posters,
    editMode: Boolean(snapshot?.editMode ?? false),
    manuallyPaused: runtimeLifecycle.manuallyPaused,
    automaticallyPaused: runtimeLifecycle.automaticallyPaused,
    effectivePaused: runtimeLifecycle.effectivePaused,
    allLocked: deriveAllLocked(posters),
    posterVisible: runtimeLifecycle.posterVisible,
    hostSurfaceReady: runtimeLifecycle.hostSurfaceReady,
    pauseReasons: runtimeLifecycle.pauseReasons,
    posterFps: Number(snapshot?.posterFps ?? 0),
    performanceMode: PERFORMANCE_MODES.includes(snapshot?.performanceMode)
      ? snapshot.performanceMode
      : "auto",
    runtimeLifecycle,
    runtimeVisibility: visibility,
    hostSurface: snapshot?.hostSurface ?? null,
    displayTopology: snapshot?.displayTopology ?? null,
    hostTopology: snapshot?.hostTopology ?? null,
    desktopHost: snapshot?.desktopHost ?? null
  };
}

export function selectedPoster(state) {
  return state.posters.find((poster) => poster.id === state.selectedPosterId) ?? state.posters[0];
}

export function posterInteractionMode(state, poster = selectedPoster(state)) {
  return state.editMode && poster && !poster.locked ? "editing" : "using";
}

export function isRuntimeSuspended(state, documentHidden = false) {
  const mode = runtimeMode(state, documentHidden);
  return mode === "paused" || mode === "stopped";
}

export function runtimeMode(state, documentHidden = false) {
  const mode = state.runtimeLifecycle?.mode
    ?? requestedRuntimeMode({ ...state, phase: state.hostSurfaceReady ? "running" : "starting" });
  if (documentHidden && mode !== "stopped") return "paused";
  if (state.effectivePaused && mode !== "stopped") return "paused";
  return mode;
}

export function posterSnapshot(state) {
  return {
    selectedPosterId: state.selectedPosterId,
    posters: state.posters
  };
}

export function buildRuntimeSnapshot(state) {
  return {
    contractVersion: state.contractVersion,
    editMode: state.editMode,
    manuallyPaused: state.manuallyPaused,
    automaticallyPaused: state.automaticallyPaused,
    effectivePaused: state.effectivePaused,
    allLocked: state.allLocked,
    posterVisible: state.posterVisible,
    hostSurfaceReady: state.hostSurfaceReady,
    pauseReasons: state.pauseReasons,
    posterFps: state.posterFps,
    runtimeLifecycle: state.runtimeLifecycle,
    runtimeVisibility: state.runtimeVisibility,
    hostSurface: state.hostSurface,
    displayTopology: state.displayTopology,
    hostTopology: state.hostTopology,
    desktopHost: state.desktopHost
  };
}

export function buildRuntimeSyncArgs(state) {
  return {
    editMode: state.editMode,
    manuallyPaused: state.manuallyPaused,
    allLocked: deriveAllLocked(state.posters),
    performanceMode: state.performanceMode
  };
}

export function applyPosterAction(state, action) {
  switch (action.type) {
    case "toggleEditMode":
      return { ...state, editMode: !state.editMode };
    case "setEditMode":
      return { ...state, editMode: Boolean(action.value) };
    case "setPerformanceMode":
      return withLocalLifecycle(state, {
        ...state,
        performanceMode: PERFORMANCE_MODES.includes(action.value)
          ? action.value
          : "auto"
      });
    case "toggleManualPause": {
      const manuallyPaused = !state.manuallyPaused;
      return withLocalLifecycle(state, {
        manuallyPaused,
        effectivePaused: manuallyPaused || state.automaticallyPaused
      });
    }
    case "setManualPaused": {
      const manuallyPaused = Boolean(action.value);
      return withLocalLifecycle(state, {
        manuallyPaused,
        effectivePaused: manuallyPaused || state.automaticallyPaused
      });
    }
    case "applyRuntimeSnapshot": {
      const raw = action.snapshot ?? {};
      const incomingGeneration = raw.runtimeLifecycle?.generation;
      const currentGeneration = state.runtimeLifecycle?.generation;
      const incomingEventCount = raw.runtimeLifecycle?.eventCount;
      const currentEventCount = state.runtimeLifecycle?.eventCount;
      if (Number.isSafeInteger(incomingGeneration)
        && Number.isSafeInteger(currentGeneration)
        && (incomingGeneration < currentGeneration
          || (incomingGeneration === currentGeneration
            && Number.isSafeInteger(incomingEventCount)
            && Number.isSafeInteger(currentEventCount)
            && incomingEventCount < currentEventCount))) {
        return state;
      }
      const runtimeLifecycle = normalizedRuntimeLifecycle(raw, state);
      const visibility = raw.runtimeVisibility ?? state.runtimeVisibility ?? {};
      return {
        ...state,
        editMode: raw.editMode ?? state.editMode,
        manuallyPaused: runtimeLifecycle.manuallyPaused,
        automaticallyPaused: runtimeLifecycle.automaticallyPaused,
        effectivePaused: runtimeLifecycle.effectivePaused,
        allLocked: deriveAllLocked(state.posters),
        posterVisible: runtimeLifecycle.posterVisible,
        hostSurfaceReady: runtimeLifecycle.hostSurfaceReady,
        pauseReasons: runtimeLifecycle.pauseReasons,
        posterFps: Number(raw.posterFps ?? state.posterFps ?? 0),
        performanceMode: PERFORMANCE_MODES.includes(raw.performanceMode)
          ? raw.performanceMode
          : state.performanceMode,
        runtimeLifecycle,
        runtimeVisibility: visibility,
        contractVersion: raw.contractVersion ?? state.contractVersion,
        hostSurface: raw.hostSurface ?? state.hostSurface,
        displayTopology: raw.displayTopology ?? state.displayTopology,
        hostTopology: raw.hostTopology ?? state.hostTopology,
        desktopHost: raw.desktopHost ?? state.desktopHost
      };
    }
    case "selectPoster":
      return state.posters.some((poster) => poster.id === action.posterId)
        ? { ...state, selectedPosterId: action.posterId }
        : state;
    case "patchPoster": {
      const posters = state.posters.map((poster) => poster.id === action.posterId
        ? normalizedPoster({ ...poster, ...action.patch })
        : poster);
      return {
        ...state,
        posters,
        allLocked: deriveAllLocked(posters)
      };
    }
    case "setAllLocked": {
      const posters = state.posters.map((poster) => normalizedPoster({
        ...poster,
        locked: action.locked
      }));
      return {
        ...state,
        posters,
        allLocked: deriveAllLocked(posters)
      };
    }
    case "movePoster": {
      const target = state.posters.find((poster) => poster.id === action.posterId);
      if (!target) return state;
      const moved = moveWithinBounds(target, action.stage, action.next);
      return {
        ...state,
        posters: state.posters.map((poster) => poster.id === action.posterId
          ? normalizedPoster({ ...poster, x: moved.x, y: moved.y })
          : poster)
      };
    }
    case "resizePoster": {
      const target = state.posters.find((poster) => poster.id === action.posterId);
      if (!target) return state;
      const resized = resizeWithinBounds(target, action.next);
      return {
        ...state,
        posters: state.posters.map((poster) => poster.id === action.posterId
          ? normalizedPoster({ ...poster, width: resized.width, height: resized.height })
          : poster)
      };
    }
    default:
      return state;
  }
}

export function buildNativeResizeArgs(poster, options = {}) {
  const source = normalizedPoster(poster);
  return {
    collapsed: options.collapsed ?? source.collapsed,
    width: source.width,
    height: source.height,
    surfaceGutter: options.surfaceGutter ?? 0,
    deviceScaleFactor: options.deviceScaleFactor ?? 1
  };
}

export function buildInteractionRegionArgs(rect, options = {}) {
  return {
    x: Number(rect?.x ?? rect?.left ?? 0),
    y: Number(rect?.y ?? rect?.top ?? 0),
    width: Number(rect?.width ?? 0),
    height: Number(rect?.height ?? 0),
    cornerRadius: options.cornerRadius ?? 0,
    deviceScaleFactor: options.deviceScaleFactor ?? 1
  };
}

export function measuredInteractionRegion(input = {}) {
  const surfaceRect = input.surfaceRect;
  const surfaceSize = input.surfaceSize;
  const posterRect = input.posterRect;
  const visualRect = input.visualRect;
  const elementRect = input.elementRect;
  const values = [
    surfaceRect?.width,
    surfaceRect?.height,
    surfaceSize?.width,
    surfaceSize?.height,
    posterRect?.left,
    posterRect?.top,
    posterRect?.width,
    elementRect?.left,
    elementRect?.top,
    elementRect?.width,
    elementRect?.height,
    visualRect?.x,
    visualRect?.y,
    visualRect?.width,
    visualRect?.height
  ];
  if (values.some((value) => !Number.isFinite(Number(value)))) return null;
  if (elementRect.width <= 0 || elementRect.height <= 0 || posterRect.width <= 0) return null;

  const transformX = surfaceRect.width > 0 ? surfaceSize.width / surfaceRect.width : 1;
  const transformY = surfaceRect.height > 0 ? surfaceSize.height / surfaceRect.height : 1;
  const posterLayoutWidth = posterRect.width * transformX;
  if (posterLayoutWidth <= 0 || visualRect.width <= 0 || visualRect.height <= 0) return null;
  const layoutWidthScale = visualRect.width / posterLayoutWidth;
  const measured = {
    left: visualRect.x
      + (elementRect.left - posterRect.left) * transformX * layoutWidthScale,
    top: visualRect.y + (elementRect.top - posterRect.top) * transformY,
    right: visualRect.x
      + (elementRect.left - posterRect.left + elementRect.width)
        * transformX * layoutWidthScale,
    bottom: visualRect.y
      + (elementRect.top - posterRect.top + elementRect.height) * transformY
  };
  const left = Math.max(visualRect.x, measured.left);
  const top = Math.max(visualRect.y, measured.top);
  const right = Math.min(visualRect.x + visualRect.width, measured.right);
  const bottom = Math.min(visualRect.y + visualRect.height, measured.bottom);
  if (right <= left || bottom <= top) return null;
  const width = right - left;
  const height = bottom - top;

  return {
    regionId: input.regionId,
    rect: { x: left, y: top, width, height },
    cornerRadius: Math.min(Number(input.cornerRadius ?? 0), width / 2, height / 2)
  };
}

export function buildHostSurfaceSyncArgs(poster, rect, options = {}) {
  const source = normalizedPoster(poster);
  const surfaceGutter = Number(options.surfaceGutter ?? 0);
  const revision = options.revision ?? 1;
  const registrySurfaceId = options.registrySurfaceId ?? `poster-${source.id}`;
  const contentHeight = source.collapsed
    ? Number(options.collapsedHeight ?? 116)
    : source.height;
  const interactionRect = rect ? {
    x: Number(rect.x ?? rect.left ?? 0),
    y: Number(rect.y ?? rect.top ?? 0),
    width: source.width,
    height: contentHeight
  } : null;
  const interactionRegions = Array.isArray(options.interactionRegions)
    ? options.interactionRegions.map((region, index) => ({
        regionId: region.regionId ?? `${registrySurfaceId}-input-${index}`,
        rect: {
          x: Number(region.rect?.x ?? region.x ?? 0),
          y: Number(region.rect?.y ?? region.y ?? 0),
          width: Number(region.rect?.width ?? region.width ?? 0),
          height: Number(region.rect?.height ?? region.height ?? 0)
        },
        cornerRadius: Number(region.cornerRadius ?? 0)
      }))
    : interactionRect ? [{
        regionId: options.regionId ?? `${registrySurfaceId}-legacy-input`,
        rect: interactionRect,
        cornerRadius: options.cornerRadius ?? 0
      }] : [];
  const logicalSize = {
    width: source.width + surfaceGutter * 2,
    height: contentHeight + surfaceGutter * 2
  };
  return {
    request: createHostSurfaceRequest({
      revision,
      surfaceId: options.surfaceId ?? "poster-host",
      logicalSize,
      scaleFactor: options.deviceScaleFactor ?? 1,
      userScale: options.userScale ?? 1,
      placementIntent: options.placementIntent ?? { kind: "preserve" },
      surfaceRegistry: {
        registryVersion: SURFACE_REGISTRY_VERSION,
        revision,
        surfaces: [{
          surfaceId: registrySurfaceId,
          visualRegion: {
            regionId: `${registrySurfaceId}-visual`,
            rect: interactionRect,
            cornerRadius: options.cornerRadius ?? 0
          },
          interactionRegions
        }]
      }
    })
  };
}
