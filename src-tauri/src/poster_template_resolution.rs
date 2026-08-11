use serde::Serialize;
use std::fmt;

pub const POSTER_TEMPLATE_RESOLUTION_CONTRACT_VERSION: u32 = 1;
pub const DEFAULT_TEMPLATE_KEY: &str = "transparent-default";
pub const INTERNAL_FALLBACK_TEMPLATE_KEY: &str = "minimal-fallback";

const SNAPSHOT_STATES: &[&str] = &["valid", "invalid", "recoveryFailed"];
const REQUESTED_TEMPLATE_STATES: &[&str] = &["available", "missing", "invalid", "unknown"];
const FALLBACK_TEMPLATE_STATES: &[&str] = &["available", "missing", "invalid"];

#[derive(Clone, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PosterTemplateResolution {
    pub contract_version: u32,
    pub requested_template_key: Option<String>,
    pub render_template_key: Option<String>,
    pub selection: &'static str,
    pub reason: Option<&'static str>,
    pub fallback_failure_reason: Option<&'static str>,
    pub preserve_requested_template_key: bool,
    pub persistence_mutation: &'static str,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PosterTemplateResolutionError {
    pub path: &'static str,
    pub code: &'static str,
    pub message: &'static str,
}

impl PosterTemplateResolutionError {
    fn new(path: &'static str, code: &'static str, message: &'static str) -> Self {
        Self {
            path,
            code,
            message,
        }
    }
}

impl fmt::Display for PosterTemplateResolutionError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "{} [{}]: {}", self.path, self.code, self.message)
    }
}

fn requested_failure_reason(
    requested_template_key: Option<&str>,
    requested_template_status: &str,
) -> &'static str {
    match (
        requested_template_key == Some(DEFAULT_TEMPLATE_KEY),
        requested_template_status,
    ) {
        (true, "missing") => "default_template_missing",
        (true, "invalid") => "default_template_invalid",
        (true, "unknown") => "default_template_unknown",
        (false, "missing") => "requested_template_missing",
        (false, "invalid") => "requested_template_invalid",
        (false, "unknown") => "requested_template_unknown",
        _ => unreachable!("requested failure requires a non-available validated status"),
    }
}

fn fallback_failure_reason(fallback_template_status: &str) -> Option<&'static str> {
    match fallback_template_status {
        "available" => None,
        "missing" => Some("minimal_fallback_missing"),
        "invalid" => Some("minimal_fallback_invalid"),
        _ => unreachable!("fallback status is validated before resolution"),
    }
}

pub fn resolve_poster_template_selection(
    snapshot_status: &str,
    requested_template_key: Option<&str>,
    requested_template_status: &str,
    fallback_template_status: &str,
) -> Result<PosterTemplateResolution, Vec<PosterTemplateResolutionError>> {
    let mut errors = Vec::new();
    if !SNAPSHOT_STATES.contains(&snapshot_status) {
        errors.push(PosterTemplateResolutionError::new(
            "$.snapshotStatus",
            "invalid_snapshot_status",
            "snapshotStatus must be valid, invalid, or recoveryFailed.",
        ));
    }
    if snapshot_status == "valid"
        && requested_template_key
            .map(str::trim)
            .filter(|key| !key.is_empty())
            .is_none()
    {
        errors.push(PosterTemplateResolutionError::new(
            "$.requestedTemplateKey",
            "requested_template_key_required",
            "A valid snapshot must provide a non-empty requestedTemplateKey.",
        ));
    }
    if !REQUESTED_TEMPLATE_STATES.contains(&requested_template_status) {
        errors.push(PosterTemplateResolutionError::new(
            "$.requestedTemplateStatus",
            "invalid_requested_template_status",
            "requestedTemplateStatus must be available, missing, invalid, or unknown.",
        ));
    }
    if !FALLBACK_TEMPLATE_STATES.contains(&fallback_template_status) {
        errors.push(PosterTemplateResolutionError::new(
            "$.fallbackTemplateStatus",
            "invalid_fallback_template_status",
            "fallbackTemplateStatus must be available, missing, or invalid.",
        ));
    }
    if !errors.is_empty() {
        return Err(errors);
    }

    let reason = if snapshot_status == "invalid" {
        Some("snapshot_invalid")
    } else if snapshot_status == "recoveryFailed" {
        Some("snapshot_recovery_failed")
    } else if requested_template_key == Some(INTERNAL_FALLBACK_TEMPLATE_KEY) {
        Some("fallback_cannot_be_selected")
    } else if requested_template_status != "available" {
        Some(requested_failure_reason(
            requested_template_key,
            requested_template_status,
        ))
    } else {
        None
    };

    if reason.is_none() {
        return Ok(PosterTemplateResolution {
            contract_version: POSTER_TEMPLATE_RESOLUTION_CONTRACT_VERSION,
            requested_template_key: requested_template_key.map(str::to_string),
            render_template_key: requested_template_key.map(str::to_string),
            selection: "requested",
            reason: None,
            fallback_failure_reason: None,
            preserve_requested_template_key: true,
            persistence_mutation: "none",
        });
    }

    let fallback_failure_reason = fallback_failure_reason(fallback_template_status);
    Ok(PosterTemplateResolution {
        contract_version: POSTER_TEMPLATE_RESOLUTION_CONTRACT_VERSION,
        requested_template_key: requested_template_key.map(str::to_string),
        render_template_key: fallback_failure_reason
            .is_none()
            .then(|| INTERNAL_FALLBACK_TEMPLATE_KEY.to_string()),
        selection: if fallback_failure_reason.is_none() {
            "minimalFallback"
        } else {
            "unavailable"
        },
        reason,
        fallback_failure_reason,
        preserve_requested_template_key: true,
        persistence_mutation: "none",
    })
}

