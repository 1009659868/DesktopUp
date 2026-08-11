import { resolveDefaultPosterGeometry } from "./poster-geometry.js";
import {
  DEFAULT_TEMPLATE_KEY,
  INTERNAL_FALLBACK_TEMPLATE_KEY,
  resolvePosterTemplateSelection
} from "./poster-template-resolution.js";

export { DEFAULT_TEMPLATE_KEY, INTERNAL_FALLBACK_TEMPLATE_KEY };

export const POSTER_CONTRACT_VERSION = 1;
export const DEFAULT_POSTER_ID = "poster-primary";

export const POSTER_ANCHORS = Object.freeze([
  "top-left",
  "top-right",
  "bottom-left",
  "bottom-right",
  "free"
]);
export const POSTER_PRESENTATION_MODES = Object.freeze(["expanded", "collapsed"]);
export const POSTER_LAYOUT_KINDS = Object.freeze(["grid", "free"]);

export const POSTER_SNAPSHOT_FIELDS = Object.freeze([
  "contractVersion",
  "revision",
  "selectedPosterId",
  "posters"
]);
export const POSTER_PERSISTENT_FIELDS = Object.freeze([
  "id",
  "title",
  "subtitle",
  "monitorIdentity",
  "anchor",
  "normalizedRect",
  "width",
  "height",
  "collapsedState",
  "autoCollapseEnabled",
  "autoCollapseDelaySeconds",
  "isLocked",
  "layoutKind",
  "layoutSpec",
  "templateKey"
]);
export const POSTER_RUNTIME_ONLY_FIELDS = Object.freeze([
  "interaction",
  "interactionMode",
  "runtime",
  "runtimeMode"
]);

function isPlainObject(value) {
  if (!value || typeof value !== "object" || Array.isArray(value)) return false;
  const prototype = Object.getPrototypeOf(value);
  return prototype === Object.prototype || prototype === null;
}

function cloneJson(value) {
  return JSON.parse(JSON.stringify(value));
}

function isJsonValue(value, ancestors = new Set()) {
  if (value === null || typeof value === "string" || typeof value === "boolean") return true;
  if (typeof value === "number") return Number.isFinite(value);
  if (typeof value !== "object" || ancestors.has(value)) return false;

  ancestors.add(value);
  const children = Array.isArray(value) ? value : Object.values(value);
  const valid = children.every((child) => isJsonValue(child, ancestors));
  ancestors.delete(value);
  return valid;
}

function addError(errors, path, code, message) {
  errors.push({ path, code, message });
}

function rejectUnknownFields(value, allowedFields, path, errors) {
  if (!isPlainObject(value)) return;
  const allowed = new Set(allowedFields);
  for (const field of Object.keys(value)) {
    if (!allowed.has(field)) {
      const code = POSTER_RUNTIME_ONLY_FIELDS.includes(field)
        ? "runtime_state_is_not_persistent"
        : "unknown_field";
      addError(errors, `${path}.${field}`, code, `Field '${field}' is not part of poster-contract-v1.`);
    }
  }
}

function validateNormalizedRect(value, path, errors) {
  if (!isPlainObject(value)) {
    addError(errors, path, "invalid_normalized_rect", "normalizedRect must be an object.");
    return;
  }
  rejectUnknownFields(value, ["x", "y"], path, errors);
  for (const axis of ["x", "y"]) {
    const coordinate = value[axis];
    if (!Number.isFinite(coordinate) || coordinate < 0 || coordinate > 1) {
      addError(
        errors,
        `${path}.${axis}`,
        "invalid_normalized_coordinate",
        `${axis} must be a finite number in the inclusive range 0..1.`
      );
    }
  }
}

