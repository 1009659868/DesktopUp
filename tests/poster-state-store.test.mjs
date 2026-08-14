import test from "node:test";
import assert from "node:assert/strict";
import { createDefaultPosterSnapshot, validatePosterSnapshot } from "../app/src/poster-contract.js";
import { createLegacyDefaultSnapshot } from "../app/src/poster-legacy-bridge.js";
import {
  BUILTIN_THEME_TEMPLATE_KEYS,
  DEFAULT_POSTER_SUBTITLE,
  DEFAULT_POSTER_TITLE,
  POSTER_STATE_CONTEXT_COMMAND,
  POSTER_STATE_LOAD_COMMAND,
  POSTER_STATE_MIGRATE_COMMAND,
  POSTER_STATE_REPLACE_COMMAND,
  PosterStateClient,
  isRecognizedPriorFormalDefault,
  projectPosterStateToRuntime,
  projectRuntimeToPosterState
} from "../app/src/poster-state-store.js";

const PRIMARY_WORK_AREA = Object.freeze({
  displayId: "primary-1080p",
  isPrimary: true,
  width: 1920,
  height: 1080
});

function clone(value) {
  return JSON.parse(JSON.stringify(value));
}

function memoryStorage(snapshot = null) {
  const raw = snapshot == null ? null : JSON.stringify(snapshot);
  let writes = 0;
  return {
    getItem: () => raw,
    setItem: () => { writes += 1; },
    writes: () => writes
  };
}

function fakeNative({ initialState = null, legacyInspection = { status: "noData" } } = {}) {
  let state = initialState == null ? null : clone(initialState);
  let conflictNextReplace = false;
  let conflictMutation = null;
  const calls = [];

  async function invoke(command, args = {}) {
    calls.push({ command, args: clone(args) });
    if (command === POSTER_STATE_CONTEXT_COMMAND) {
      return { primaryWorkArea: clone(PRIMARY_WORK_AREA) };
    }
    if (command === POSTER_STATE_LOAD_COMMAND) {
      return state == null
        ? { status: "noData" }
        : { status: "loaded", revision: state.revision, snapshot: clone(state) };
    }
    if (command === "inspect_poster_snapshot") return clone(legacyInspection);
    if (command === POSTER_STATE_MIGRATE_COMMAND) {
      if (state != null) {
        return {
          status: "skippedExistingState",
          revision: state.revision,
          sourceFingerprint: "existing",
          backupPath: null,
          source: clone(args.request.source)
        };
      }
      if (args.request.adapterErrors.length || args.request.adaptedSnapshot == null) {
        return {
          status: "rejected",
          code: "adapterFailed",
          message: "adapter failed",
          adapterErrors: clone(args.request.adapterErrors)
        };
      }
      state = clone(args.request.adaptedSnapshot);
      state.revision = 0;
      return {
        status: "migrated",
        source: clone(args.request.source),
        sourceFingerprint: "legacy-source",
        backupPath: "poster-state-backups/legacy-source.json",
        revision: 0,
        snapshot: clone(state),
        adapterMigrations: clone(args.request.adapterMigrations)
      };
    }
    if (command === POSTER_STATE_REPLACE_COMMAND) {
      const actualRevision = state?.revision ?? null;
      if (conflictNextReplace) {
        conflictNextReplace = false;
        state = clone(state);
        if (typeof conflictMutation === "function") {
          state = conflictMutation(state) ?? state;
        }
        conflictMutation = null;
        state.revision += 1;
        return {
          status: "conflict",
          expectedRevision: args.expectedRevision,
          actualRevision: state.revision
        };
      }
      if (args.expectedRevision !== actualRevision) {
        return {
          status: "conflict",
          expectedRevision: args.expectedRevision,
          actualRevision
        };
      }
      state = clone(args.snapshot);
      state.revision = actualRevision == null ? 0 : actualRevision + 1;
      return {
        status: "stored",
        previousRevision: actualRevision,
        revision: state.revision,
        snapshot: clone(state)
      };
    }
    throw new Error(`unexpected command ${command}`);
  }

  return {
    invoke,
    calls,
    state: () => clone(state),
    conflictOnce: (mutation = null) => {
      conflictNextReplace = true;
      conflictMutation = mutation;
    }
  };
}

