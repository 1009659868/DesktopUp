import { normalizedPoster } from "./model.js";

export const POSTER_LEGACY_BRIDGE_CONTRACT_VERSION = 1;
export const LEGACY_POSTER_STORAGE_KEY = "desktop-up.a0-demo.v1";
export const LEGACY_POSTER_LOAD_COMMAND = "load_poster_snapshot";
export const LEGACY_POSTER_SAVE_COMMAND = "save_poster_snapshot";

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
    const snapshot = JSON.parse(storage.getItem(LEGACY_POSTER_STORAGE_KEY));
    return isLegacyPosterSnapshot(snapshot) ? snapshot : null;
  } catch {
    return null;
  }
}

export function writeLegacyPosterSnapshot(storage, snapshot) {
  if (!isLegacyPosterSnapshot(snapshot)) return false;
  storage.setItem(LEGACY_POSTER_STORAGE_KEY, JSON.stringify(snapshot));
  return true;
}

export async function readSharedLegacyPosterSnapshot(storage, invoke) {
  const localSnapshot = readLegacyPosterSnapshot(storage);
  if (typeof invoke !== "function") return localSnapshot;

  try {
    const sharedSnapshot = await invoke(LEGACY_POSTER_LOAD_COMMAND);
    const sharedClassification = classifyLegacyPosterSnapshot(sharedSnapshot);
    if (sharedClassification.accepted) {
      writeLegacyPosterSnapshot(storage, sharedSnapshot);
      return sharedSnapshot;
    }
    if (sharedClassification.kind === "formal") {
      return localSnapshot;
    }
    if (isLegacyPosterSnapshot(localSnapshot)) {
      await invoke(LEGACY_POSTER_SAVE_COMMAND, { snapshot: localSnapshot });
    }
  } catch {
    return localSnapshot;
  }
  return localSnapshot;
}

export async function writeSharedLegacyPosterSnapshot(storage, invoke, snapshot) {
  if (!writeLegacyPosterSnapshot(storage, snapshot)) return false;
  if (typeof invoke !== "function") return true;
  try {
    const sharedSnapshot = await invoke(LEGACY_POSTER_LOAD_COMMAND);
    if (classifyLegacyPosterSnapshot(sharedSnapshot).kind === "formal") {
      return false;
    }
    await invoke(LEGACY_POSTER_SAVE_COMMAND, { snapshot });
    return true;
  } catch {
    return false;
  }
}

export function readFirstLegacyPoster(snapshot, fallback) {
  return normalizedPoster(snapshot?.posters?.[0] ?? fallback);
}

export function replaceFirstLegacyPoster(snapshot, next) {
  return {
    ...snapshot,
    posters: snapshot.posters.length
      ? snapshot.posters.map((entry, index) => index === 0 ? next : entry)
      : [next]
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
