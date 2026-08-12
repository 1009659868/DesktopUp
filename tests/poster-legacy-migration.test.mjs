import test from "node:test";
import assert from "node:assert/strict";
import {
  LEGACY_POSTER_SOURCE_KINDS,
  LEGACY_POSTER_STORAGE_KEY,
  createLegacyDefaultSnapshot,
  prepareLegacyPosterMigration
} from "../app/src/poster-legacy-bridge.js";

test("legacy migration retains the full source but adapts only the first poster", () => {
  const legacy = createLegacyDefaultSnapshot();
  const before = structuredClone(legacy);
  const result = prepareLegacyPosterMigration(legacy, {
    workArea: { width: 1920, height: 1080 },
    sourceKind: "sharedJson",
    sourceKey: "poster-snapshot.json"
  });

  assert.equal(result.ok, true);
  assert.deepEqual(LEGACY_POSTER_SOURCE_KINDS, ["sharedJson", "localStorage"]);
  assert.deepEqual(result.request.source, {
    kind: "sharedJson",
    key: "poster-snapshot.json"
  });
  assert.equal(result.request.legacySnapshot.posters.length, 2);
  assert.equal(result.request.legacySnapshot.posters[1].id, "indie-arcade");
  assert.equal(result.request.adaptedSnapshot.posters.length, 1);
  assert.equal(result.request.adaptedSnapshot.posters[0].id, "night-ops");
  assert.equal(result.request.adaptedSnapshot.selectedPosterId, "night-ops");
  assert.equal(result.request.adapterMigrations.length, 4);
  assert.deepEqual(result.request.adapterErrors, []);
  assert.deepEqual(legacy, before);
});

test("legacy migration carries adapter failures without creating a default", () => {
  const legacy = createLegacyDefaultSnapshot();
  legacy.posters[0].width = 0;
  const result = prepareLegacyPosterMigration(legacy, {
    workArea: { width: 1920, height: 1080 },
    sourceKind: "localStorage",
    sourceKey: LEGACY_POSTER_STORAGE_KEY
  });

  assert.equal(result.ok, false);
  assert.equal(result.request.adaptedSnapshot, null);
  assert.equal(result.request.legacySnapshot.posters.length, 2);
  assert.equal(result.request.adapterErrors.some((error) => error.code === "invalid_dimension"), true);
  assert.deepEqual(result.errors, result.request.adapterErrors);
});

test("legacy migration rejects formal snapshots and unknown sources", () => {
  const formal = prepareLegacyPosterMigration({
    contractVersion: 1,
    revision: 0,
    selectedPosterId: "formal",
    posters: []
  }, {
    workArea: { width: 1920, height: 1080 },
    sourceKind: "sharedJson",
    sourceKey: "poster-snapshot.json"
  });
  assert.equal(formal.ok, false);
  assert.equal(formal.request, null);
  assert.equal(formal.errors[0].code, "formal_snapshot_requires_poster_contract");

  const unknownSource = prepareLegacyPosterMigration(createLegacyDefaultSnapshot(), {
    workArea: { width: 1920, height: 1080 },
    sourceKind: "unknown",
    sourceKey: "poster-snapshot.json"
  });
  assert.equal(unknownSource.ok, false);
  assert.equal(unknownSource.request, null);
  assert.equal(unknownSource.errors[0].code, "invalid_legacy_source");
});
