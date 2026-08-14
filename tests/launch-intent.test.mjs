import assert from "node:assert/strict";
import fs from "node:fs";
import test from "node:test";

const launchIntent = fs.readFileSync(
  new URL("../src-tauri/src/launch_intent.rs", import.meta.url),
  "utf8"
);
const nativeRuntime = fs.readFileSync(
  new URL("../src-tauri/src/lib.rs", import.meta.url),
  "utf8"
);
const performanceGate = fs.readFileSync(
  new URL("../src-tauri/src/performance_gate.rs", import.meta.url),
  "utf8"
);

test("C-RLC-02 defines one versioned launch intent decision table", () => {
  assert.match(launchIntent, /LAUNCH_INTENT_CONTRACT_VERSION: u32 = 1/);
  assert.match(launchIntent, /LAUNCH_INTENT_CONTRACT_NAME: &str = "launch-intent-v1"/);
  assert.match(launchIntent, /Interactive/);
  assert.match(launchIntent, /Autostart/);
  assert.match(launchIntent, /PerformanceGate/);
  assert.match(launchIntent, /Invalid/);
  assert.match(launchIntent, /unsupportedArguments/);
});

test("recovery remains before launch rejection and Tauri initialization", () => {
  const recovery = nativeRuntime.indexOf("poster_state_recovery_gate::run_if_requested");
  const launch = nativeRuntime.indexOf("let initial_launch_intent = {");
  const tauri = nativeRuntime.indexOf("let application = tauri::Builder::default()");
  assert.ok(recovery >= 0);
  assert.ok(launch > recovery);
  assert.match(nativeRuntime.slice(launch, tauri), /decide_initial_from_env\(\)/);
  assert.ok(tauri > launch);
});

test("secondary instance routing opens the control center only for interactive intent", () => {
  const callback = nativeRuntime.match(
    /tauri_plugin_single_instance::init\(\|app, args, _cwd\|[\s\S]*?\n        \}\)\)/
  );
  assert.ok(callback, "single-instance callback must exist");
  assert.match(callback[0], /decide_secondary_forwarded_arguments\(&args\)/);
  assert.match(
    callback[0],
    /LaunchIntentDisposition::ShowControlCenter => show_main_window\(app\)/
  );
  assert.match(callback[0], /LaunchIntentDisposition::KeepExistingUi => \{\}/);
  assert.match(callback[0], /#\[cfg\(feature = "autostart-test-root"\)\]/);
  assert.match(callback[0], /autostart_test_gate::try_handle_forwarded\(app, &args\)/);
  assert.ok(callback[0].indexOf("try_handle_forwarded") < callback[0].indexOf("decide_secondary_forwarded_arguments"));
  assert.doesNotMatch(callback[0], /_args/);
});

test("performance gate keeps one internal diagnostic argument authority", () => {
  assert.match(
    performanceGate,
    /PERFORMANCE_GATE_ARGUMENT: &str = "--cdrp-performance-gate"/
  );
  assert.match(nativeRuntime, /argument == PERFORMANCE_GATE_ARGUMENT/);
  assert.match(launchIntent, /performance_gate::PERFORMANCE_GATE_ARGUMENT/);
});

test("launch intent diagnostics are managed and exposed read-only", () => {
  assert.match(nativeRuntime, /manage\(LaunchIntentCoordinator::new\(initial_launch_intent\)\)/);
  assert.match(nativeRuntime, /fn launch_intent_diagnostics\(/);
  assert.match(
    nativeRuntime,
    /generate_handler!\[[\s\S]*?launch_intent_diagnostics,/
  );
  assert.doesNotMatch(launchIntent, /RegSetValue|RegDeleteValue|fs::write|File::create/);
});
