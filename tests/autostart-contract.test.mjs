import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const contract = readFileSync(
  "src-tauri/src/autostart_contract.rs",
  "utf8",
);
const nativeRuntime = readFileSync("src-tauri/src/lib.rs", "utf8");
const cargoManifest = readFileSync("src-tauri/Cargo.toml", "utf8");
const controlCenter = readFileSync("app/src/main.js", "utf8");
const controlCenterHtml = readFileSync("app/index.html", "utf8");

test("C-IBC-04 freezes one exact current-user opt-in registration", () => {
  assert.match(contract, /AUTOSTART_CONTRACT_NAME: &str = "autostart-contract-v1"/);
  assert.match(contract, /AUTOSTART_REGISTRY_HIVE: &str = "HKEY_CURRENT_USER"/);
  assert.match(
    contract,
    /Software\\Microsoft\\Windows\\CurrentVersion\\Run/,
  );
  assert.match(contract, /AUTOSTART_REGISTRY_VALUE_NAME: &str = "DesktopUp"/);
  assert.match(contract, /AUTOSTART_REGISTRY_VALUE_TYPE: &str = "REG_SZ"/);
  assert.match(contract, /default_enabled: false/);
  assert.match(contract, /explicit_opt_in_required: true/);
  assert.match(contract, /registration_requires_target_installed: true/);
  assert.match(contract, /registration_requires_exact_command: true/);
});

test("only a formal DesktopUp.exe and strict autostart argument are eligible", () => {
  assert.match(contract, /EXPECTED_EXECUTABLE_NAME: &str = "DesktopUp\.exe"/);
  assert.match(contract, /AUTOSTART_ARGUMENT: &str = "--autostart"/);
  assert.match(contract, /registration_requires_regular_file: true/);
  assert.match(contract, /registration_rejects_symlink: true/);
  assert.match(contract, /additional_arguments_allowed: false/);
  assert.match(contract, /startPosterWithoutControlCenter/);
  assert.match(contract, /keepExistingInstanceWithoutUiChange/);
  assert.match(contract, /current_runtime_integration: "authoritativeCrlc03"/);
});

test("temporary G drive eligibility is compile-time scoped and cannot change the installer root", () => {
  assert.match(cargoManifest, /\[features\][\s\S]*?default = \[\][\s\S]*?autostart-test-root = \[\]/);
  assert.match(
    contract,
    /#\[cfg\(feature = "autostart-test-root"\)\][\s\S]*?TEMPORARY_AUTOSTART_TEST_EXECUTABLE: &str = r"G:\\A_\\Tools\\Test\\DesktopUp\.exe"/,
  );
  assert.match(contract, /eligibility_scope[\s\S]*?"temporaryTest"/);
  assert.match(contract, /paths\.install_root\(\)\.join\(EXPECTED_EXECUTABLE_NAME\)/);
});

test("C-RLC-03 writes only the exact owned HKCU value after the formal path gate", () => {
  const productionSource = contract.split("#[cfg(test)]", 1)[0];
  assert.match(cargoManifest, /"Win32_System_Registry"/);
  assert.match(productionSource, /RegOpenKeyExW/);
  assert.match(productionSource, /RegQueryValueExW/);
  assert.match(productionSource, /RegCreateKeyExW/);
  assert.match(productionSource, /RegSetValueExW/);
  assert.match(productionSource, /RegDeleteValueW/);
  assert.match(productionSource, /if !before\.registration_eligible/);
  assert.match(productionSource, /backend\.write_exact\(&before\.expected_command\)/);
  assert.match(productionSource, /backend\.delete_owned\(\)/);
  assert.doesNotMatch(productionSource, /HKEY_LOCAL_MACHINE|TaskScheduler|StartupFolder/);
});

test("cleanup owns only the exact value and remains mandatory after recovery failure", () => {
  assert.match(contract, /remove_owned_value_only: true/);
  assert.match(contract, /remove_parent_key_allowed: false/);
  assert.match(contract, /owner: "C-URV"\.to_string\(\)/);
  assert.match(contract, /required_on_uninstall: true/);
  assert.match(contract, /required_on_recovery_failure: true/);
  assert.match(contract, /required_after_partial_restore_failure: true/);
  assert.match(contract, /missing_value_is_success: true/);
  assert.match(contract, /requires_elevation: false/);
});

test("tray and control center consume one read-back snapshot without optimistic state", () => {
  assert.match(nativeRuntime, /fn autostart_diagnostics\(/);
  assert.match(nativeRuntime, /fn set_autostart_enabled\(/);
  assert.match(nativeRuntime, /"autostart" => toggle_autostart\(app\)/);
  assert.match(nativeRuntime, /tray\.sync_autostart\(&snapshot\)/);
  assert.match(
    nativeRuntime,
    /generate_handler!\[[\s\S]*?autostart_diagnostics,[\s\S]*?set_autostart_enabled,/,
  );
  assert.match(controlCenter, /invoke\("set_autostart_enabled", \{ enabled \}\)/);
  assert.match(controlCenter, /applyAutostartSnapshot\(result\?\.after/);
  assert.match(controlCenter, /await listen\(AUTOSTART_STATE_EVENT/);
  assert.match(controlCenterHtml, /id="autostart-control" type="checkbox" disabled/);
});