test("bootstrap migrates only the first A0 poster and leaves both legacy sources read-only", async () => {
  const legacy = createLegacyDefaultSnapshot();
  const storage = memoryStorage(legacy);
  const native = fakeNative({
    legacyInspection: { status: "loaded", kind: "legacyA0", snapshot: legacy }
  });
  const client = new PosterStateClient({ invoke: native.invoke, storage });

  const result = await client.bootstrap();
  assert.equal(result.status, "ready");
  assert.equal(result.origin, "legacyMigration");
  assert.equal(result.snapshot.posters.length, 1);
  assert.equal(result.snapshot.posters[0].id, "night-ops");
  assert.equal(result.runtimeSnapshot.posters[0].items.length, 6);
  assert.equal(storage.writes(), 0);
  const migrationCall = native.calls.find((call) => call.command === POSTER_STATE_MIGRATE_COMMAND);
  assert.equal(migrationCall.args.request.legacySnapshot.posters.length, 2);
  assert.equal(migrationCall.args.request.adaptedSnapshot.posters.length, 1);
  assert.equal(native.calls.some((call) => call.command === "save_poster_snapshot"), false);
});

test("valid SQLite state bypasses legacy inspection and projects stable demo content", async () => {
  const snapshot = createDefaultPosterSnapshot({
    title: "SQLITE POSTER",
    primaryWorkArea: PRIMARY_WORK_AREA
  });
  const native = fakeNative({ initialState: snapshot });
  const client = new PosterStateClient({ invoke: native.invoke, storage: memoryStorage() });
  const result = await client.bootstrap();

  assert.equal(result.status, "ready");
  assert.equal(result.origin, "sqlite");
  assert.equal(result.runtimeSnapshot.posters[0].title, "SQLITE POSTER");
  assert.equal(result.runtimeSnapshot.posters[0].items.length, 6);
  assert.equal(native.calls.some((call) => call.command === "inspect_poster_snapshot"), false);
});

test("explicit absence creates one formal default from the native primary work area", async () => {
  const native = fakeNative();
  const client = new PosterStateClient({ invoke: native.invoke, storage: memoryStorage() });
  const result = await client.bootstrap();

  assert.equal(result.status, "ready");
  assert.equal(result.origin, "formalDefault");
  assert.equal(result.snapshot.revision, 0);
  assert.equal(result.snapshot.posters.length, 1);
  assert.equal(result.snapshot.posters[0].title, DEFAULT_POSTER_TITLE);
  assert.equal(result.snapshot.posters[0].subtitle, DEFAULT_POSTER_SUBTITLE);
  assert.equal(result.snapshot.posters[0].monitorIdentity, "primary");
  assert.equal(validatePosterSnapshot(result.snapshot).ok, true);
});

test("exact prior revision-zero formal default is recovered once through CAS", async () => {
  const prior = createDefaultPosterSnapshot({
    title: "DESKTOP COLLECTION",
    primaryWorkArea: PRIMARY_WORK_AREA
  });
  assert.equal(isRecognizedPriorFormalDefault(prior, { primaryWorkArea: PRIMARY_WORK_AREA }), true);
  const native = fakeNative({ initialState: prior });
  const client = new PosterStateClient({ invoke: native.invoke, storage: memoryStorage() });

  const result = await client.bootstrap();

  assert.equal(result.status, "ready");
  assert.equal(result.origin, "formalDefaultIdentityRecovery");
  assert.equal(result.snapshot.revision, 1);
  assert.equal(result.snapshot.posters[0].title, DEFAULT_POSTER_TITLE);
  assert.equal(result.snapshot.posters[0].subtitle, DEFAULT_POSTER_SUBTITLE);
  const replacements = native.calls.filter((call) => call.command === POSTER_STATE_REPLACE_COMMAND);
  assert.equal(replacements.length, 1);
  assert.equal(replacements[0].args.expectedRevision, 0);
});

test("prior-looking state with any user change is never recovered", async () => {
  const changed = createDefaultPosterSnapshot({
    title: "DESKTOP COLLECTION",
    primaryWorkArea: PRIMARY_WORK_AREA
  });
  changed.posters[0].isLocked = true;
  assert.equal(isRecognizedPriorFormalDefault(changed, { primaryWorkArea: PRIMARY_WORK_AREA }), false);
  const native = fakeNative({ initialState: changed });
  const client = new PosterStateClient({ invoke: native.invoke, storage: memoryStorage() });

  const result = await client.bootstrap();

  assert.equal(result.status, "ready");
  assert.equal(result.origin, "sqlite");
  assert.equal(result.snapshot.posters[0].title, "DESKTOP COLLECTION");
  assert.equal(result.snapshot.posters[0].isLocked, true);
  assert.equal(native.calls.some((call) => call.command === POSTER_STATE_REPLACE_COMMAND), false);
});

