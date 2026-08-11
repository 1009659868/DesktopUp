use serde::Serialize;
use std::fmt;

pub const POSTER_PERMISSION_CONTRACT_VERSION: u32 = 1;
pub const TITLEBAR_REQUIRED_SLOTS: &[&str] = &[
    "title",
    "templateIdentity",
    "itemCount",
    "presentationToggle",
    "lockToggle",
];
pub const TITLEBAR_DRAG_ORIGINS: &[&str] =
    &["background", "title", "templateIdentity", "itemCount"];
pub const TITLEBAR_DRAG_EXCLUSIONS: &[&str] = &[
    "presentationToggle",
    "lockToggle",
    "button",
    "link",
    "input",
    "select",
    "textarea",
    "item",
];
pub const POINTER_DRAG_THRESHOLD_PX: u32 = 5;

const PRESENTATION_MODES: &[&str] = &["expanded", "collapsed"];
const INTERACTION_MODES: &[&str] = &["using", "editing"];
const RUNTIME_MODES: &[&str] = &["activeStatic", "activeAnimated", "paused", "stopped"];

#[derive(Clone, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PosterPermissionSet {
    pub drag_from_titlebar: bool,
    pub drag_from_content: bool,
    pub toggle_presentation: bool,
    pub toggle_lock: bool,
    pub expand_from_title_text: bool,
    pub single_activate_item: bool,
    pub explicit_activate_item: bool,
    pub select_item: bool,
    pub reorder_item: bool,
    pub resize_poster: bool,
    pub open_poster_menu: bool,
    pub open_item_menu: bool,
    pub auto_collapse: bool,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ResolvedPosterPermissions {
    pub contract_version: u32,
    pub presentation_mode: String,
    pub requested_interaction_mode: String,
    pub effective_interaction_mode: String,
    pub runtime_mode: String,
    pub is_locked: bool,
    pub runtime_interactive: bool,
    pub permissions: PosterPermissionSet,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PosterPermissionError {
    pub path: &'static str,
    pub code: &'static str,
    pub message: &'static str,
}

impl PosterPermissionError {
    fn new(path: &'static str, code: &'static str, message: &'static str) -> Self {
        Self {
            path,
            code,
            message,
        }
    }
}

impl fmt::Display for PosterPermissionError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "{} [{}]: {}", self.path, self.code, self.message)
    }
}

fn is_active_runtime(runtime_mode: &str) -> bool {
    matches!(runtime_mode, "activeStatic" | "activeAnimated")
}

pub fn resolve_poster_permissions(
    presentation_mode: &str,
    interaction_mode: &str,
    runtime_mode: &str,
    is_locked: bool,
) -> Result<ResolvedPosterPermissions, Vec<PosterPermissionError>> {
    let mut errors = Vec::new();
    if !PRESENTATION_MODES.contains(&presentation_mode) {
        errors.push(PosterPermissionError::new(
            "$.presentationMode",
            "invalid_presentation_mode",
            "presentation mode must be expanded or collapsed",
        ));
    }
    if !INTERACTION_MODES.contains(&interaction_mode) {
        errors.push(PosterPermissionError::new(
            "$.interactionMode",
            "invalid_interaction_mode",
            "interaction mode must be using or editing",
        ));
    }
    if !RUNTIME_MODES.contains(&runtime_mode) {
        errors.push(PosterPermissionError::new(
            "$.runtimeMode",
            "invalid_runtime_mode",
            "runtime mode must be activeStatic, activeAnimated, paused, or stopped",
        ));
    }
    if !errors.is_empty() {
        return Err(errors);
    }

    let runtime_interactive = is_active_runtime(runtime_mode);
    let effective_interaction_mode =
        if runtime_interactive && !(is_locked && interaction_mode == "editing") {
            interaction_mode
        } else {
            "using"
        };
    let expanded = presentation_mode == "expanded";
    let editing = effective_interaction_mode == "editing";

    Ok(ResolvedPosterPermissions {
        contract_version: POSTER_PERMISSION_CONTRACT_VERSION,
        presentation_mode: presentation_mode.to_string(),
        requested_interaction_mode: interaction_mode.to_string(),
        effective_interaction_mode: effective_interaction_mode.to_string(),
        runtime_mode: runtime_mode.to_string(),
        is_locked,
        runtime_interactive,
        permissions: PosterPermissionSet {
            drag_from_titlebar: runtime_interactive && !is_locked,
            drag_from_content: runtime_interactive && expanded && editing,
            toggle_presentation: runtime_interactive,
            toggle_lock: runtime_interactive,
            expand_from_title_text: runtime_interactive && !expanded,
            single_activate_item: runtime_interactive && expanded && !editing,
            explicit_activate_item: runtime_interactive && expanded,
            select_item: runtime_interactive && expanded && editing,
            reorder_item: runtime_interactive && expanded && editing,
            resize_poster: runtime_interactive && expanded && editing,
            open_poster_menu: runtime_interactive,
            open_item_menu: runtime_interactive && expanded,
            auto_collapse: runtime_interactive && !editing,
        },
    })
}

