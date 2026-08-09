import { moveWithinBounds, normalizedPoster, resizeWithinBounds } from "./model.js";
import { HOST_CONTRACT_VERSION, createHostSurfaceRequest } from "./host-contract.js";

export const PERFORMANCE_MODES = Object.freeze([
  "auto",
  "activeStatic",
  "activeAnimated",
  "paused",
  "stopped"
]);

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
  const pauseReasons = snapshot?.pauseReasons ?? visibility.pauseReasons ?? [];
  const automaticallyPaused = Boolean(
    snapshot?.automaticallyPaused ?? visibility.automaticallyPaused ?? false
  );
  const manuallyPaused = Boolean(snapshot?.manuallyPaused ?? false);

  return {
    contractVersion: snapshot?.contractVersion ?? HOST_CONTRACT_VERSION,
    selectedPosterId: snapshot?.selectedPosterId ?? posters[0]?.id ?? null,
    posters,
    editMode: Boolean(snapshot?.editMode ?? false),
    manuallyPaused,
    automaticallyPaused,
    effectivePaused: Boolean(
      snapshot?.effectivePaused ?? (manuallyPaused || automaticallyPaused)
    ),
    allLocked: deriveAllLocked(posters),
    posterVisible: snapshot?.posterVisible ?? true,
    hostSurfaceReady: Boolean(snapshot?.hostSurfaceReady ?? false),
    pauseReasons: Array.isArray(pauseReasons) ? pauseReasons : [],
    performanceMode: PERFORMANCE_MODES.includes(snapshot?.performanceMode)
      ? snapshot.performanceMode
      : "auto",
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
  return Boolean(state.effectivePaused) || Boolean(documentHidden);
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
      return {
        ...state,
        performanceMode: PERFORMANCE_MODES.includes(action.value)
          ? action.value
          : "auto"
      };
    case "toggleManualPause": {
      const manuallyPaused = !state.manuallyPaused;
      return {
        ...state,
        manuallyPaused,
        effectivePaused: manuallyPaused || state.automaticallyPaused
      };
    }
    case "setManualPaused": {
      const manuallyPaused = Boolean(action.value);
      return {
        ...state,
        manuallyPaused,
        effectivePaused: manuallyPaused || state.automaticallyPaused
      };
    }
    case "applyRuntimeSnapshot": {
      const raw = action.snapshot ?? {};
      const visibility = raw.runtimeVisibility ?? state.runtimeVisibility ?? {};
      const automaticallyPaused = Boolean(
        raw.automaticallyPaused ?? visibility.automaticallyPaused ?? state.automaticallyPaused
      );
      const manuallyPaused = Boolean(raw.manuallyPaused ?? state.manuallyPaused);
      const pauseReasons = raw.pauseReasons ?? visibility.pauseReasons ?? state.pauseReasons;
      return {
        ...state,
        editMode: raw.editMode ?? state.editMode,
        manuallyPaused,
        automaticallyPaused,
        effectivePaused: Boolean(
          raw.effectivePaused ?? (manuallyPaused || automaticallyPaused)
        ),
        allLocked: deriveAllLocked(state.posters),
        posterVisible: raw.posterVisible ?? state.posterVisible,
        hostSurfaceReady: Boolean(raw.hostSurfaceReady ?? state.hostSurfaceReady),
        pauseReasons: Array.isArray(pauseReasons) ? pauseReasons : [],
        performanceMode: PERFORMANCE_MODES.includes(raw.performanceMode)
          ? raw.performanceMode
          : state.performanceMode,
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

export function buildHostSurfaceSyncArgs(poster, rect, options = {}) {
  const source = normalizedPoster(poster);
  const surfaceGutter = Number(options.surfaceGutter ?? 0);
  const contentHeight = source.collapsed
    ? Number(options.collapsedHeight ?? 116)
    : source.height;
  const interactionRect = rect ? {
    x: Number(rect.x ?? rect.left ?? 0),
    y: Number(rect.y ?? rect.top ?? 0),
    width: source.width,
    height: contentHeight
  } : null;
  const logicalSize = {
    width: source.width + surfaceGutter * 2,
    height: contentHeight + surfaceGutter * 2
  };
  return {
    request: createHostSurfaceRequest({
      revision: options.revision ?? 1,
      surfaceId: options.surfaceId ?? "poster-host",
      logicalSize,
      scaleFactor: options.deviceScaleFactor ?? 1,
      userScale: options.userScale ?? 1,
      placementIntent: options.placementIntent ?? { kind: "preserve" },
      interactionRegion: interactionRect ? {
        regionId: options.regionId ?? `poster-${source.id}`,
        rect: interactionRect,
        cornerRadius: options.cornerRadius ?? 0
      } : null
    })
  };
}
