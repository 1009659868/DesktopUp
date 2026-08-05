import { moveWithinBounds, normalizedPoster, resizeWithinBounds } from "./model.js";

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
    selectedPosterId: snapshot?.selectedPosterId ?? posters[0]?.id ?? null,
    posters,
    editMode: Boolean(snapshot?.editMode ?? false),
    manuallyPaused,
    automaticallyPaused,
    effectivePaused: Boolean(
      snapshot?.effectivePaused ?? (manuallyPaused || automaticallyPaused)
    ),
    allLocked: Boolean(snapshot?.allLocked ?? posters.every((poster) => poster.locked)),
    posterVisible: snapshot?.posterVisible ?? true,
    pauseReasons: Array.isArray(pauseReasons) ? pauseReasons : [],
    runtimeVisibility: visibility
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
    editMode: state.editMode,
    manuallyPaused: state.manuallyPaused,
    automaticallyPaused: state.automaticallyPaused,
    effectivePaused: state.effectivePaused,
    allLocked: state.allLocked,
    posterVisible: state.posterVisible,
    pauseReasons: state.pauseReasons,
    runtimeVisibility: state.runtimeVisibility
  };
}

export function buildRuntimeSyncArgs(state) {
  return {
    editMode: state.editMode,
    manuallyPaused: state.manuallyPaused,
    allLocked: state.allLocked
  };
}

export function applyPosterAction(state, action) {
  switch (action.type) {
    case "toggleEditMode":
      return { ...state, editMode: !state.editMode };
    case "setEditMode":
      return { ...state, editMode: Boolean(action.value) };
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
        allLocked: raw.allLocked ?? state.allLocked,
        posterVisible: raw.posterVisible ?? state.posterVisible,
        pauseReasons: Array.isArray(pauseReasons) ? pauseReasons : [],
        runtimeVisibility: visibility
      };
    }
    case "selectPoster":
      return state.posters.some((poster) => poster.id === action.posterId)
        ? { ...state, selectedPosterId: action.posterId }
        : state;
    case "patchPoster":
      return {
        ...state,
        posters: state.posters.map((poster) => poster.id === action.posterId
          ? normalizedPoster({ ...poster, ...action.patch })
          : poster)
      };
    case "setAllLocked":
      return {
        ...state,
        posters: state.posters.map((poster) => normalizedPoster({ ...poster, locked: action.locked }))
      };
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
