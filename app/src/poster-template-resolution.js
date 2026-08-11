export const POSTER_TEMPLATE_RESOLUTION_CONTRACT_VERSION = 1;
export const DEFAULT_TEMPLATE_KEY = "transparent-default";
export const INTERNAL_FALLBACK_TEMPLATE_KEY = "minimal-fallback";

export const POSTER_SNAPSHOT_LOAD_STATES = Object.freeze(["valid", "invalid", "recoveryFailed"]);
export const REQUESTED_TEMPLATE_STATES = Object.freeze(["available", "missing", "invalid", "unknown"]);
export const FALLBACK_TEMPLATE_STATES = Object.freeze(["available", "missing", "invalid"]);

function resolutionError(path, code, message) {
  return { path, code, message };
}

function requestedFailureReason(requestedTemplateKey, requestedTemplateStatus) {
  const prefix = requestedTemplateKey === DEFAULT_TEMPLATE_KEY ? "default_template" : "requested_template";
  return `${prefix}_${requestedTemplateStatus}`;
}

function fallbackFailureReason(fallbackTemplateStatus) {
  return fallbackTemplateStatus === "available"
    ? null
    : `minimal_fallback_${fallbackTemplateStatus}`;
}

export function resolvePosterTemplateSelection(input) {
  const errors = [];
  const snapshotStatus = input?.snapshotStatus;
  const requestedTemplateKey = input?.requestedTemplateKey ?? null;
  const requestedTemplateStatus = input?.requestedTemplateStatus;
  const fallbackTemplateStatus = input?.fallbackTemplateStatus;

  if (!POSTER_SNAPSHOT_LOAD_STATES.includes(snapshotStatus)) {
    errors.push(resolutionError(
      "$.snapshotStatus",
      "invalid_snapshot_status",
      "snapshotStatus must be valid, invalid, or recoveryFailed."
    ));
  }
  if (requestedTemplateKey !== null && typeof requestedTemplateKey !== "string") {
    errors.push(resolutionError(
      "$.requestedTemplateKey",
      "invalid_requested_template_key",
      "requestedTemplateKey must be a string or null."
    ));
  }
  if (snapshotStatus === "valid"
      && (typeof requestedTemplateKey !== "string" || !requestedTemplateKey.trim())) {
    errors.push(resolutionError(
      "$.requestedTemplateKey",
      "requested_template_key_required",
      "A valid snapshot must provide a non-empty requestedTemplateKey."
    ));
  }
  if (!REQUESTED_TEMPLATE_STATES.includes(requestedTemplateStatus)) {
    errors.push(resolutionError(
      "$.requestedTemplateStatus",
      "invalid_requested_template_status",
      "requestedTemplateStatus must be available, missing, invalid, or unknown."
    ));
  }
  if (!FALLBACK_TEMPLATE_STATES.includes(fallbackTemplateStatus)) {
    errors.push(resolutionError(
      "$.fallbackTemplateStatus",
      "invalid_fallback_template_status",
      "fallbackTemplateStatus must be available, missing, or invalid."
    ));
  }
  if (errors.length > 0) return { ok: false, value: null, errors };

  let reason = null;
  if (snapshotStatus === "invalid") {
    reason = "snapshot_invalid";
  } else if (snapshotStatus === "recoveryFailed") {
    reason = "snapshot_recovery_failed";
  } else if (requestedTemplateKey === INTERNAL_FALLBACK_TEMPLATE_KEY) {
    reason = "fallback_cannot_be_selected";
  } else if (requestedTemplateStatus !== "available") {
    reason = requestedFailureReason(requestedTemplateKey, requestedTemplateStatus);
  }

  if (reason === null) {
    return {
      ok: true,
      errors: [],
      value: {
        contractVersion: POSTER_TEMPLATE_RESOLUTION_CONTRACT_VERSION,
        requestedTemplateKey,
        renderTemplateKey: requestedTemplateKey,
        selection: "requested",
        reason: null,
        fallbackFailureReason: null,
        preserveRequestedTemplateKey: true,
        persistenceMutation: "none"
      }
    };
  }

  const fallbackFailure = fallbackFailureReason(fallbackTemplateStatus);
  return {
    ok: true,
    errors: [],
    value: {
      contractVersion: POSTER_TEMPLATE_RESOLUTION_CONTRACT_VERSION,
      requestedTemplateKey,
      renderTemplateKey: fallbackFailure === null ? INTERNAL_FALLBACK_TEMPLATE_KEY : null,
      selection: fallbackFailure === null ? "minimalFallback" : "unavailable",
      reason,
      fallbackFailureReason: fallbackFailure,
      preserveRequestedTemplateKey: true,
      persistenceMutation: "none"
    }
  };
}