test("default identity recovery write failure preserves the accepted SQLite state", async () => {
  const prior = createDefaultPosterSnapshot({
    title: "DESKTOP COLLECTION",
    primaryWorkArea: PRIMARY_WORK_AREA
  });
  const native = fakeNative({ initialState: prior });
  const invoke = async (command, args) => {
    if (command === POSTER_STATE_REPLACE_COMMAND) {
      throw { code: "write_database_failed", message: "injected recovery failure" };
    }
    return native.invoke(command, args);
  };
  const client = new PosterStateClient({ invoke, storage: memoryStorage() });

  const result = await client.bootstrap();

  assert.equal(result.status, "ready");
  assert.equal(result.origin, "sqlite");
  assert.equal(result.recovery.status, "deferred");
  assert.equal(result.recovery.code, "write_database_failed");
  assert.deepEqual(result.snapshot, prior);
  assert.deepEqual(client.snapshot, prior);
});

test("corrupt shared legacy input fails closed without migration or default creation", async () => {
  const native = fakeNative({
    legacyInspection: {
      status: "rejected",
      code: "corruptJson",
      message: "invalid JSON"
    }
  });
  const client = new PosterStateClient({ invoke: native.invoke, storage: memoryStorage() });
  const result = await client.bootstrap();

  assert.equal(result.status, "rejected");
  assert.equal(result.code, "corruptJson");
  assert.equal(native.state(), null);
  assert.equal(native.calls.some((call) => call.command === POSTER_STATE_MIGRATE_COMMAND), false);
  assert.equal(native.calls.some((call) => call.command === POSTER_STATE_REPLACE_COMMAND), false);
});

test("formal bridge snapshot imports once while retaining the read-only source", async () => {
  const formal = createDefaultPosterSnapshot({
    title: "FORMAL BRIDGE",
    primaryWorkArea: PRIMARY_WORK_AREA
  });
  const storage = memoryStorage();
  const native = fakeNative({
    legacyInspection: { status: "loaded", kind: "formalV1", snapshot: formal }
  });
  const client = new PosterStateClient({ invoke: native.invoke, storage });
  const result = await client.bootstrap();

  assert.equal(result.status, "ready");
  assert.equal(result.origin, "formalBridgeImport");
  assert.equal(result.snapshot.posters[0].title, "FORMAL BRIDGE");
  assert.equal(storage.writes(), 0);
});

test("runtime saves only formal fields and retries one expected-revision conflict", async () => {
  const formal = createDefaultPosterSnapshot({
    title: "STATEFUL",
    primaryWorkArea: PRIMARY_WORK_AREA
  });
  const native = fakeNative({ initialState: formal });
  const client = new PosterStateClient({ invoke: native.invoke, storage: memoryStorage() });
  assert.equal((await client.bootstrap()).status, "ready");

  const runtime = client.runtimeSnapshot();
  runtime.posters[0].locked = true;
  runtime.posters[0].collapsed = true;
  runtime.posters[0].theme = "ember";
  runtime.posters[0].items.push({ id: "runtime-only", title: "RUNTIME ONLY" });
  native.conflictOnce((external) => {
    external.posters[0].subtitle = "EXTERNAL UPDATE";
    return external;
  });
  const result = await client.saveRuntimeState(runtime);

  assert.equal(result.status, "stored");
  assert.equal(result.revision, 2);
  const stored = native.state();
  assert.equal(stored.posters[0].isLocked, true);
  assert.equal(stored.posters[0].collapsedState, "collapsed");
  assert.equal(stored.posters[0].subtitle, "EXTERNAL UPDATE");
  assert.equal(Object.hasOwn(stored.posters[0], "theme"), false);
  assert.equal(Object.hasOwn(stored.posters[0], "items"), false);
  assert.equal(validatePosterSnapshot(stored).ok, true);
  assert.equal(
    native.calls.filter((call) => call.command === POSTER_STATE_REPLACE_COMMAND).length,
    2
  );
});

test("built-in style presets persist through templateKey and survive formal refresh", async () => {
  const formal = createDefaultPosterSnapshot({
    title: "STYLE PRESET",
    primaryWorkArea: PRIMARY_WORK_AREA
  });
  const native = fakeNative({ initialState: formal });
  const client = new PosterStateClient({ invoke: native.invoke, storage: memoryStorage() });
  const bootstrapped = await client.bootstrap();
  assert.equal(bootstrapped.runtimeSnapshot.posters[0].theme, "nocturne");

  const runtime = client.runtimeSnapshot();
  runtime.posters[0].theme = "ember";
  const stored = await client.saveRuntimeState(runtime);

  assert.equal(stored.status, "stored");
  assert.equal(stored.snapshot.posters[0].templateKey, BUILTIN_THEME_TEMPLATE_KEYS.ember);
  assert.equal(client.runtimeSnapshot().posters[0].theme, "ember");

  const restarted = new PosterStateClient({ invoke: native.invoke, storage: memoryStorage() });
  const reloaded = await restarted.bootstrap();
  assert.equal(reloaded.origin, "sqlite");
  assert.equal(reloaded.runtimeSnapshot.posters[0].theme, "ember");
  assert.equal(reloaded.snapshot.posters[0].templateKey, BUILTIN_THEME_TEMPLATE_KEYS.ember);
});

