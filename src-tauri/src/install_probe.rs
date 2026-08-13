use std::{fs, io::ErrorKind, path::Path};

use serde::Serialize;

use crate::{
    install_baseline::{InstallBaselineContract, CONTRACT_NAME, CONTRACT_VERSION},
    install_paths::RuntimePathResolution,
};

pub const PROBE_VERSION: u32 = 1;
pub const PROBE_NAME: &str = "install-content-probe-v1";

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct InstallPermissionProbe {
    pub install_scope: String,
    pub elevation_required: bool,
    pub runtime_install_writes_allowed: bool,
    pub enforcement: String,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct InstallProbeSideEffects {
    pub writes_install_root: bool,
    pub creates_directories: bool,
    pub installs_or_uninstalls: bool,
    pub requests_elevation: bool,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct InstallEntryProbe {
    pub entry: String,
    pub expected_kind: String,
    pub target_path: String,
    pub observed_kind: String,
    pub ready: bool,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct InstallProbeGap {
    pub code: String,
    pub severity: String,
    pub owner: String,
    pub detail: String,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct InstallContentProbe {
    pub probe_version: u32,
    pub probe_name: String,
    pub contract_version: u32,
    pub contract_name: String,
    pub deployment_status: String,
    pub readiness: String,
    pub target_install_root: String,
    pub target_install_root_kind: String,
    pub current_executable_path: String,
    pub current_executable_name: String,
    pub current_executable_kind: String,
    pub current_executable_in_target_install_root: bool,
    pub current_executable_name_matches_contract: bool,
    pub required_entries: Vec<InstallEntryProbe>,
    pub permissions: InstallPermissionProbe,
    pub side_effects: InstallProbeSideEffects,
    pub gaps: Vec<InstallProbeGap>,
}

pub fn probe_install_content(paths: &RuntimePathResolution) -> InstallContentProbe {
    let baseline = InstallBaselineContract::v1();
    let target_install_root = paths.install_root();
    let current_executable_path = paths.executable_path();
    let current_executable_name = current_executable_path
        .file_name()
        .map(|name| name.to_string_lossy().into_owned())
        .unwrap_or_default();
    let expected_executable_name = baseline
        .required_install_entries
        .first()
        .copied()
        .unwrap_or("DesktopUp.exe");
    let executable_in_target =
        paths.diagnostics().executable_placement == "matchesTargetInstallRoot";
    let executable_name_matches =
        current_executable_name.eq_ignore_ascii_case(expected_executable_name);

    let required_entries = baseline
        .required_install_entries
        .iter()
        .map(|entry| probe_required_entry(target_install_root, entry))
        .collect::<Vec<_>>();

    let mut gaps = Vec::new();
    if !executable_in_target {
        gaps.push(InstallProbeGap {
            code: "runningOutsideTargetInstallRoot".to_string(),
            severity: "info".to_string(),
            owner: "C-RCG".to_string(),
            detail: path_to_string(current_executable_path),
        });
    }
    if !executable_name_matches {
        gaps.push(InstallProbeGap {
            code: "mainExecutableNameMismatch".to_string(),
            severity: "error".to_string(),
            owner: "C-RCG".to_string(),
            detail: format!(
                "expected {expected_executable_name}, observed {current_executable_name}"
            ),
        });
    }
    for entry in required_entries.iter().filter(|entry| !entry.ready) {
        gaps.push(InstallProbeGap {
            code: "requiredInstallEntryNotReady".to_string(),
            severity: "error".to_string(),
            owner: if entry.entry == "uninstall.exe" {
                "C-URV".to_string()
            } else {
                "C-RCG".to_string()
            },
            detail: format!(
                "{} expected {}, observed {}",
                entry.entry, entry.expected_kind, entry.observed_kind
            ),
        });
    }

    let target_root_kind = inspect_path_kind(target_install_root);
    let current_executable_kind = inspect_path_kind(current_executable_path);
    let ready = executable_in_target
        && executable_name_matches
        && target_root_kind == "directory"
        && current_executable_kind == "file"
        && required_entries.iter().all(|entry| entry.ready);

    InstallContentProbe {
        probe_version: PROBE_VERSION,
        probe_name: PROBE_NAME.to_string(),
        contract_version: CONTRACT_VERSION,
        contract_name: CONTRACT_NAME.to_string(),
        deployment_status: if executable_in_target {
            "targetInstalled".to_string()
        } else {
            "developmentOrPortable".to_string()
        },
        readiness: if ready {
            "ready".to_string()
        } else {
            "gapsDetected".to_string()
        },
        target_install_root: path_to_string(target_install_root),
        target_install_root_kind: target_root_kind,
        current_executable_path: path_to_string(current_executable_path),
        current_executable_name,
        current_executable_kind,
        current_executable_in_target_install_root: executable_in_target,
        current_executable_name_matches_contract: executable_name_matches,
        required_entries,
        permissions: InstallPermissionProbe {
            install_scope: baseline.install_scope.to_string(),
            elevation_required: baseline.elevation_required,
            runtime_install_writes_allowed: baseline.mutable_install_writes_allowed,
            enforcement: "contractAndSourceBoundary".to_string(),
        },
        side_effects: InstallProbeSideEffects {
            writes_install_root: false,
            creates_directories: false,
            installs_or_uninstalls: false,
            requests_elevation: false,
        },
        gaps,
    }
}

fn probe_required_entry(install_root: &Path, entry: &str) -> InstallEntryProbe {
    let expects_directory = entry.ends_with('/');
    let expected_kind = if expects_directory {
        "directory"
    } else {
        "file"
    };
    let target_path = install_root.join(entry.trim_end_matches('/'));
    let observed_kind = inspect_path_kind(&target_path);
    InstallEntryProbe {
        entry: entry.to_string(),
        expected_kind: expected_kind.to_string(),
        target_path: path_to_string(&target_path),
        ready: observed_kind == expected_kind,
        observed_kind,
    }
}

fn inspect_path_kind(path: &Path) -> String {
    match fs::symlink_metadata(path) {
        Ok(metadata) if metadata.file_type().is_symlink() => "symlink".to_string(),
        Ok(metadata) if metadata.is_file() => "file".to_string(),
        Ok(metadata) if metadata.is_dir() => "directory".to_string(),
        Ok(_) => "other".to_string(),
        Err(error) if error.kind() == ErrorKind::NotFound => "missing".to_string(),
        Err(_) => "unreadable".to_string(),
    }
}

fn path_to_string(path: &Path) -> String {
    path.to_string_lossy().into_owned()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::install_paths::{resolve_runtime_paths, RuntimePathInputs};
    use std::path::PathBuf;
    use std::time::{SystemTime, UNIX_EPOCH};

    fn unique_root(label: &str) -> PathBuf {
        let unique = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("system clock")
            .as_nanos();
        std::env::temp_dir().join(format!("desktop-up-{label}-{unique}"))
    }

    fn resolution_for(root: &Path, executable_path: PathBuf) -> RuntimePathResolution {
        resolve_runtime_paths(RuntimePathInputs {
            local_data_dir: root.join("LocalAppData"),
            current_runtime_root: root.join("Roaming").join("com.allmight.desktopup"),
            executable_path,
        })
        .expect("resolve runtime paths")
    }

    #[test]
    fn missing_target_is_reported_without_materializing_it() {
        let root = unique_root("install-probe-missing");
        let resolution = resolution_for(
            &root,
            root.join("workspace")
                .join("target")
                .join("release")
                .join("desktop-up.exe"),
        );
        let report = probe_install_content(&resolution);
        assert_eq!(report.deployment_status, "developmentOrPortable");
        assert_eq!(report.readiness, "gapsDetected");
        assert_eq!(report.target_install_root_kind, "missing");
        assert!(report.required_entries.iter().all(|entry| !entry.ready));
        assert!(!root.exists());
    }

    #[test]
    fn exact_target_content_can_be_recognized_without_probe_writes() {
        let root = unique_root("install-probe-ready");
        let install_root = root.join("LocalAppData").join("Programs").join("DesktopUp");
        fs::create_dir_all(install_root.join("resources")).expect("create resource fixture");
        fs::create_dir_all(install_root.join("licenses")).expect("create license fixture");
        fs::write(install_root.join("DesktopUp.exe"), b"fixture").expect("write app fixture");
        fs::write(install_root.join("uninstall.exe"), b"fixture")
            .expect("write uninstaller fixture");

        let resolution = resolution_for(&root, install_root.join("DesktopUp.exe"));
        let report = probe_install_content(&resolution);
        assert_eq!(report.deployment_status, "targetInstalled");
        assert_eq!(report.readiness, "ready");
        assert!(report.gaps.is_empty());
        assert!(report.required_entries.iter().all(|entry| entry.ready));
        assert!(!report.side_effects.writes_install_root);

        fs::remove_dir_all(&root).expect("remove isolated fixture");
    }

    #[test]
    fn wrong_entry_kind_and_executable_name_are_explicit_gaps() {
        let root = unique_root("install-probe-wrong-kind");
        let install_root = root.join("LocalAppData").join("Programs").join("DesktopUp");
        fs::create_dir_all(&install_root).expect("create install fixture");
        fs::write(install_root.join("resources"), b"not a directory")
            .expect("write wrong-kind fixture");
        let resolution = resolution_for(&root, install_root.join("desktop-up.exe"));
        let report = probe_install_content(&resolution);
        assert!(report
            .gaps
            .iter()
            .any(|gap| gap.code == "mainExecutableNameMismatch"));
        assert_eq!(
            report
                .required_entries
                .iter()
                .find(|entry| entry.entry == "resources/")
                .expect("resource entry")
                .observed_kind,
            "file"
        );

        fs::remove_dir_all(&root).expect("remove isolated fixture");
    }

    #[test]
    fn serialized_probe_freezes_permission_and_side_effect_contracts() {
        let root = unique_root("install-probe-serialization");
        let resolution = resolution_for(&root, root.join("desktop-up.exe"));
        let value = serde_json::to_value(probe_install_content(&resolution))
            .expect("serialize install probe");
        assert_eq!(value["probeName"], "install-content-probe-v1");
        assert_eq!(value["permissions"]["installScope"], "currentUser");
        assert_eq!(value["permissions"]["elevationRequired"], false);
        assert_eq!(value["permissions"]["runtimeInstallWritesAllowed"], false);
        assert_eq!(value["sideEffects"]["writesInstallRoot"], false);
        assert_eq!(value["sideEffects"]["installsOrUninstalls"], false);
        assert!(!root.exists());
    }
}
