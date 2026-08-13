import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const contract = readFileSync(
  "src-tauri/src/autostart_contract.rs",
  "utf8",
);
const nativeRuntime = readFileSync("src-tauri/src/lib.rs", "utf8");
const cargoManifest = readFileSync("src-tauri/Cargo.toml", "utf8");

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
  assert.match(contract, /current_runtime_integration: "contractOnlyCrlcPending"/);
});

test("diagnostics read HKCU without any registration mutation API", () => {
  const productionSource = contract.split("#[cfg(test)]", 1)[0];
  assert.match(cargoManifest, /"Win32_System_Registry"/);
  assert.match(productionSource, /RegOpenKeyExW/);
  assert.match(productionSource, /RegQueryValueExW/);
  assert.match(productionSource, /RegCloseKey/);
  assert.doesNotMatch(
    productionSource,
    /Reg(?:Create|Set|Delete|Replace|Restore|Save|Load)|set_value|delete_value/,
  );
  assert.match(productionSource, /writes_registry: false/);
  assert.match(productionSource, /changes_autostart: false/);
  assert.match(productionSource, /launches_processes: false/);
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

test("read-only diagnostics are registered while the autostart UI stays disabled", () => {
  assert.match(nativeRuntime, /fn autostart_diagnostics\(/);
  assert.match(
    nativeRuntime,
    /generate_handler!\[[\s\S]*?autostart_diagnostics,/,
  );
  assert.match(
    nativeRuntime,
    /with_id\("autostart", "开机自启（A1 接入）"\)[\s\S]*?\.enabled\(false\)/,
  );
  assert.doesNotMatch(
    nativeRuntime,
    /"autostart"\s*=>|set_autostart|enable_autostart/,
  );
});