test("pending formal mutations remain visible until the accepted revision catches up", async () => {
  const formal = createDefaultPosterSnapshot({
    title: "PENDING OVERLAY",
    primaryWorkArea: PRIMARY_WORK_AREA
  });
  const native = fakeNative({ initialState: formal });
  let releaseReplace;
  let replaceArgs;
  const invoke = async (command, args) => {
    if (command !== POSTER_STATE_REPLACE_COMMAND) return native.invoke(command, args);
    replaceArgs = clone(args);
    return new Promise((resolve) => { releaseReplace = resolve; });
  };
  const client = new PosterStateClient({ invoke, storage: memoryStorage() });
  assert.equal((await client.bootstrap()).status, "ready");
  const runtime = client.runtimeSnapshot();
  runtime.posters[0].locked = true;

  const saving = client.saveRuntimeState(runtime);
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(client.localMutationStatus(), "pending");
  assert.equal(client.runtimeSnapshot().posters[0].locked, true);

  const stored = clone(replaceArgs.snapshot);
  stored.revision = formal.revision + 1;
  releaseReplace({
    status: "stored",
    previousRevision: formal.revision,
    revision: stored.revision,
    snapshot: stored
  });
  const result = await saving;

  assert.equal(result.status, "stored");
  assert.equal(client.localMutationStatus(), "none");
  assert.equal(client.snapshot.revision, stored.revision);
  assert.equal(client.runtimeSnapshot().posters[0].locked, true);
});

test("native write errors reject safely without replacing the accepted snapshot", async () => {
  const formal = createDefaultPosterSnapshot({
    title: "PRESERVE ON ERROR",
    primaryWorkArea: PRIMARY_WORK_AREA
  });
  const native = fakeNative({ initialState: formal });
  const invoke = async (command, args) => {
    if (command === POSTER_STATE_REPLACE_COMMAND) {
      throw {
        code: "write_database_failed",
        message: "injected database write failure"
      };
    }
    return native.invoke(command, args);
  };
  const client = new PosterStateClient({ invoke, storage: memoryStorage() });
  assert.equal((await client.bootstrap()).status, "ready");
  const acceptedBefore = clone(client.snapshot);
  const runtime = client.runtimeSnapshot();
  runtime.posters[0].locked = true;

  const result = await client.saveRuntimeState(runtime);

  assert.equal(result.status, "rejected");
  assert.equal(result.code, "write_database_failed");
  assert.equal(result.message, "injected database write failure");
  assert.deepEqual(client.snapshot, acceptedBefore);
  assert.equal(client.localMutationStatus(), "retained");
  assert.equal(client.runtimeSnapshot().posters[0].locked, true);

  const external = clone(acceptedBefore);
  external.revision += 1;
  external.posters[0].collapsedState = "collapsed";
  assert.equal(client.acceptSnapshot(external), true);
  const merged = client.runtimeSnapshot().posters[0];
  assert.equal(merged.locked, true, "failed local lock remains visible");
  assert.equal(merged.collapsed, true, "new authoritative fields still merge in");

  const stale = clone(acceptedBefore);
  assert.equal(client.acceptSnapshot(stale), false, "older revisions must be ignored");
  const conflictingSameRevision = clone(external);
  conflictingSameRevision.posters[0].collapsedState = "expanded";
  assert.equal(client.acceptSnapshot(conflictingSameRevision), false);
  assert.equal(client.runtimeSnapshot().posters[0].collapsed, true);
});

test("formal/runtime projection round-trip preserves geometry, lock and collapse contract", () => {
  const formal = createDefaultPosterSnapshot({
    title: "ROUND TRIP",
    primaryWorkArea: PRIMARY_WORK_AREA
  });
  const runtime = projectPosterStateToRuntime(formal, { primaryWorkArea: PRIMARY_WORK_AREA });
  runtime.posters[0].x = 400;
  runtime.posters[0].y = 20;
  runtime.posters[0].locked = true;
  runtime.posters[0].collapsed = true;
  const projected = projectRuntimeToPosterState(formal, runtime, {
    primaryWorkArea: PRIMARY_WORK_AREA
  });

  assert.equal(projected.ok, true);
  assert.equal(projected.snapshot.posters[0].isLocked, true);
  assert.equal(projected.snapshot.posters[0].collapsedState, "collapsed");
  assert.equal(projected.snapshot.posters[0].normalizedRect.x > 0, true);
  assert.equal(projected.snapshot.posters[0].normalizedRect.y > 0, true);
});
