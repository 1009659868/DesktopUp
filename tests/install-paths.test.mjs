import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const installPaths = readFileSync("src-tauri/src/install_paths.rs", "utf8");
const nativeRuntime = readFileSync("src-tauri/src/lib.rs", "utf8");

test("C-IBC-02 resolves one current authority and an inactive target root", () => {
  assert.match(installPaths, /local_data_dir\(\)/);
  assert.match(installPaths, /app_config_dir\(\)/);
  assert.match(installPaths, /status: "preMigrationAuthoritative"\.to_string\(\)/);
  assert.match(installPaths, /target_data_root_active: false/);
  assert.match(installPaths, /owner: baseline\.data_migration_owner\.to_string\(\)/);
});

test("path resolution is structurally read-only", () => {
  assert.match(installPaths, /creates_directories: false/);
  assert.match(installPaths, /writes_files: false/);
  assert.match(installPaths, /mutates_authority: false/);
  assert.doesNotMatch(
    installPaths,
    /create_dir|create_new|File::create|fs::write|fs::copy|fs::rename|remove_dir|remove_file/
  );
});

test("setup consumes the resolver while SQLite remains on the current root", () => {
  const setup = nativeRuntime.slice(
    nativeRuntime.indexOf(".setup(|app|"),
    nativeRuntime.indexOf("if let Some(state)", nativeRuntime.indexOf(".setup(|app|"))
  );
  assert.match(setup, /install_paths::resolve_for_app\(app\.handle\(\)\)/);
  assert.match(setup, /runtime_paths\.placement_path\(\)/);
  assert.match(setup, /runtime_paths\.snapshot_bridge_path\(\)/);
  assert.match(setup, /runtime_paths\.poster_state_path\(\)/);
  assert.doesNotMatch(setup, /app_local_data_dir\(\)/);
});

test("versioned diagnostics are registered as a read-only Tauri command", () => {
  assert.match(installPaths, /DIAGNOSTIC_NAME: &str = "install-path-diagnostics-v1"/);
  assert.match(
    nativeRuntime,
    /fn install_path_diagnostics\([\s\S]*?paths\.diagnostics\(\)\.clone\(\)/
  );
  assert.match(
    nativeRuntime,
    /tauri::generate_handler!\[[\s\S]*?install_path_diagnostics/
  );
});
