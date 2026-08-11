import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import {
  adaptLegacyPosterSnapshot,
  createDefaultPosterSnapshot,
  DEFAULT_TEMPLATE_KEY,
  INTERNAL_FALLBACK_TEMPLATE_KEY,
  POSTER_CONTRACT_VERSION,
  resolvePosterTemplate,
  validatePosterSnapshot
} from "../app/src/poster-contract.js";

const CONTRACT_DECISION_MATRIX = JSON.parse(readFileSync(
  new URL("./fixtures/poster-contract-v1.json", import.meta.url),
  "utf8"
));

function validSnapshot() {
  return createDefaultPosterSnapshot({
    title: "DESKTOP COLLECTION",
    primaryWorkArea: {
      displayId: "primary-1080p",
      isPrimary: true,
      width: 1920,
      height: 1080
    }
  });
}

test("JS matches the shared poster-contract-v1 decision matrix", () => {
  for (const sample of CONTRACT_DECISION_MATRIX) {
    assert.equal(validatePosterSnapshot(sample.snapshot).ok, sample.valid, sample.name);
  }
});

test("C-PCB default snapshot creates exactly one poster with frozen state defaults", () => {
  const snapshot = validSnapshot();
  assert.equal(snapshot.contractVersion, POSTER_CONTRACT_VERSION);
  assert.equal(snapshot.revision, 0);
  assert.equal(snapshot.posters.length, 1);
  assert.equal(snapshot.selectedPosterId, "poster-primary");
  assert.deepEqual(snapshot.posters[0], {
    id: "poster-primary",
    title: "DESKTOP COLLECTION",
    subtitle: "",
    monitorIdentity: "primary",
    anchor: "top-right",
    normalizedRect: { x: 0.9905277401894452, y: 0.5 },
    width: 422,
    height: 1032,
    collapsedState: "expanded",
    autoCollapseEnabled: false,
    autoCollapseDelaySeconds: 3,
    isLocked: false,
    layoutKind: "grid",
    layoutSpec: {},
    templateKey: DEFAULT_TEMPLATE_KEY
  });
});

test("default snapshot requires a title and primary work-area evidence", () => {
  assert.throws(() => createDefaultPosterSnapshot(), /non-empty title/);
  assert.throws(
    () => createDefaultPosterSnapshot({ title: "DESKTOP COLLECTION" }),
    /display_id_required/
  );
  assert.throws(
    () => createDefaultPosterSnapshot({
      title: "DESKTOP COLLECTION",
      primaryWorkArea: { displayId: "secondary", isPrimary: false, width: 1920, height: 1080 }
    }),
    /primary_display_required/
  );
});

test("contract rejects unknown versions, duplicate ids and dangling selection", () => {
  const snapshot = validSnapshot();
  snapshot.contractVersion = 99;
  snapshot.posters.push({ ...snapshot.posters[0] });
  snapshot.selectedPosterId = "missing";

  const result = validatePosterSnapshot(snapshot);
  assert.equal(result.ok, false);
  assert.deepEqual(
    new Set(result.errors.map((error) => error.code)),
    new Set(["unsupported_contract_version", "duplicate_poster_id", "selected_poster_not_found"])
  );
});

test("runtime and interaction state cannot leak into persistent poster data", () => {
  const snapshot = validSnapshot();
  snapshot.posters[0].runtimeMode = "paused";
  snapshot.posters[0].interactionMode = "editing";

  const result = validatePosterSnapshot(snapshot);
  assert.equal(result.ok, false);
  assert.deepEqual(
    result.errors.map((error) => error.code),
    ["runtime_state_is_not_persistent", "runtime_state_is_not_persistent"]
  );
});

test("minimal fallback is rejected as persisted user selection", () => {
  const snapshot = validSnapshot();
  snapshot.posters[0].templateKey = INTERNAL_FALLBACK_TEMPLATE_KEY;
  const result = validatePosterSnapshot(snapshot);
  assert.equal(result.ok, false);
  assert.equal(result.errors[0].code, "fallback_template_is_not_persistent");
});

test("non-JSON layout specs fail structurally instead of throwing during cloning", () => {
  const snapshot = validSnapshot();
  snapshot.posters[0].layoutSpec = { columns: 3 };
  snapshot.posters[0].layoutSpec.self = snapshot.posters[0].layoutSpec;

  let result;
  assert.doesNotThrow(() => {
    result = validatePosterSnapshot(snapshot);
  });
  assert.equal(result.ok, false);
  assert.equal(result.errors.some((error) => error.code === "invalid_layout_spec"), true);
});

test("template fallback is ephemeral and does not rewrite the poster", () => {
  const poster = validSnapshot().posters[0];
  poster.templateKey = "missing-user-template";
  const before = structuredClone(poster);
  const resolved = resolvePosterTemplate(poster, [DEFAULT_TEMPLATE_KEY]);

  assert.deepEqual(resolved, {
    templateKey: INTERNAL_FALLBACK_TEMPLATE_KEY,
    requestedTemplateKey: "missing-user-template",
    isFallback: true,
    reason: "requested_template_unavailable"
  });
  assert.deepEqual(poster, before);
});

test("legacy template resolver fails safely for a whitespace-only key", () => {
  const poster = validSnapshot().posters[0];
  poster.templateKey = "   ";

  assert.deepEqual(resolvePosterTemplate(poster, [DEFAULT_TEMPLATE_KEY]), {
    templateKey: INTERNAL_FALLBACK_TEMPLATE_KEY,
    requestedTemplateKey: "   ",
    isFallback: true,
    reason: "requested_template_unavailable"
  });
  assert.equal(poster.templateKey, "   ");
});

test("legacy adapter is explicit, non-mutating and preserves one-poster scope", () => {
  const legacy = {
    selectedPosterId: "night-ops",
    posters: [{
      id: "night-ops",
      title: "NIGHT OPS",
      subtitle: "TACTICAL ARCHIVE / 01",
      theme: "nocturne",
      density: 1,
      x: 72,
      y: 154,
      width: 490,
      height: 392,
      collapsed: false,
      locked: true,
      autoCollapse: false
    }]
  };
  const before = structuredClone(legacy);
  const result = adaptLegacyPosterSnapshot(legacy, { workArea: { width: 1920, height: 1080 } });

  assert.equal(result.ok, true);
  assert.equal(result.snapshot.posters.length, 1);
  assert.equal(result.snapshot.posters[0].collapsedState, "expanded");
  assert.equal(result.snapshot.posters[0].isLocked, true);
  assert.equal(result.snapshot.posters[0].anchor, "free");
  assert.equal(result.snapshot.posters[0].templateKey, DEFAULT_TEMPLATE_KEY);
  assert.equal(result.migrations.length, 4);
  assert.deepEqual(legacy, before);
});

test("legacy adapter requires work-area evidence and reports invalid geometry", () => {
  const missingWorkArea = adaptLegacyPosterSnapshot({ posters: [] });
  assert.equal(missingWorkArea.ok, false);
  assert.equal(missingWorkArea.errors[0].code, "legacy_work_area_required");

  const invalidGeometry = adaptLegacyPosterSnapshot({
    selectedPosterId: "legacy",
    posters: [{ id: "legacy", title: "Legacy", width: 0, height: 392 }]
  }, { workArea: { width: 1920, height: 1080 } });
  assert.equal(invalidGeometry.ok, false);
  assert.equal(invalidGeometry.errors.some((error) => error.code === "invalid_dimension"), true);
});
