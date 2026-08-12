import { readFileSync, readdirSync } from "node:fs";
import test from "node:test";
import assert from "node:assert/strict";
import {
  POSTER_CONTRACT_VERSION,
  POSTER_PERSISTENT_FIELDS,
  POSTER_RUNTIME_ONLY_FIELDS,
  POSTER_SNAPSHOT_FIELDS
} from "../app/src/poster-contract.js";

function sourceFiles(directory, extension) {
  return readdirSync(directory, { withFileTypes: true })
    .filter((entry) => entry.isFile() && entry.name.endsWith(extension))
    .map((entry) => ({
      name: entry.name,
      source: readFileSync(`${directory}/${entry.name}`, "utf8")
    }));
}

function filesContaining(files, token) {
  return files.filter((file) => file.source.includes(token)).map((file) => file.name);
}

test("C-PCB public persistence field allowlists are sealed for C-PSS", () => {
  assert.equal(POSTER_CONTRACT_VERSION, 1);
  assert.deepEqual(POSTER_SNAPSHOT_FIELDS, [
    "contractVersion",
    "revision",
    "selectedPosterId",
    "posters"
  ]);
  assert.deepEqual(POSTER_PERSISTENT_FIELDS, [
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
  assert.deepEqual(POSTER_RUNTIME_ONLY_FIELDS, [
    "interaction",
    "interactionMode",
    "runtime",
    "runtimeMode"
  ]);
});

test("A0 sources are read-only and formal state commands have one frontend owner", () => {
  const appSources = sourceFiles("app/src", ".js");
  for (const token of ["desktop-up.a0-demo.v1", "inspect_poster_snapshot"]) {
    assert.deepEqual(filesContaining(appSources, token), ["poster-legacy-bridge.js"], token);
  }
  for (const token of [
    "load_poster_state",
    "replace_poster_state",
    "migrate_poster_state",
    "poster_state_bootstrap_context"
  ]) {
    assert.deepEqual(filesContaining(appSources, token), ["poster-state-store.js"], token);
  }
  assert.deepEqual(filesContaining(appSources, "load_poster_snapshot"), []);
  assert.deepEqual(filesContaining(appSources, "save_poster_snapshot"), []);

  const posterCore = readFileSync("app/src/poster-core.js", "utf8");
  const controlCenter = readFileSync("app/src/main.js", "utf8");
  const posterSurface = readFileSync("app/src/poster-surface.js", "utf8");
  const combined = appSources.map((file) => file.source).join("\n");
  assert.doesNotMatch(posterCore, /legacy|localStorage|poster_state/i);
  assert.match(controlCenter, /from "\.\/poster-state-store\.js"/);
  assert.match(posterSurface, /from "\.\/poster-state-store\.js"/);
  assert.doesNotMatch(posterSurface, /poster-legacy-bridge/);
  assert.doesNotMatch(combined, /localStorage\.(setItem|removeItem)/);
});

test("frontend host projection uses only the canonical surface command", () => {
  const appSources = sourceFiles("app/src", ".js");
  const combined = appSources.map((file) => file.source).join("\n");
  const posterSurface = readFileSync("app/src/poster-surface.js", "utf8");

  assert.match(posterSurface, /invokeNative\("sync_host_surface", args\)/);
  assert.deepEqual(filesContaining(appSources, '"sync_host_surface"'), ["poster-surface.js"]);
  assert.doesNotMatch(combined, /"resize_poster_surface"/);
  assert.doesNotMatch(combined, /"set_poster_interaction_region"/);
  assert.doesNotMatch(combined, /"runtime-pause-state"/);
});

test("template default and fallback constants retain one authority per language", () => {
  const jsSources = sourceFiles("app/src", ".js");
  const rustSources = sourceFiles("src-tauri/src", ".rs");
  assert.deepEqual(
    filesContaining(jsSources, "export const DEFAULT_TEMPLATE_KEY"),
    ["poster-template-resolution.js"]
  );
  assert.deepEqual(
    filesContaining(jsSources, "export const INTERNAL_FALLBACK_TEMPLATE_KEY"),
    ["poster-template-resolution.js"]
  );
  assert.deepEqual(
    filesContaining(rustSources, "pub const DEFAULT_TEMPLATE_KEY"),
    ["poster_template_resolution.rs"]
  );
  assert.deepEqual(
    filesContaining(rustSources, "pub const INTERNAL_FALLBACK_TEMPLATE_KEY"),
    ["poster_template_resolution.rs"]
  );
});
