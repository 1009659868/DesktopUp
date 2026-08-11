use std::{collections::HashSet, fmt};

use serde::Deserialize;
use serde_json::Value;

pub use crate::poster_template_resolution::INTERNAL_FALLBACK_TEMPLATE_KEY;

pub const POSTER_CONTRACT_VERSION: u64 = 1;
const JAVASCRIPT_MAX_SAFE_INTEGER: u64 = 9_007_199_254_740_991;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct PosterContractViolation {
    pub path: String,
    pub code: &'static str,
    pub message: String,
}

impl PosterContractViolation {
    fn new(path: impl Into<String>, code: &'static str, message: impl Into<String>) -> Self {
        Self {
            path: path.into(),
            code,
            message: message.into(),
        }
    }
}

impl fmt::Display for PosterContractViolation {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "{} [{}]: {}", self.path, self.code, self.message)
    }
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct PosterContractSnapshotV1 {
    contract_version: u64,
    revision: u64,
    selected_poster_id: String,
    posters: Vec<PosterContractPosterV1>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct PosterContractPosterV1 {
    id: String,
    title: String,
    subtitle: String,
    monitor_identity: String,
    anchor: PosterAnchor,
    normalized_rect: NormalizedRect,
    width: u64,
    height: u64,
    collapsed_state: PresentationMode,
    auto_collapse_enabled: bool,
    auto_collapse_delay_seconds: u64,
    is_locked: bool,
    layout_kind: LayoutKind,
    layout_spec: Value,
    template_key: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct NormalizedRect {
    x: f64,
    y: f64,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "kebab-case")]
enum PosterAnchor {
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Free,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "lowercase")]
enum PresentationMode {
    Expanded,
    Collapsed,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "lowercase")]
enum LayoutKind {
    Grid,
    Free,
}

pub fn validate_poster_contract_v1(snapshot: &Value) -> Result<(), Vec<PosterContractViolation>> {
    let parsed = match serde_json::from_value::<PosterContractSnapshotV1>(snapshot.clone()) {
        Ok(parsed) => parsed,
        Err(error) => {
            return Err(vec![PosterContractViolation::new(
                "$",
                "schema_mismatch",
                error.to_string(),
            )]);
        }
    };

    let mut errors = Vec::new();
    if parsed.contract_version != POSTER_CONTRACT_VERSION {
        errors.push(PosterContractViolation::new(
            "$.contractVersion",
            "unsupported_contract_version",
            format!("expected poster contract version {POSTER_CONTRACT_VERSION}"),
        ));
    }
    if parsed.revision > JAVASCRIPT_MAX_SAFE_INTEGER {
        errors.push(PosterContractViolation::new(
            "$.revision",
            "invalid_revision",
            "revision exceeds the JavaScript safe integer range",
        ));
    }
    if parsed.posters.is_empty() {
        errors.push(PosterContractViolation::new(
            "$.posters",
            "empty_posters",
            "snapshot must contain at least one poster",
        ));
    }

    let mut poster_ids = HashSet::new();
    for (index, poster) in parsed.posters.iter().enumerate() {
        let path = format!("$.posters[{index}]");
        if poster.id.trim().is_empty() {
            errors.push(PosterContractViolation::new(
                format!("{path}.id"),
                "invalid_poster_id",
                "poster id must be a non-empty string",
            ));
        } else if !poster_ids.insert(poster.id.as_str()) {
            errors.push(PosterContractViolation::new(
                format!("{path}.id"),
                "duplicate_poster_id",
                "poster ids must be unique",
            ));
        }
        if poster.title.trim().is_empty() {
            errors.push(PosterContractViolation::new(
                format!("{path}.title"),
                "invalid_title",
                "poster title must be a non-empty string",
            ));
        }
        if poster.monitor_identity.trim().is_empty() {
            errors.push(PosterContractViolation::new(
                format!("{path}.monitorIdentity"),
                "invalid_monitor_identity",
                "monitor identity must be a non-empty string",
            ));
        }
        for (axis, coordinate) in [
            ("x", poster.normalized_rect.x),
            ("y", poster.normalized_rect.y),
        ] {
            if !coordinate.is_finite() || !(0.0..=1.0).contains(&coordinate) {
                errors.push(PosterContractViolation::new(
                    format!("{path}.normalizedRect.{axis}"),
                    "invalid_normalized_coordinate",
                    "normalized coordinate must be finite and in the inclusive range 0..1",
                ));
            }
        }
        for (field, dimension) in [("width", poster.width), ("height", poster.height)] {
            if dimension == 0 || dimension > JAVASCRIPT_MAX_SAFE_INTEGER {
                errors.push(PosterContractViolation::new(
                    format!("{path}.{field}"),
                    "invalid_dimension",
                    "dimension must be a positive JavaScript-safe integer",
                ));
            }
        }
        if !(1..=300).contains(&poster.auto_collapse_delay_seconds) {
            errors.push(PosterContractViolation::new(
                format!("{path}.autoCollapseDelaySeconds"),
                "invalid_auto_collapse_delay",
                "auto-collapse delay must be in the inclusive range 1..300",
            ));
        }
        if !poster.layout_spec.is_object() {
            errors.push(PosterContractViolation::new(
                format!("{path}.layoutSpec"),
                "invalid_layout_spec",
                "layout spec must be a JSON object",
            ));
        }
        if poster.template_key.trim().is_empty() {
            errors.push(PosterContractViolation::new(
                format!("{path}.templateKey"),
                "invalid_template_key",
                "template key must be a non-empty string",
            ));
        } else if poster.template_key == INTERNAL_FALLBACK_TEMPLATE_KEY {
            errors.push(PosterContractViolation::new(
                format!("{path}.templateKey"),
                "fallback_template_is_not_persistent",
                "minimal-fallback cannot be persisted",
            ));
        }

        let _ = (
            &poster.subtitle,
            &poster.anchor,
            &poster.collapsed_state,
            poster.auto_collapse_enabled,
            poster.is_locked,
            &poster.layout_kind,
        );
    }

    if !poster_ids.contains(parsed.selected_poster_id.as_str()) {
        errors.push(PosterContractViolation::new(
            "$.selectedPosterId",
            "selected_poster_not_found",
            "selected poster id must reference an existing poster",
        ));
    }

    if errors.is_empty() {
        Ok(())
    } else {
        Err(errors)
    }
}

