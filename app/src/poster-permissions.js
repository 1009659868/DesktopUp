export const POSTER_PERMISSION_CONTRACT_VERSION = 1;

export const POSTER_PRESENTATION_MODES = Object.freeze(["expanded", "collapsed"]);
export const POSTER_INTERACTION_MODES = Object.freeze(["using", "editing"]);
export const POSTER_RUNTIME_MODES = Object.freeze([
  "activeStatic",
  "activeAnimated",
  "paused",
  "stopped"
]);

export const POSTER_TITLEBAR_CONTRACT = Object.freeze({
  requiredSlots: Object.freeze([
    "title",
    "templateIdentity",
    "itemCount",
    "presentationToggle",
    "lockToggle"
  ]),
  dragOrigins: Object.freeze(["background", "title", "templateIdentity", "itemCount"]),
  dragExclusions: Object.freeze([
    "presentationToggle",
    "lockToggle",
    "button",
    "link",
    "input",
    "select",
    "textarea",
    "item"
  ]),
  pointerDragThresholdPx: 5
});

function permissionError(path, code, message) {
  return { path, code, message };
}

function isActiveRuntime(runtimeMode) {
  return runtimeMode === "activeStatic" || runtimeMode === "activeAnimated";
}

export function resolvePosterPermissions(input) {
  const errors = [];
  const presentationMode = input?.presentationMode;
  const interactionMode = input?.interactionMode;
  const runtimeMode = input?.runtimeMode;
  const isLocked = input?.isLocked;

  if (!POSTER_PRESENTATION_MODES.includes(presentationMode)) {
    errors.push(permissionError(
      "$.presentationMode",
      "invalid_presentation_mode",
      "presentationMode must be expanded or collapsed."
    ));
  }
  if (!POSTER_INTERACTION_MODES.includes(interactionMode)) {
    errors.push(permissionError(
      "$.interactionMode",
      "invalid_interaction_mode",
      "interactionMode must be using or editing."
    ));
  }
  if (!POSTER_RUNTIME_MODES.includes(runtimeMode)) {
    errors.push(permissionError(
      "$.runtimeMode",
      "invalid_runtime_mode",
      "runtimeMode must be activeStatic, activeAnimated, paused, or stopped."
    ));
  }
  if (typeof isLocked !== "boolean") {
    errors.push(permissionError(
      "$.isLocked",
      "invalid_lock_state",
      "isLocked must be boolean."
    ));
  }
  if (errors.length > 0) return { ok: false, value: null, errors };

  const runtimeInteractive = isActiveRuntime(runtimeMode);
  const effectiveInteractionMode = runtimeInteractive && !(isLocked && interactionMode === "editing")
    ? interactionMode
    : "using";
  const expanded = presentationMode === "expanded";
  const editing = effectiveInteractionMode === "editing";

  return {
    ok: true,
    errors: [],
    value: {
      contractVersion: POSTER_PERMISSION_CONTRACT_VERSION,
      presentationMode,
      requestedInteractionMode: interactionMode,
      effectiveInteractionMode,
      runtimeMode,
      isLocked,
      runtimeInteractive,
      permissions: {
        dragFromTitlebar: runtimeInteractive && !isLocked,
        dragFromContent: runtimeInteractive && expanded && editing,
        togglePresentation: runtimeInteractive,
        toggleLock: runtimeInteractive,
        expandFromTitleText: runtimeInteractive && !expanded,
        singleActivateItem: runtimeInteractive && expanded && !editing,
        explicitActivateItem: runtimeInteractive && expanded,
        selectItem: runtimeInteractive && expanded && editing,
        reorderItem: runtimeInteractive && expanded && editing,
        resizePoster: runtimeInteractive && expanded && editing,
        openPosterMenu: runtimeInteractive,
        openItemMenu: runtimeInteractive && expanded,
        autoCollapse: runtimeInteractive && !editing
      }
    }
  };
}