function validatePoster(poster, index, errors) {
  const path = `$.posters[${index}]`;
  if (!isPlainObject(poster)) {
    addError(errors, path, "invalid_poster", "Poster must be an object.");
    return;
  }

  rejectUnknownFields(poster, POSTER_PERSISTENT_FIELDS, path, errors);

  if (typeof poster.id !== "string" || !poster.id.trim()) {
    addError(errors, `${path}.id`, "invalid_poster_id", "Poster id must be a non-empty string.");
  }
  if (typeof poster.title !== "string" || !poster.title.trim()) {
    addError(errors, `${path}.title`, "invalid_title", "Poster title must be a non-empty string.");
  }
  if (typeof poster.subtitle !== "string") {
    addError(errors, `${path}.subtitle`, "invalid_subtitle", "Poster subtitle must be a string.");
  }
  if (typeof poster.monitorIdentity !== "string" || !poster.monitorIdentity.trim()) {
    addError(
      errors,
      `${path}.monitorIdentity`,
      "invalid_monitor_identity",
      "monitorIdentity must be a non-empty string."
    );
  }
  if (!POSTER_ANCHORS.includes(poster.anchor)) {
    addError(errors, `${path}.anchor`, "invalid_anchor", "Poster anchor is not supported.");
  }
  validateNormalizedRect(poster.normalizedRect, `${path}.normalizedRect`, errors);

  for (const dimension of ["width", "height"]) {
    const value = poster[dimension];
    if (!Number.isSafeInteger(value) || value <= 0) {
      addError(
        errors,
        `${path}.${dimension}`,
        "invalid_dimension",
        `${dimension} must be a positive safe integer. Work-area limits are applied by C-PCB-02.`
      );
    }
  }

  if (!POSTER_PRESENTATION_MODES.includes(poster.collapsedState)) {
    addError(
      errors,
      `${path}.collapsedState`,
      "invalid_presentation_mode",
      "collapsedState must be 'expanded' or 'collapsed'."
    );
  }
  if (typeof poster.autoCollapseEnabled !== "boolean") {
    addError(
      errors,
      `${path}.autoCollapseEnabled`,
      "invalid_auto_collapse_enabled",
      "autoCollapseEnabled must be boolean."
    );
  }
  if (!Number.isSafeInteger(poster.autoCollapseDelaySeconds)
      || poster.autoCollapseDelaySeconds < 1
      || poster.autoCollapseDelaySeconds > 300) {
    addError(
      errors,
      `${path}.autoCollapseDelaySeconds`,
      "invalid_auto_collapse_delay",
      "autoCollapseDelaySeconds must be an integer in the inclusive range 1..300."
    );
  }
  if (typeof poster.isLocked !== "boolean") {
    addError(errors, `${path}.isLocked`, "invalid_lock_state", "isLocked must be boolean.");
  }
  if (!POSTER_LAYOUT_KINDS.includes(poster.layoutKind)) {
    addError(errors, `${path}.layoutKind`, "invalid_layout_kind", "Poster layoutKind is not supported.");
  }
  if (!isPlainObject(poster.layoutSpec) || !isJsonValue(poster.layoutSpec)) {
    addError(
      errors,
      `${path}.layoutSpec`,
      "invalid_layout_spec",
      "layoutSpec must be a finite, acyclic JSON object."
    );
  }
  if (typeof poster.templateKey !== "string" || !poster.templateKey.trim()) {
    addError(errors, `${path}.templateKey`, "invalid_template_key", "templateKey must be a non-empty string.");
  } else if (poster.templateKey === INTERNAL_FALLBACK_TEMPLATE_KEY) {
    addError(
      errors,
      `${path}.templateKey`,
      "fallback_template_is_not_persistent",
      "minimal-fallback is an internal render fallback and cannot be persisted."
    );
  }
}

export function validatePosterSnapshot(snapshot) {
  const errors = [];
  if (!isPlainObject(snapshot)) {
    return {
      ok: false,
      value: null,
      errors: [{ path: "$", code: "invalid_snapshot", message: "Snapshot must be an object." }]
    };
  }

  rejectUnknownFields(snapshot, POSTER_SNAPSHOT_FIELDS, "$", errors);
  if (snapshot.contractVersion !== POSTER_CONTRACT_VERSION) {
    addError(
      errors,
      "$.contractVersion",
      "unsupported_contract_version",
      `Expected poster contract version ${POSTER_CONTRACT_VERSION}.`
    );
  }
  if (!Number.isSafeInteger(snapshot.revision) || snapshot.revision < 0) {
    addError(errors, "$.revision", "invalid_revision", "revision must be a non-negative safe integer.");
  }
  if (!Array.isArray(snapshot.posters) || snapshot.posters.length === 0) {
    addError(errors, "$.posters", "empty_posters", "Snapshot must contain at least one poster.");
  } else {
    snapshot.posters.forEach((poster, index) => validatePoster(poster, index, errors));
    const seen = new Set();
    snapshot.posters.forEach((poster, index) => {
      if (typeof poster?.id !== "string" || !poster.id.trim()) return;
      if (seen.has(poster.id)) {
        addError(errors, `$.posters[${index}].id`, "duplicate_poster_id", "Poster ids must be unique.");
      }
      seen.add(poster.id);
    });
    if (typeof snapshot.selectedPosterId !== "string" || !seen.has(snapshot.selectedPosterId)) {
      addError(
        errors,
        "$.selectedPosterId",
        "selected_poster_not_found",
        "selectedPosterId must reference an existing poster."
      );
    }
  }

  return {
    ok: errors.length === 0,
    value: errors.length === 0 ? cloneJson(snapshot) : null,
    errors
  };
}

export function createDefaultPosterSnapshot({
  id = DEFAULT_POSTER_ID,
  title,
  subtitle = "",
  primaryWorkArea
} = {}) {
  if (typeof title !== "string" || !title.trim()) {
    throw new TypeError("createDefaultPosterSnapshot requires a non-empty title.");
  }
  const geometry = resolveDefaultPosterGeometry(primaryWorkArea);
  if (!geometry.ok) {
    throw new TypeError(`Invalid default poster work area: ${geometry.errors.map((error) => error.code).join(", ")}`);
  }

  const snapshot = {
    contractVersion: POSTER_CONTRACT_VERSION,
    revision: 0,
    selectedPosterId: id,
    posters: [{
      id,
      title,
      subtitle,
      monitorIdentity: "primary",
      anchor: "top-right",
      normalizedRect: cloneJson(geometry.value.normalizedRect),
      width: geometry.value.posterRect.width,
      height: geometry.value.posterRect.height,
      collapsedState: "expanded",
      autoCollapseEnabled: false,
      autoCollapseDelaySeconds: 3,
      isLocked: false,
      layoutKind: "grid",
      layoutSpec: {},
      templateKey: DEFAULT_TEMPLATE_KEY
    }]
  };

  const result = validatePosterSnapshot(snapshot);
  if (!result.ok) {
    throw new TypeError(`Invalid default poster input: ${result.errors.map((error) => error.code).join(", ")}`);
  }
  return result.value;
}