pub fn summarize_poster_contract_errors(errors: &[PosterContractViolation]) -> String {
    errors
        .iter()
        .map(ToString::to_string)
        .collect::<Vec<_>>()
        .join("; ")
}

#[cfg(test)]
mod tests {
    use super::{validate_poster_contract_v1, INTERNAL_FALLBACK_TEMPLATE_KEY};
    use serde_json::{json, Value};

    fn valid_snapshot() -> Value {
        json!({
            "contractVersion": 1,
            "revision": 0,
            "selectedPosterId": "poster-primary",
            "posters": [{
                "id": "poster-primary",
                "title": "DESKTOP COLLECTION",
                "subtitle": "",
                "monitorIdentity": "primary",
                "anchor": "top-right",
                "normalizedRect": { "x": 1.0, "y": 0.0 },
                "width": 490,
                "height": 392,
                "collapsedState": "expanded",
                "autoCollapseEnabled": false,
                "autoCollapseDelaySeconds": 3,
                "isLocked": false,
                "layoutKind": "grid",
                "layoutSpec": {},
                "templateKey": "transparent-default"
            }]
        })
    }

    fn violation_codes(snapshot: &Value) -> Vec<&'static str> {
        validate_poster_contract_v1(snapshot)
            .expect_err("sample should fail contract validation")
            .into_iter()
            .map(|error| error.code)
            .collect()
    }

    #[test]
    fn rust_matches_the_shared_poster_contract_v1_decision_matrix() {
        let matrix: Value =
            serde_json::from_str(include_str!("../../tests/fixtures/poster-contract-v1.json"))
                .expect("shared contract matrix should be valid JSON");
        for sample in matrix
            .as_array()
            .expect("contract matrix should be an array")
        {
            let name = sample["name"].as_str().expect("sample should have a name");
            let expected = sample["valid"]
                .as_bool()
                .expect("sample should have an expected decision");
            assert_eq!(
                validate_poster_contract_v1(&sample["snapshot"]).is_ok(),
                expected,
                "shared contract decision mismatch for {name}"
            );
        }
    }

    #[test]
    fn accepts_the_same_single_poster_default_as_the_js_contract() {
        assert!(validate_poster_contract_v1(&valid_snapshot()).is_ok());
    }

    #[test]
    fn rejects_unknown_versions_duplicate_ids_and_dangling_selection() {
        let mut snapshot = valid_snapshot();
        snapshot["contractVersion"] = json!(99);
        let duplicate = snapshot["posters"][0].clone();
        snapshot["posters"].as_array_mut().unwrap().push(duplicate);
        snapshot["selectedPosterId"] = json!("missing");
        let codes = violation_codes(&snapshot);
        assert!(codes.contains(&"unsupported_contract_version"));
        assert!(codes.contains(&"duplicate_poster_id"));
        assert!(codes.contains(&"selected_poster_not_found"));
    }

    #[test]
    fn rejects_runtime_state_and_unknown_fields_at_the_schema_boundary() {
        let mut snapshot = valid_snapshot();
        snapshot["posters"][0]["runtimeMode"] = json!("paused");
        let codes = violation_codes(&snapshot);
        assert_eq!(codes, vec!["schema_mismatch"]);
    }

    #[test]
    fn rejects_internal_fallback_as_a_persistent_template() {
        let mut snapshot = valid_snapshot();
        snapshot["posters"][0]["templateKey"] = json!(INTERNAL_FALLBACK_TEMPLATE_KEY);
        assert_eq!(
            violation_codes(&snapshot),
            vec!["fallback_template_is_not_persistent"]
        );
    }

    #[test]
    fn rejects_invalid_geometry_delay_and_layout_spec() {
        let mut snapshot = valid_snapshot();
        snapshot["posters"][0]["normalizedRect"]["x"] = json!(1.1);
        snapshot["posters"][0]["width"] = json!(0);
        snapshot["posters"][0]["autoCollapseDelaySeconds"] = json!(0);
        snapshot["posters"][0]["layoutSpec"] = json!([]);
        let codes = violation_codes(&snapshot);
        assert!(codes.contains(&"invalid_normalized_coordinate"));
        assert!(codes.contains(&"invalid_dimension"));
        assert!(codes.contains(&"invalid_auto_collapse_delay"));
        assert!(codes.contains(&"invalid_layout_spec"));
    }
}
