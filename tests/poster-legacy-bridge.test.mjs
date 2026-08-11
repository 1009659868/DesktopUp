import test from "node:test";
import assert from "node:assert/strict";
import {
  LEGACY_POSTER_LOAD_COMMAND,
  LEGACY_POSTER_SAVE_COMMAND,
  LEGACY_POSTER_STORAGE_KEY,
  POSTER_LEGACY_BRIDGE_CONTRACT_VERSION,
  classifyLegacyPosterSnapshot,
  createLegacyDefaultSnapshot,
  readFirstLegacyPoster,
  readLegacyPosterSnapshot,
  readSharedLegacyPosterSnapshot,
  replaceFirstLegacyPoster,
  writeLegacyPosterSnapshot,
  writeSharedLegacyPosterSnapshot
} from "../app/src/poster-legacy-bridge.js";

function memoryStorage() {
  const values = new Map();
  return {
    getItem: (key) => values.has(key) ? values.get(key) : null,
    setItem: (key, value) => values.set(key, String(value)),
    removeItem: (key) => values.delete(key)
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

test("legacy storage accepts only unversioned A0 snapshot shape", () => {
  const storage = memoryStorage();
  assert.equal(readLegacyPosterSnapshot(storage), null);

  storage.setItem(LEGACY_POSTER_STORAGE_KEY, "{");
  assert.equal(readLegacyPosterSnapshot(storage), null);

  const legacy = { selectedPosterId: "night-ops", posters: [{ id: "night-ops" }] };
  assert.equal(writeLegacyPosterSnapshot(storage, legacy), true);
  assert.deepEqual(readLegacyPosterSnapshot(storage), legacy);

  const formal = { contractVersion: 1, revision: 0, selectedPosterId: "formal", posters: [] };
  assert.equal(writeLegacyPosterSnapshot(storage, formal), false);
  assert.deepEqual(readLegacyPosterSnapshot(storage), legacy);
});

test("shared legacy bridge prefers Rust A0 state and migrates local fallback", async () => {
  const storage = memoryStorage();
  const local = { selectedPosterId: "local", posters: [{ id: "local" }] };
  const shared = { selectedPosterId: "shared", posters: [{ id: "shared" }] };
  writeLegacyPosterSnapshot(storage, local);

  const preferred = await readSharedLegacyPosterSnapshot(storage, async (command) => {
    assert.equal(command, LEGACY_POSTER_LOAD_COMMAND);
    return shared;
  });
  assert.deepEqual(preferred, shared);
  assert.deepEqual(readLegacyPosterSnapshot(storage), shared);

  let migrated = null;
  const fallbackStorage = memoryStorage();
  writeLegacyPosterSnapshot(fallbackStorage, local);
  const migratedSnapshot = await readSharedLegacyPosterSnapshot(fallbackStorage, async (command, args) => {
    if (command === LEGACY_POSTER_LOAD_COMMAND) return null;
    if (command === LEGACY_POSTER_SAVE_COMMAND) migrated = args.snapshot;
  });
  assert.deepEqual(migratedSnapshot, local);
  assert.deepEqual(migrated, local);

  const next = { selectedPosterId: "next", posters: [{ id: "next" }] };
  await writeSharedLegacyPosterSnapshot(fallbackStorage, async (command, args) => {
    if (command === LEGACY_POSTER_LOAD_COMMAND) return null;
    assert.equal(command, LEGACY_POSTER_SAVE_COMMAND);
    assert.deepEqual(args.snapshot, next);
  }, next);
  assert.deepEqual(readLegacyPosterSnapshot(fallbackStorage), next);
});

test("formal shared snapshot is never consumed or overwritten by the A0 bridge", async () => {
  const storage = memoryStorage();
  const local = { selectedPosterId: "local", posters: [{ id: "local" }] };
  const formal = {
    contractVersion: 1,
    revision: 7,
    selectedPosterId: "poster-primary",
    posters: []
  };
  const commands = [];
  writeLegacyPosterSnapshot(storage, local);

  const result = await readSharedLegacyPosterSnapshot(storage, async (command) => {
    commands.push(command);
    return formal;
  });

  assert.deepEqual(result, local);
  assert.deepEqual(commands, [LEGACY_POSTER_LOAD_COMMAND]);
  assert.deepEqual(readLegacyPosterSnapshot(storage), local);

  const writeResult = await writeSharedLegacyPosterSnapshot(storage, async (command) => {
    commands.push(command);
    return formal;
  }, local);
  assert.equal(writeResult, false);
  assert.deepEqual(commands, [LEGACY_POSTER_LOAD_COMMAND, LEGACY_POSTER_LOAD_COMMAND]);
});

test("legacy bridge owns the existing two-poster demo default", () => {
  const snapshot = createLegacyDefaultSnapshot();
  assert.equal(snapshot.selectedPosterId, "night-ops");
  assert.equal(snapshot.posters.length, 2);
  assert.equal(snapshot.posters[0].locked, false);
  assert.equal(snapshot.posters[0].items.length, 6);
  assert.equal(snapshot.posters[1].items.length, 3);
  assert.equal(Object.hasOwn(snapshot, "contractVersion"), false);
});

test("legacy bridge reads and replaces the projected first poster without mutation", () => {
  const snapshot = createLegacyDefaultSnapshot();
  const first = readFirstLegacyPoster(snapshot, snapshot.posters[0]);
  assert.equal(first.id, "night-ops");

  const next = { ...first, title: "RENAMED OPS" };
  const updated = replaceFirstLegacyPoster(snapshot, next);
  assert.equal(updated.posters[0].title, "RENAMED OPS");
  assert.equal(updated.posters[1].id, "indie-arcade");
  assert.equal(snapshot.posters[0].title, "NIGHT OPS", "legacy helper must not mutate its source");
});
