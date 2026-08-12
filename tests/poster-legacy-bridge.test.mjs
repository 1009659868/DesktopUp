import test from "node:test";
import assert from "node:assert/strict";
import {
  LEGACY_POSTER_INSPECT_COMMAND,
  LEGACY_POSTER_STORAGE_KEY,
  LEGACY_SHARED_SOURCE_KEY,
  POSTER_LEGACY_BRIDGE_CONTRACT_VERSION,
  classifyLegacyPosterSnapshot,
  createLegacyDefaultSnapshot,
  inspectLegacyPosterSource,
  inspectLocalLegacyPosterSource,
  readLegacyPosterSnapshot
} from "../app/src/poster-legacy-bridge.js";

function memoryStorage(initial = new Map()) {
  const values = new Map(initial);
  let writes = 0;
  return {
    getItem: (key) => values.has(key) ? values.get(key) : null,
    setItem: (key, value) => {
      writes += 1;
      values.set(key, String(value));
    },
    writes: () => writes
  };
}

test("legacy bridge classifies A0, formal and malformed snapshots without overlap", () => {
  assert.equal(POSTER_LEGACY_BRIDGE_CONTRACT_VERSION, 1);
  assert.deepEqual(classifyLegacyPosterSnapshot({ posters: [] }), {
    accepted: true,
    kind: "legacyA0",
    reason: null
  });
  assert.deepEqual(classifyLegacyPosterSnapshot({ contractVersion: 1, posters: [] }), {
    accepted: false,
    kind: "formal",
    reason: "formal_snapshot_requires_poster_contract"
  });
  assert.deepEqual(classifyLegacyPosterSnapshot({}), {
    accepted: false,
    kind: "invalid",
    reason: "invalid_legacy_snapshot"
  });
});

test("legacy storage compatibility read is read-only", () => {
  const legacy = { selectedPosterId: "night-ops", posters: [{ id: "night-ops" }] };
  const storage = memoryStorage(new Map([
    [LEGACY_POSTER_STORAGE_KEY, JSON.stringify(legacy)]
  ]));
  assert.deepEqual(readLegacyPosterSnapshot(storage), legacy);
  assert.equal(storage.writes(), 0);

  const corrupt = memoryStorage(new Map([[LEGACY_POSTER_STORAGE_KEY, "{"]]));
  assert.equal(readLegacyPosterSnapshot(corrupt), null);
  assert.equal(corrupt.writes(), 0);
});

test("local legacy inspection distinguishes absent, corrupt, A0, formal and invalid sources", () => {
  assert.deepEqual(inspectLocalLegacyPosterSource(memoryStorage()), { status: "noData" });

  const corrupt = inspectLocalLegacyPosterSource(memoryStorage(new Map([
    [LEGACY_POSTER_STORAGE_KEY, "{"]
  ])));
  assert.equal(corrupt.status, "rejected");
  assert.equal(corrupt.code, "corruptJson");

  const legacy = { selectedPosterId: "legacy", posters: [{ id: "legacy" }] };
  const loadedLegacy = inspectLocalLegacyPosterSource(memoryStorage(new Map([
    [LEGACY_POSTER_STORAGE_KEY, JSON.stringify(legacy)]
  ])));
  assert.deepEqual(loadedLegacy, {
    status: "loaded",
    kind: "legacyA0",
    source: { kind: "localStorage", key: LEGACY_POSTER_STORAGE_KEY },
    snapshot: legacy
  });

  const formal = { contractVersion: 1, revision: 0, selectedPosterId: "formal", posters: [] };
  const loadedFormal = inspectLocalLegacyPosterSource(memoryStorage(new Map([
    [LEGACY_POSTER_STORAGE_KEY, JSON.stringify(formal)]
  ])));
  assert.equal(loadedFormal.status, "loaded");
  assert.equal(loadedFormal.kind, "formalV1");

  const invalid = inspectLocalLegacyPosterSource(memoryStorage(new Map([
    [LEGACY_POSTER_STORAGE_KEY, JSON.stringify({ nope: true })]
  ])));
  assert.equal(invalid.status, "rejected");
  assert.equal(invalid.code, "invalid_legacy_snapshot");
});

test("shared JSON inspection is preferred and never writes localStorage", async () => {
  const local = { selectedPosterId: "local", posters: [{ id: "local" }] };
  const shared = { selectedPosterId: "shared", posters: [{ id: "shared" }] };
  const storage = memoryStorage(new Map([
    [LEGACY_POSTER_STORAGE_KEY, JSON.stringify(local)]
  ]));
  const result = await inspectLegacyPosterSource(storage, async (command) => {
    assert.equal(command, LEGACY_POSTER_INSPECT_COMMAND);
    return { status: "loaded", kind: "legacyA0", snapshot: shared };
  });

  assert.deepEqual(result, {
    status: "loaded",
    kind: "legacyA0",
    source: { kind: "sharedJson", key: LEGACY_SHARED_SOURCE_KEY },
    snapshot: shared
  });
  assert.equal(storage.writes(), 0);
  assert.deepEqual(readLegacyPosterSnapshot(storage), local);
});

test("rejected shared source fails closed instead of falling back or rewriting", async () => {
  const local = { selectedPosterId: "local", posters: [{ id: "local" }] };
  const storage = memoryStorage(new Map([
    [LEGACY_POSTER_STORAGE_KEY, JSON.stringify(local)]
  ]));
  const result = await inspectLegacyPosterSource(storage, async () => ({
    status: "rejected",
    code: "corruptJson",
    message: "invalid JSON"
  }));
  assert.equal(result.status, "rejected");
  assert.equal(result.code, "corruptJson");
  assert.equal(result.source.kind, "sharedJson");
  assert.equal(storage.writes(), 0);
});

test("absent shared source falls back to read-only local inspection", async () => {
  const local = { selectedPosterId: "local", posters: [{ id: "local" }] };
  const storage = memoryStorage(new Map([
    [LEGACY_POSTER_STORAGE_KEY, JSON.stringify(local)]
  ]));
  const result = await inspectLegacyPosterSource(storage, async () => ({ status: "noData" }));
  assert.equal(result.status, "loaded");
  assert.equal(result.kind, "legacyA0");
  assert.equal(result.source.kind, "localStorage");
  assert.equal(storage.writes(), 0);
});

test("legacy bridge retains the existing two-poster demo only as source/fixture data", () => {
  const snapshot = createLegacyDefaultSnapshot();
  assert.equal(snapshot.selectedPosterId, "night-ops");
  assert.equal(snapshot.posters.length, 2);
  assert.equal(snapshot.posters[0].items.length, 6);
  assert.equal(snapshot.posters[1].items.length, 3);
  assert.equal(Object.hasOwn(snapshot, "contractVersion"), false);
});
