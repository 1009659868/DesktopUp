import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const nativeRuntime = readFileSync("src-tauri/src/lib.rs", "utf8");
const recoveryRuntime = readFileSync(
  "src-tauri/src/poster_state_recovery_gate.rs",
  "utf8"
);
const recoveryRunner = readFileSync(
  "scripts/run-poster-state-recovery-gate.mjs",
  "utf8"
);

test("Release recovery probe exits before Tauri and single-instance initialization", () => {
  const recoveryBranch = nativeRuntime.indexOf(
    "poster_state_recovery_gate::run_if_requested()"
  );
  const tauriBuilder = nativeRuntime.indexOf("tauri::Builder::default()");
  const singleInstance = nativeRuntime.indexOf("tauri_plugin_single_instance::init");

  assert.ok(recoveryBranch >= 0, "Release recovery probe entry is missing");
  assert.ok(
    recoveryBranch < tauriBuilder,
    "recovery probe must not initialize the normal Tauri application"
  );
  assert.ok(
    recoveryBranch < singleInstance,
    "recovery probe must not join or affect the normal single-instance process"
  );
  assert.match(recoveryRuntime, /--cpss-recovery-gate-report=/);
  assert.match(recoveryRuntime, /poster-state-recovery-gate-v1/);
});

test("recovery matrix seals every C-PSS-05 isolated failure family", () => {
  for (const scenario of [
    "restart-revision-and-schema-bootstrap",
    "damaged-content-rejections",
    "schema-and-database-identity-rejections",
    "unreadable-and-query-failures",
    "cas-conflict-and-validation-preserve",
    "transactional-write-failure-rollback",
    "legacy-backup-and-idempotence",
    "legacy-failure-source-retention"
  ]) {
    assert.match(recoveryRuntime, new RegExp(scenario));
    assert.match(recoveryRunner, new RegExp(scenario));
  }

  assert.match(recoveryRuntime, /PosterStateRepository::open/);
  assert.match(recoveryRuntime, /replace_with_expected_revision/);
  assert.match(recoveryRuntime, /migrate_legacy/);
  assert.match(recoveryRuntime, /CREATE TRIGGER reject_recovery_update/);
  assert.match(recoveryRuntime, /CREATE TRIGGER reject_recovery_migration/);
  assert.match(recoveryRuntime, /write_report_atomically/);
});

test("Release runner owns and safely cleans only its OS-temporary workspace", () => {
  assert.match(recoveryRunner, /mkdtempSync\(join\(temporaryBase, TEMPORARY_PREFIX\)\)/);
  assert.match(recoveryRunner, /APPDATA: isolatedAppData/);
  assert.match(recoveryRunner, /LOCALAPPDATA: isolatedLocalAppData/);
  assert.match(recoveryRunner, /assertSafeTemporaryRoot\(temporaryRoot\)/);
  assert.match(recoveryRunner, /dirname\(resolvedPath\) !== temporaryBase/);
  assert.match(recoveryRunner, /rmSync\(temporaryRoot, \{ recursive: true, force: true \}\)/);
  assert.doesNotMatch(recoveryRunner, /com\.allmight\.desktopup/);
});
