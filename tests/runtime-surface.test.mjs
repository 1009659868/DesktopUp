import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import test from "node:test";
import {
  STORAGE_KEY,
  buildPosterMarkup,
  readPosterSnapshot,
  writePosterSnapshot
} from "../app/src/poster-core.js";

const tauriConfig = JSON.parse(readFileSync("src-tauri/tauri.conf.json", "utf8"));
const releaseExe = "src-tauri/target/release/desktop-up.exe";

function windowConfig(label) {
  return tauriConfig.app.windows.find((window) => window.label === label);
}

function memoryStorage() {
  const values = new Map();
  return {
    getItem: (key) => values.has(key) ? values.get(key) : null,
    setItem: (key, value) => values.set(key, String(value)),
    removeItem: (key) => values.delete(key)
  };
}

test("cold start surface contract is configured in Tauri", () => {
  const main = windowConfig("main");
  const poster = windowConfig("poster");

  assert.ok(main, "main control-center window must be configured");
  assert.ok(poster, "poster surface window must be configured");
  assert.equal(main.visible, false, "control center must stay hidden on cold start");
  assert.equal(poster.visible, true, "poster surface must remain the default visible surface");
  assert.equal(poster.title, "", "poster native title must stay empty");
  assert.equal(poster.url, "poster.html", "poster window must load the native poster page");
  assert.equal(poster.transparent, true, "poster surface must be transparent");
  assert.deepEqual(
    poster.backgroundColor,
    [0, 0, 0, 0],
    "poster window and WebView must start with a fully transparent background"
  );
  assert.equal(poster.decorations, false, "poster surface must not use native decorations");
  assert.equal(poster.focus, false, "poster surface must not steal focus");
  assert.equal(poster.focusable, false, "poster surface must not become an active application");
  assert.equal(
    poster.alwaysOnBottom,
    false,
    "the native desktop anchor must own Z-order instead of Tauri window levels"
  );
  assert.equal(poster.closable, false, "poster surface must not be closable by the user");
  assert.equal(poster.skipTaskbar, true, "poster surface must never acquire a taskbar button");
});

test("release executable uses the Windows GUI subsystem", { skip: !existsSync(releaseExe) }, () => {
  const pe = readFileSync(releaseExe);
  const peOffset = pe.readUInt32LE(0x3c);
  assert.equal(pe.toString("ascii", peOffset, peOffset + 4), "PE\0\0", "invalid PE signature");
  const optionalOffset = peOffset + 4 + 20;
  assert.equal(
    pe.readUInt16LE(optionalOffset + 68),
    2,
    "release executable must use the Windows GUI subsystem"
  );
});

test("poster snapshot storage is a single shared contract", () => {
  const storage = memoryStorage();
  assert.equal(readPosterSnapshot(storage), null, "empty storage must produce no snapshot");

  storage.setItem(STORAGE_KEY, "{");
  assert.equal(readPosterSnapshot(storage), null, "corrupt JSON must not crash the loader");

  writePosterSnapshot(storage, {
    selectedPosterId: "night-ops",
    posters: [{ id: "night-ops", title: "NIGHT OPS" }]
  });
  const snapshot = readPosterSnapshot(storage);
  assert.equal(snapshot.selectedPosterId, "night-ops");
  assert.equal(snapshot.posters[0].title, "NIGHT OPS");
});

test("poster core renders the native surface without demo controls", () => {
  const markup = buildPosterMarkup({
    title: "NIGHT OPS",
    theme: "nocturne",
    accent: "#72e4ff",
    opacity: 94,
    density: 1,
    collapsed: false,
    locked: true,
    items: [{ title: "AFTERLIGHT", mark: "AL", hue: "cyan" }]
  }, { surface: "native" });

  assert.match(markup, /data-native-drag/);
  assert.match(markup, /data-action="lock"/);
  assert.match(markup, /data-action="collapse"/);
  assert.doesNotMatch(markup, /data-resize-handle/);
  assert.doesNotMatch(markup, /data-poster-id=/);
});

test("poster core renders the demo surface with geometry and edit handles", () => {
  const markup = buildPosterMarkup({
    id: "night-ops",
    x: 72,
    y: 154,
    width: 490,
    height: 392,
    theme: "nocturne",
    accent: "#72e4ff",
    opacity: 94,
    density: 1,
    collapsed: false,
    locked: false,
    items: []
  }, {
    surface: "demo",
    posterId: "night-ops",
    posterIndex: 0,
    isSelected: true
  });

  assert.match(markup, /data-poster-id="night-ops"/);
  assert.match(markup, /data-drag-handle/);
  assert.match(markup, /data-resize-handle/);
  assert.match(markup, /--poster-width:490px/);
  assert.match(markup, /is-selected/);
});