function normalizedLegacyPosition(poster, workArea) {
  const width = Number(poster.width);
  const height = Number(poster.height);
  const availableWidth = Math.max(1, workArea.width - width);
  const availableHeight = Math.max(1, workArea.height - height);
  const legacyX = Number(poster.x);
  const legacyY = Number(poster.y);
  const x = Number.isFinite(legacyX) ? Math.min(1, Math.max(0, legacyX / availableWidth)) : 0;
  const y = Number.isFinite(legacyY) ? Math.min(1, Math.max(0, legacyY / availableHeight)) : 0;
  return { x, y };
}

export function adaptLegacyPosterSnapshot(legacySnapshot, { workArea } = {}) {
  if (!isPlainObject(legacySnapshot) || !Array.isArray(legacySnapshot.posters)) {
    return {
      ok: false,
      snapshot: null,
      migrations: [],
      errors: [{ path: "$", code: "invalid_legacy_snapshot", message: "Legacy snapshot must contain posters." }]
    };
  }
  if (!isPlainObject(workArea)
      || !Number.isFinite(workArea.width)
      || !Number.isFinite(workArea.height)
      || workArea.width <= 0
      || workArea.height <= 0) {
    return {
      ok: false,
      snapshot: null,
      migrations: [],
      errors: [{
        path: "$.workArea",
        code: "legacy_work_area_required",
        message: "A positive work area is required to normalize legacy x/y coordinates."
      }]
    };
  }

  const migrations = [];
  const posters = legacySnapshot.posters.map((poster, index) => {
    const path = `$.posters[${index}]`;
    const id = typeof poster?.id === "string" && poster.id.trim() ? poster.id : `legacy-poster-${index + 1}`;
    migrations.push(
      { path: `${path}.collapsed`, target: `${path}.collapsedState` },
      { path: `${path}.locked`, target: `${path}.isLocked` },
      { path: `${path}.theme`, target: `${path}.templateKey`, note: "legacy visual theme is not authoritative" },
      { path: `${path}.x/y`, target: `${path}.normalizedRect` }
    );
    return {
      id,
      title: typeof poster?.title === "string" && poster.title.trim() ? poster.title : `Legacy Poster ${index + 1}`,
      subtitle: typeof poster?.subtitle === "string" ? poster.subtitle : "",
      monitorIdentity: "primary",
      anchor: "free",
      normalizedRect: normalizedLegacyPosition(poster ?? {}, workArea),
      width: Number(poster?.width),
      height: Number(poster?.height),
      collapsedState: poster?.collapsed ? "collapsed" : "expanded",
      autoCollapseEnabled: Boolean(poster?.autoCollapse),
      autoCollapseDelaySeconds: 3,
      isLocked: Boolean(poster?.locked),
      layoutKind: "grid",
      layoutSpec: Number.isFinite(Number(poster?.density)) ? { legacyDensity: Number(poster.density) } : {},
      templateKey: DEFAULT_TEMPLATE_KEY
    };
  });

  const selectedPosterId = posters.some((poster) => poster.id === legacySnapshot.selectedPosterId)
    ? legacySnapshot.selectedPosterId
    : posters[0]?.id ?? "";
  const snapshot = {
    contractVersion: POSTER_CONTRACT_VERSION,
    revision: 0,
    selectedPosterId,
    posters
  };
  const validation = validatePosterSnapshot(snapshot);
  return {
    ok: validation.ok,
    snapshot: validation.value,
    migrations,
    errors: validation.errors
  };
}

export function resolvePosterTemplate(poster, availableTemplateKeys) {
  const keys = new Set(Array.isArray(availableTemplateKeys) ? availableTemplateKeys : []);
  const requestedTemplateKey = typeof poster?.templateKey === "string" ? poster.templateKey : null;
  const resolution = resolvePosterTemplateSelection({
    snapshotStatus: requestedTemplateKey?.trim() ? "valid" : "invalid",
    requestedTemplateKey,
    requestedTemplateStatus: requestedTemplateKey && keys.has(requestedTemplateKey)
      ? "available"
      : "unknown",
    fallbackTemplateStatus: "available"
  });
  const value = resolution.value;

  return {
    templateKey: value.renderTemplateKey,
    requestedTemplateKey,
    isFallback: value.selection === "minimalFallback",
    reason: value.reason === null
      ? null
      : value.reason === "fallback_cannot_be_selected"
        ? value.reason
        : "requested_template_unavailable"
  };
}