#[cfg(test)]
mod tests {
    use super::{
        resolve_poster_permissions, POINTER_DRAG_THRESHOLD_PX, TITLEBAR_DRAG_EXCLUSIONS,
        TITLEBAR_DRAG_ORIGINS, TITLEBAR_REQUIRED_SLOTS,
    };
    use serde_json::Value;
    use std::collections::BTreeSet;

    #[test]
    fn accepted_titlebar_contract_is_frozen() {
        assert_eq!(
            TITLEBAR_REQUIRED_SLOTS,
            [
                "title",
                "templateIdentity",
                "itemCount",
                "presentationToggle",
                "lockToggle"
            ]
        );
        assert_eq!(
            TITLEBAR_DRAG_ORIGINS,
            ["background", "title", "templateIdentity", "itemCount"]
        );
        assert!(TITLEBAR_DRAG_EXCLUSIONS.contains(&"button"));
        assert!(TITLEBAR_DRAG_EXCLUSIONS.contains(&"item"));
        assert_eq!(POINTER_DRAG_THRESHOLD_PX, 5);
    }

    #[test]
    fn rust_matches_the_shared_poster_permission_matrix() {
        let matrix: Value = serde_json::from_str(include_str!(
            "../../tests/fixtures/poster-permissions-v1.json"
        ))
        .expect("shared permission matrix should be valid JSON");

        for sample in matrix
            .as_array()
            .expect("permission matrix should be an array")
        {
            let name = sample["name"].as_str().expect("sample name");
            let input = &sample["input"];
            let result = resolve_poster_permissions(
                input["presentationMode"].as_str().unwrap_or_default(),
                input["interactionMode"].as_str().unwrap_or_default(),
                input["runtimeMode"].as_str().unwrap_or_default(),
                input["isLocked"].as_bool().unwrap_or_default(),
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
                .expect("resolved permissions should serialize");
            assert_eq!(
                value["effectiveInteractionMode"], sample["effectiveInteractionMode"],
                "{name}"
            );
            let actual_actions = value["permissions"]
                .as_object()
                .expect("permission object")
                .iter()
                .filter_map(|(action, allowed)| {
                    allowed
                        .as_bool()
                        .filter(|allowed| *allowed)
                        .map(|_| action.as_str())
                })
                .collect::<BTreeSet<_>>();
            let expected_actions = sample["allowedActions"]
                .as_array()
                .expect("allowed actions")
                .iter()
                .map(|action| action.as_str().expect("action name"))
                .collect::<BTreeSet<_>>();
            assert_eq!(value["contractVersion"], 1, "{name}");
            assert_eq!(
                value["runtimeInteractive"],
                !expected_actions.is_empty(),
                "{name}"
            );
            assert_eq!(actual_actions, expected_actions, "{name}");
        }
    }
}
