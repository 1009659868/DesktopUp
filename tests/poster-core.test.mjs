import test from "node:test";
import assert from "node:assert/strict";
import {
  STORAGE_KEY,
  buildPosterMarkup,
  createDefaultSnapshot,
  readSharedPosterSnapshot,
  readFirstPoster,
  replaceFirstPoster,
  readPosterSnapshot,
  writePosterSnapshot,
  writeSharedPosterSnapshot
} from "../app/src/poster-core.js";

function memoryStorage() {
  const values = new Map();
  return {
    getItem: (key) => values.has(key) ? values.get(key) : null,
    setItem: (key, value) => values.set(key, String(value)),
    removeItem: (key) => values.delete(key)
  };
}

test("poster core renders locked native surface without demo handles", () => {
  const markup = buildPosterMarkup({
    title: "NIGHT OPS",
    subtitle: "TACTICAL ARCHIVE / 01",
    theme: "nocturne",
    accent: "#72e4ff",
    opacity: 94,
    density: 1,
    width: 490,
    height: 392,
    collapsed: false,
    locked: true,
    items: [{ title: "<b>AL</b>", mark: "AL", hue: "cyan" }]
  }, { surface: "native" });

  assert.match(markup, /class="poster theme-nocturne is-locked"/);
  assert.match(markup, /data-native-drag/);
  assert.doesNotMatch(markup, /data-resize-handle/);
  assert.match(markup, /&lt;b&gt;AL&lt;\/b&gt;/);
  assert.doesNotMatch(markup, /<b>AL<\/b>/);
  assert.match(markup, /aria-pressed="true"/);
});

test("poster core renders selected demo surface with geometry and edit handle", () => {
  const markup = buildPosterMarkup({
    id: "night-ops",
    x: 72,
    y: 154,
    width: 490,
    height: 392,
    collapsed: true,
    locked: false,
    theme: "nocturne",
    accent: "#72e4ff",
    opacity: 94,
    density: 1,
    items: []
  }, {
    surface: "demo",
    posterId: "night-ops",
    posterIndex: 1,
    isSelected: true
  });

  assert.match(markup, /data-poster-id="night-ops"/);
  assert.match(markup, /data-drag-handle/);
  assert.match(markup, /data-resize-handle/);
  assert.match(markup, /is-collapsed is-selected/);
  assert.match(markup, /--poster-x:72px/);
  assert.match(markup, /--poster-index:1/);
  assert.match(markup, /00 TITLES/);
});

test("poster core keeps storage shape as the single shared contract", () => {
  const storage = memoryStorage();
  assert.equal(readPosterSnapshot(storage), null);

  storage.setItem(STORAGE_KEY, "{");
  assert.equal(readPosterSnapshot(storage), null);

  writePosterSnapshot(storage, {
    selectedPosterId: "night-ops",
    posters: [{ id: "night-ops" }]
  });
  const snapshot = readPosterSnapshot(storage);
  assert.equal(snapshot.selectedPosterId, "night-ops");
  assert.deepEqual(snapshot.posters, [{ id: "night-ops" }]);
});

test("shared snapshot bridge prefers Rust state and migrates local fallback", async () => {
  const storage = memoryStorage();
  const local = { selectedPosterId: "local", posters: [{ id: "local" }] };
  const shared = { selectedPosterId: "shared", posters: [{ id: "shared" }] };
  writePosterSnapshot(storage, local);

  const preferred = await readSharedPosterSnapshot(storage, async (command) => {
    assert.equal(command, "load_poster_snapshot");
    return shared;
  });
  assert.deepEqual(preferred, shared);
  assert.deepEqual(readPosterSnapshot(storage), shared);

  let migrated = null;
  const fallbackStorage = memoryStorage();
  writePosterSnapshot(fallbackStorage, local);
  const migratedSnapshot = await readSharedPosterSnapshot(fallbackStorage, async (command, args) => {
    if (command === "load_poster_snapshot") return null;
    if (command === "save_poster_snapshot") migrated = args.snapshot;
  });
  assert.deepEqual(migratedSnapshot, local);
  assert.deepEqual(migrated, local);

  const next = { selectedPosterId: "next", posters: [{ id: "next" }] };
  await writeSharedPosterSnapshot(fallbackStorage, async (command, args) => {
    assert.equal(command, "save_poster_snapshot");
    assert.deepEqual(args.snapshot, next);
  }, next);
  assert.deepEqual(readPosterSnapshot(fallbackStorage), next);
});

test("poster core provides one canonical default snapshot", () => {
  const snapshot = createDefaultSnapshot();
  assert.equal(snapshot.selectedPosterId, "night-ops");
  assert.equal(snapshot.posters.length, 2);
  assert.equal(snapshot.posters[0].locked, false);
  assert.equal(snapshot.posters[0].items.length, 6);
  assert.equal(snapshot.posters[1].items.length, 3);
});

test("poster core reads and replaces the first poster through shared helpers", () => {
  const snapshot = createDefaultSnapshot();
  const first = readFirstPoster(snapshot, snapshot.posters[0]);
  assert.equal(first.id, "night-ops");

  const next = { ...first, title: "RENAMED OPS" };
  const updated = replaceFirstPoster(snapshot, next);
  assert.equal(updated.posters[0].title, "RENAMED OPS");
  assert.equal(updated.posters[1].id, "indie-arcade");
  assert.equal(snapshot.posters[0].title, "NIGHT OPS", "shared helpers must not mutate the source snapshot");
});
