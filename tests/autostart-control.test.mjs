import assert from "node:assert/strict";
import test from "node:test";

import {
  AUTOSTART_STATE_EVENT,
  autostartOutcomeMessage,
  deriveAutostartControl
} from "../app/src/autostart-control.js";

test("development and portable runtimes expose a disabled authoritative control", () => {
  const control = deriveAutostartControl({
    registrationEligible: false,
    registrationState: "absent",
    effectiveEnabled: false
  });
  assert.equal(control.checked, false);
  assert.equal(control.disabled, true);
  assert.equal(control.state, "ineligible");
});

test("matching and absent snapshots directly drive the checked state", () => {
  assert.deepEqual(
    deriveAutostartControl({
      registrationEligible: true,
      registrationState: "matching",
      effectiveEnabled: true
    }),
    {
      checked: true,
      disabled: false,
      status: "已启用并通过读回确认",
      state: "matching"
    }
  );
  assert.equal(
    deriveAutostartControl({
      registrationEligible: true,
      registrationState: "absent",
      effectiveEnabled: false
    }).checked,
    false
  );
});

test("pending and unreadable states cannot be changed optimistically", () => {
  const snapshot = {
    registrationEligible: true,
    registrationState: "unreadable",
    effectiveEnabled: false
  };
  assert.equal(deriveAutostartControl(snapshot).disabled, true);
  const pending = deriveAutostartControl(
    { ...snapshot, registrationState: "matching", effectiveEnabled: true },
    true
  );
  assert.equal(pending.checked, true);
  assert.equal(pending.disabled, true);
  assert.equal(pending.state, "pending");
});

test("change outcomes and event name are stable for both UI consumers", () => {
  assert.equal(AUTOSTART_STATE_EVENT, "autostart-state-changed");
  assert.match(
    autostartOutcomeMessage({ succeeded: true, outcome: "repaired" }),
    /修复/
  );
  assert.match(
    autostartOutcomeMessage({ succeeded: false, outcome: "verificationFailed" }),
    /读回结果/
  );
});