#[cfg(test)]
mod tests {
    use super::{
        resolve_poster_template_selection, DEFAULT_TEMPLATE_KEY, INTERNAL_FALLBACK_TEMPLATE_KEY,
    };
    use serde_json::Value;

    #[test]
    fn normal_default_and_internal_fallback_are_distinct() {
        assert_eq!(DEFAULT_TEMPLATE_KEY, "transparent-default");
        assert_eq!(INTERNAL_FALLBACK_TEMPLATE_KEY, "minimal-fallback");
        assert_ne!(DEFAULT_TEMPLATE_KEY, INTERNAL_FALLBACK_TEMPLATE_KEY);
    }

    #[test]
    fn rust_matches_the_shared_poster_template_resolution_matrix() {
        let matrix: Value = serde_json::from_str(include_str!(
            "../../tests/fixtures/poster-template-resolution-v1.json"
        ))
        .expect("shared template resolution matrix should be valid JSON");

        for sample in matrix
            .as_array()
            .expect("template resolution matrix should be an array")
        {
            let name = sample["name"].as_str().expect("sample name");
            let input = &sample["input"];
            let result = resolve_poster_template_selection(
                input["snapshotStatus"].as_str().unwrap_or_default(),
                input["requestedTemplateKey"].as_str(),
                input["requestedTemplateStatus"]
                    .as_str()
                    .unwrap_or_default(),
                input["fallbackTemplateStatus"].as_str().unwrap_or_default(),
            );
            let expected_valid = sample["valid"].as_bool().expect("sample decision");
            assert_eq!(result.is_ok(), expected_valid, "{name}");

            if !expected_valid {
                let expected_code = sample["errorCode"].as_str().expect("error code");
                assert!(
                    result
                        .expect_err("invalid sample should fail")
                        .iter()
                        .any(|error| error.code == expected_code),
                    "{name}"
                );
                continue;
            }

            let value = serde_json::to_value(result.expect("valid sample should resolve"))
                .expect("template resolution should serialize");
            let expected = &sample["expected"];
            assert_eq!(value["contractVersion"], 1, "{name}");
            assert_eq!(
                value["requestedTemplateKey"], input["requestedTemplateKey"],
                "{name}"
            );
            assert_eq!(
                value["renderTemplateKey"], expected["renderTemplateKey"],
                "{name}"
            );
            assert_eq!(value["selection"], expected["selection"], "{name}");
            assert_eq!(value["reason"], expected["reason"], "{name}");
            assert_eq!(
                value["fallbackFailureReason"], expected["fallbackFailureReason"],
                "{name}"
            );
            assert_eq!(value["preserveRequestedTemplateKey"], true, "{name}");
            assert_eq!(value["persistenceMutation"], "none", "{name}");
        }
    }
}
