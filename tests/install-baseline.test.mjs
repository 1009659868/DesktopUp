import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const baseline = readFileSync("src-tauri/src/install_baseline.rs", "utf8");
const installPaths = readFileSync("src-tauri/src/install_paths.rs", "utf8");
const nativeRuntime = readFileSync("src-tauri/src/lib.rs", "utf8");
const cargoManifest = readFileSync("src-tauri/Cargo.toml", "utf8");
const cargoConfig = readFileSync(".cargo/config.toml", "utf8");
const nsisTemplate = readFileSync("src-tauri/nsis/installer.nsi", "utf8");
const config = JSON.parse(readFileSync("src-tauri/tauri.conf.json", "utf8"));

test("C-IBC-01 freezes a current-user non-elevated installer baseline", () => {
  assert.equal(config.identifier, "com.allmight.desktopup");
  assert.equal(config.productName, "DesktopUp");
  assert.equal(config.mainBinaryName, "DesktopUp");
  assert.equal(config.bundle.windows.nsis.installMode, "currentUser");
  assert.match(cargoConfig, /STATIC_VCRUNTIME = "true"/);
  assert.equal(config.bundle.windows.webviewInstallMode.type, "downloadBootstrapper");
  assert.match(baseline, /install_scope: "currentUser"/);
  assert.match(baseline, /elevation_required: false/);
  assert.match(baseline, /mutable_install_writes_allowed: false/);
  assert.match(baseline, /distribution_scope: "privateUse"/);
  assert.match(baseline, /license_payload_required: false/);
  assert.deepEqual(config.bundle.resources, ["resources/*"]);
  assert.equal(config.bundle.windows.nsis.template, "nsis/installer.nsi");
});

test("target local data stays distinct from current roaming authority until C-UGM", () => {
  assert.match(baseline, /TARGET_DATA_ROOT: &str = r"%LOCALAPPDATA%\\DesktopUp"/);
  assert.match(
    baseline,
    /CURRENT_RUNTIME_ROOT: &str = r"%APPDATA%\\com\.allmight\.desktopup"/
  );
  assert.match(baseline, /data_migration_owner: "C-UGM"/);
  assert.match(baseline, /eager_data_move_allowed: false/);
  assert.match(baseline, /current_runtime_root_writable_until_migration: true/);
  assert.match(installPaths, /app_config_dir\(\)/);
  assert.match(nativeRuntime, /runtime_paths\.poster_state_path\(\)/);
  assert.doesNotMatch(installPaths, /app_local_data_dir\(\)/);
});

test("autostart remains opt-in and owned by C-RLC", () => {
  assert.match(baseline, /autostart_default_enabled: false/);
  assert.match(baseline, /autostart_explicit_opt_in_required: true/);
  assert.match(baseline, /autostart_implementation_owner: "C-RLC"/);
  assert.match(
    nativeRuntime,
    /with_id\("autostart", "开机自启（A1 接入）"\)[\s\S]*?\.enabled\(false\)/
  );
});

test("runtime dependencies are explicit and cannot use WebView2 skip mode", () => {
  assert.match(cargoManifest, /rusqlite = \{ version = "[^"]+", features = \["bundled"\] \}/);
  assert.match(baseline, /webview2_runtime: "evergreen"/);
  assert.match(baseline, /webview2_install_mode: "downloadBootstrapper"/);
  assert.match(baseline, /static_vc_runtime: true/);
  assert.match(baseline, /bundled_sqlite: true/);
  assert.doesNotMatch(JSON.stringify(config.bundle.windows.webviewInstallMode), /skip/);
});

test("project NSIS template freezes install root and exact autostart cleanup", () => {
  assert.match(
    nsisTemplate,
    /StrCpy \$INSTDIR "\$LOCALAPPDATA\\Programs\\\$\{PRODUCTNAME\}"/,
  );
  assert.match(
    nsisTemplate,
    /DeleteRegValue HKCU "Software\\Microsoft\\Windows\\CurrentVersion\\Run" "DesktopUp"/,
  );
  assert.doesNotMatch(
    nsisTemplate,
    /DeleteRegValue HKCU "Software\\Microsoft\\Windows\\CurrentVersion\\Run" "\$\{PRODUCTNAME\}"/,
  );
});
