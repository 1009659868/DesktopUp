import { adaptLegacyPosterSnapshot } from "./poster-contract.js";

export const POSTER_LEGACY_BRIDGE_CONTRACT_VERSION = 1;
export const LEGACY_POSTER_STORAGE_KEY = "desktop-up.a0-demo.v1";
export const LEGACY_POSTER_INSPECT_COMMAND = "inspect_poster_snapshot";
export const LEGACY_SHARED_SOURCE_KEY = "poster-snapshot-bridge-v1.json";
export const LEGACY_POSTER_SOURCE_KINDS = Object.freeze([
  "sharedJson",
  "localStorage"
]);

export const LEGACY_DEFAULT_CATALOG = Object.freeze([
  ["AFTERLIGHT", "AL", "violet"],
  ["GHOST SIGNAL", "GS", "cyan"],
  ["IRON VEIL", "IV", "red"],
  ["LAST ORBIT", "LO", "amber"],
  ["SABLE RUN", "SR", "blue"],
  ["HOLLOW CROWN", "HC", "green"],
  ["RED SECTOR", "RS", "red"],
  ["DEEP SIGNAL", "DS", "cyan"],
  ["ASHLINE", "AS", "amber"]
]);

function isObject(value) {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

function cloneJson(value) {
  return JSON.parse(JSON.stringify(value));
}

function source(kind, key) {
  return { kind, key };
}

function rejectedSource(sourceIdentity, code, message) {
  return {
    status: "rejected",
    source: sourceIdentity,
    code,
    message
  };
}

export function classifyLegacyPosterSnapshot(snapshot) {
  if (isObject(snapshot) && Object.hasOwn(snapshot, "contractVersion")) {
    return {
      accepted: false,
      kind: "formal",
      reason: "formal_snapshot_requires_poster_contract"
    };
  }
  if (isObject(snapshot) && Array.isArray(snapshot.posters)) {
    return { accepted: true, kind: "legacyA0", reason: null };
  }
  return { accepted: false, kind: "invalid", reason: "invalid_legacy_snapshot" };
}

export function isLegacyPosterSnapshot(snapshot) {
  return classifyLegacyPosterSnapshot(snapshot).accepted;
}

export function readLegacyPosterSnapshot(storage) {
  try {
    const raw = storage.getItem(LEGACY_POSTER_STORAGE_KEY);
    if (raw == null) return null;
    const snapshot = JSON.parse(raw);
    return isLegacyPosterSnapshot(snapshot) ? snapshot : null;
  } catch {
    return null;
  }
}

export function inspectLocalLegacyPosterSource(storage) {
  const sourceIdentity = source("localStorage", LEGACY_POSTER_STORAGE_KEY);
  let raw;
  try {
    raw = storage?.getItem?.(LEGACY_POSTER_STORAGE_KEY);
  } catch (error) {
    return rejectedSource(sourceIdentity, "readFailed", String(error));
  }
  if (raw == null) return { status: "noData" };

  let snapshot;
  try {
    snapshot = JSON.parse(raw);
  } catch (error) {
    return rejectedSource(sourceIdentity, "corruptJson", String(error));
  }
  const classification = classifyLegacyPosterSnapshot(snapshot);
  if (classification.accepted) {
    return { status: "loaded", kind: "legacyA0", source: sourceIdentity, snapshot };
  }
  if (classification.kind === "formal") {
    return { status: "loaded", kind: "formalV1", source: sourceIdentity, snapshot };
  }
  return rejectedSource(sourceIdentity, classification.reason, "Unsupported local legacy source.");
}

export async function inspectLegacyPosterSource(storage, invoke) {
  if (typeof invoke !== "function") return inspectLocalLegacyPosterSource(storage);
  const sourceIdentity = source("sharedJson", LEGACY_SHARED_SOURCE_KEY);
  let shared;
  try {
    shared = await invoke(LEGACY_POSTER_INSPECT_COMMAND);
  } catch (error) {
    return rejectedSource(sourceIdentity, "inspectCommandFailed", String(error));
  }

  if (shared?.status === "rejected") {
    return rejectedSource(sourceIdentity, shared.code ?? "sharedSourceRejected", shared.message ?? "");
  }
  if (shared?.status === "loaded") {
    const classification = classifyLegacyPosterSnapshot(shared.snapshot);
    const kindMatches = (shared.kind === "legacyA0" && classification.accepted)
      || (shared.kind === "formalV1" && classification.kind === "formal");
    if (!kindMatches) {
      return rejectedSource(
        sourceIdentity,
        "sharedSourceClassificationMismatch",
        "Native and frontend legacy source classifications disagree."
      );
    }
    return {
      status: "loaded",
      kind: shared.kind,
      source: sourceIdentity,
      snapshot: cloneJson(shared.snapshot)
    };
  }
  if (shared?.status !== "noData") {
    return rejectedSource(
      sourceIdentity,
      "invalidInspectResult",
      "Native legacy inspection returned an unsupported result."
    );
  }
  return inspectLocalLegacyPosterSource(storage);
}

export function prepareLegacyPosterMigration(legacySnapshot, {
  workArea,
  sourceKind,
  sourceKey
} = {}) {
  const classification = classifyLegacyPosterSnapshot(legacySnapshot);
  const sourceIsValid = LEGACY_POSTER_SOURCE_KINDS.includes(sourceKind)
    && typeof sourceKey === "string"
    && Boolean(sourceKey.trim());
  if (!classification.accepted || !sourceIsValid) {
    return {
      ok: false,
      request: null,
      errors: [{
        path: classification.accepted ? "$.source" : "$",
        code: classification.accepted ? "invalid_legacy_source" : classification.reason,
        message: classification.accepted
          ? "Legacy migration requires a supported source kind and non-empty source key."
          : "Legacy migration requires an unversioned A0 snapshot with a posters array."
      }]
    };
  }

  const legacyEnvelope = cloneJson(legacySnapshot);
  const firstPoster = legacyEnvelope.posters[0];
  const projectedSnapshot = {
    selectedPosterId: typeof firstPoster?.id === "string" ? firstPoster.id : "",
    posters: firstPoster === undefined ? [] : [cloneJson(firstPoster)]
  };
  const adapted = adaptLegacyPosterSnapshot(projectedSnapshot, { workArea });
  const request = {
    source: { kind: sourceKind, key: sourceKey },
    legacySnapshot: legacyEnvelope,
    adaptedSnapshot: adapted.snapshot,
    adapterMigrations: cloneJson(adapted.migrations),
    adapterErrors: cloneJson(adapted.errors)
  };

  return {
    ok: adapted.ok,
    request,
    errors: cloneJson(adapted.errors)
  };
}

export function toLegacyDemoItem([title, mark, hue], index) {
  return { id: `${title.toLowerCase().replace(/\W+/g, "-")}-${index}`, title, mark, hue };
}

export function createLegacyDefaultSnapshot() {
  return {
    selectedPosterId: "night-ops",
    posters: [
      {
        id: "night-ops",
        title: "NIGHT OPS",
        subtitle: "TACTICAL ARCHIVE / 01",
        theme: "nocturne",
        accent: "#72e4ff",
        opacity: 94,
        density: 1,
        x: 72,
        y: 154,
        width: 490,
        height: 392,
        collapsed: false,
        locked: false,
        autoCollapse: false,
        items: LEGACY_DEFAULT_CATALOG.slice(0, 6).map(toLegacyDemoItem)
      },
      {
        id: "indie-arcade",
        title: "INDIE ARCADE",
        subtitle: "SMALL WORLDS / BIG IDEAS",
        theme: "ember",
        accent: "#ff735c",
        opacity: 92,
        density: 0,
        x: 610,
        y: 226,
        width: 376,
        height: 314,
        collapsed: true,
        locked: false,
        autoCollapse: false,
        items: LEGACY_DEFAULT_CATALOG.slice(6).map(toLegacyDemoItem)
      }
    ]
  };
}
