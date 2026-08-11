import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import {
  DEFAULT_TEMPLATE_KEY,
  INTERNAL_FALLBACK_TEMPLATE_KEY,
  POSTER_TEMPLATE_RESOLUTION_CONTRACT_VERSION,
  resolvePosterTemplateSelection
} from "../app/src/poster-template-resolution.js";

const RESOLUTION_MATRIX = JSON.parse(readFileSync(
  new URL("./fixtures/poster-template-resolution-v1.json", import.meta.url),
  "utf8"
));

function decision(value) {
  return {
    renderTemplateKey: value.renderTemplateKey,
    selection: value.selection,
    reason: value.reason,
    fallbackFailureReason: value.fallbackFailureReason
  };
}

test("JS matches the shared poster-template-resolution-v1 failure matrix", () => {
  for (const sample of RESOLUTION_MATRIX) {
    const result = resolvePosterTemplateSelection(sample.input);
    assert.equal(result.ok, sample.valid, sample.name);
    if (!sample.valid) {
      assert.equal(result.errors.some((error) => error.code === sample.errorCode), true, sample.name);
      continue;
    }
    assert.equal(result.value.contractVersion, POSTER_TEMPLATE_RESOLUTION_CONTRACT_VERSION, sample.name);
    assert.deepEqual(decision(result.value), sample.expected, sample.name);
    assert.equal(result.value.preserveRequestedTemplateKey, true, sample.name);
    assert.equal(result.value.persistenceMutation, "none", sample.name);
  }
});

test("transparent default is a normal selection and not the internal fallback", () => {
  assert.notEqual(DEFAULT_TEMPLATE_KEY, INTERNAL_FALLBACK_TEMPLATE_KEY);
  const result = resolvePosterTemplateSelection({
    snapshotStatus: "valid",
    requestedTemplateKey: DEFAULT_TEMPLATE_KEY,
    requestedTemplateStatus: "available",
    fallbackTemplateStatus: "available"
  });
  assert.equal(result.ok, true);
  assert.equal(result.value.selection, "requested");
  assert.equal(result.value.renderTemplateKey, DEFAULT_TEMPLATE_KEY);
  assert.equal(result.value.reason, null);
});

test("fallback rendering never rewrites or replaces the requested template key", () => {
  const input = {
    snapshotStatus: "valid",
    requestedTemplateKey: "missing-user-template",
    requestedTemplateStatus: "missing",
    fallbackTemplateStatus: "available"
  };
  const before = structuredClone(input);
  const result = resolvePosterTemplateSelection(input);

  assert.equal(result.ok, true);
  assert.equal(result.value.selection, "minimalFallback");
  assert.equal(result.value.requestedTemplateKey, "missing-user-template");
  assert.equal(result.value.renderTemplateKey, INTERNAL_FALLBACK_TEMPLATE_KEY);
  assert.equal(result.value.persistenceMutation, "none");
  assert.deepEqual(input, before);
});

test("invalid snapshots and recovery failures remain diagnosable instead of resetting to default", () => {
  for (const [snapshotStatus, reason] of [
    ["invalid", "snapshot_invalid"],
    ["recoveryFailed", "snapshot_recovery_failed"]
  ]) {
    const result = resolvePosterTemplateSelection({
      snapshotStatus,
      requestedTemplateKey: null,
      requestedTemplateStatus: "unknown",
      fallbackTemplateStatus: "available"
    });
    assert.equal(result.ok, true);
    assert.equal(result.value.requestedTemplateKey, null);
    assert.equal(result.value.renderTemplateKey, INTERNAL_FALLBACK_TEMPLATE_KEY);
    assert.equal(result.value.reason, reason);
    assert.notEqual(result.value.renderTemplateKey, DEFAULT_TEMPLATE_KEY);
  }
});

test("fallback failure reports unavailable rendering instead of pretending to recover", () => {
  const result = resolvePosterTemplateSelection({
    snapshotStatus: "valid",
    requestedTemplateKey: "missing-user-template",
    requestedTemplateStatus: "missing",
    fallbackTemplateStatus: "invalid"
  });
  assert.equal(result.ok, true);
  assert.equal(result.value.selection, "unavailable");
  assert.equal(result.value.renderTemplateKey, null);
  assert.equal(result.value.reason, "requested_template_missing");
  assert.equal(result.value.fallbackFailureReason, "minimal_fallback_invalid");
});
