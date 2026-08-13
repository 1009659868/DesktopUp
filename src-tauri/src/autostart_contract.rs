use std::{
    fs,
    io::ErrorKind,
    path::{Path, PathBuf},
};

use serde::Serialize;

use crate::install_paths::RuntimePathResolution;

pub const AUTOSTART_CONTRACT_VERSION: u32 = 1;
pub const AUTOSTART_CONTRACT_NAME: &str = "autostart-contract-v1";
pub const AUTOSTART_DIAGNOSTIC_VERSION: u32 = 1;
pub const AUTOSTART_DIAGNOSTIC_NAME: &str = "autostart-diagnostics-v1";
pub const AUTOSTART_REGISTRY_HIVE: &str = "HKEY_CURRENT_USER";
pub const AUTOSTART_REGISTRY_SUBKEY: &str = r"Software\Microsoft\Windows\CurrentVersion\Run";
pub const AUTOSTART_REGISTRY_VALUE_NAME: &str = "DesktopUp";
pub const AUTOSTART_REGISTRY_VALUE_TYPE: &str = "REG_SZ";
pub const AUTOSTART_ARGUMENT: &str = "--autostart";
pub const EXPECTED_EXECUTABLE_NAME: &str = "DesktopUp.exe";

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AutostartRegistryContract {
    pub hive: String,
    pub subkey: String,
    pub value_name: String,
    pub value_type: String,
    pub remove_owned_value_only: bool,
    pub remove_parent_key_allowed: bool,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AutostartSingleInstanceContract {
    pub startup_argument: String,
    pub initial_instance_behavior: String,
    pub secondary_instance_behavior: String,
    pub control_center_must_remain_hidden: bool,
    pub additional_arguments_allowed: bool,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AutostartCleanupContract {
    pub owner: String,
    pub required_on_uninstall: bool,
    pub required_on_recovery_failure: bool,
    pub required_after_partial_restore_failure: bool,
    pub missing_value_is_success: bool,
    pub idempotent: bool,
    pub requires_elevation: bool,
    pub failure_result: String,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AutostartContract {
    pub contract_version: u32,
    pub contract_name: String,
    pub default_enabled: bool,
    pub explicit_opt_in_required: bool,
    pub implementation_owner: String,
    pub registration_scope: String,
    pub registration_mechanism: String,
    pub registration_requires_target_installed: bool,
    pub registration_requires_regular_file: bool,
    pub registration_rejects_symlink: bool,
    pub registration_requires_exact_command: bool,
    pub expected_executable_name: String,
    pub expected_executable_path: String,
    pub expected_command: String,
    pub registry: AutostartRegistryContract,
    pub single_instance: AutostartSingleInstanceContract,
    pub cleanup: AutostartCleanupContract,
}

impl AutostartContract {
    pub fn v1(paths: &RuntimePathResolution) -> Self {
        let expected_executable_path = paths.install_root().join(EXPECTED_EXECUTABLE_NAME);
        let expected_executable_path = path_to_string(&expected_executable_path);
        Self {
            contract_version: AUTOSTART_CONTRACT_VERSION,
            contract_name: AUTOSTART_CONTRACT_NAME.to_string(),
            default_enabled: false,
            explicit_opt_in_required: true,
            implementation_owner: "C-RLC".to_string(),
            registration_scope: "currentUser".to_string(),
            registration_mechanism: "registryRun".to_string(),
            registration_requires_target_installed: true,
            registration_requires_regular_file: true,
            registration_rejects_symlink: true,
            registration_requires_exact_command: true,
            expected_executable_name: EXPECTED_EXECUTABLE_NAME.to_string(),
            expected_command: expected_command(&expected_executable_path),
            expected_executable_path,
            registry: AutostartRegistryContract {
                hive: AUTOSTART_REGISTRY_HIVE.to_string(),
                subkey: AUTOSTART_REGISTRY_SUBKEY.to_string(),
                value_name: AUTOSTART_REGISTRY_VALUE_NAME.to_string(),
                value_type: AUTOSTART_REGISTRY_VALUE_TYPE.to_string(),
                remove_owned_value_only: true,
                remove_parent_key_allowed: false,
            },
            single_instance: AutostartSingleInstanceContract {
                startup_argument: AUTOSTART_ARGUMENT.to_string(),
                initial_instance_behavior: "startPosterWithoutControlCenter".to_string(),
                secondary_instance_behavior: "keepExistingInstanceWithoutUiChange".to_string(),
                control_center_must_remain_hidden: true,
                additional_arguments_allowed: false,
            },
            cleanup: AutostartCleanupContract {
                owner: "C-URV".to_string(),
                required_on_uninstall: true,
                required_on_recovery_failure: true,
                required_after_partial_restore_failure: true,
                missing_value_is_success: true,
                idempotent: true,
                requires_elevation: false,
                failure_result: "partialFailureWithRetryEvidence".to_string(),
            },
        }
    }

    pub fn validate(&self, paths: &RuntimePathResolution) -> Result<(), Vec<String>> {
        let expected_path = path_to_string(&paths.install_root().join(EXPECTED_EXECUTABLE_NAME));
        let mut errors = Vec::new();
        if self.contract_version != AUTOSTART_CONTRACT_VERSION
            || self.contract_name != AUTOSTART_CONTRACT_NAME
        {
            errors.push("unsupported autostart contract".to_string());
        }
        if self.default_enabled
            || !self.explicit_opt_in_required
            || self.registration_scope != "currentUser"
            || self.registration_mechanism != "registryRun"
        {
            errors.push("autostart opt-in or registration scope drifted".to_string());
        }
        if self.registry.hive != AUTOSTART_REGISTRY_HIVE
            || self.registry.subkey != AUTOSTART_REGISTRY_SUBKEY
            || self.registry.value_name != AUTOSTART_REGISTRY_VALUE_NAME
            || self.registry.value_type != AUTOSTART_REGISTRY_VALUE_TYPE
            || !self.registry.remove_owned_value_only
            || self.registry.remove_parent_key_allowed
        {
            errors.push("autostart registry ownership drifted".to_string());
        }
        if !self.registration_requires_target_installed
            || !self.registration_requires_regular_file
            || !self.registration_rejects_symlink
            || !self.registration_requires_exact_command
            || self.expected_executable_name != EXPECTED_EXECUTABLE_NAME
            || !paths_equal_text(&self.expected_executable_path, &expected_path)
            || self.expected_command != expected_command(&expected_path)
        {
            errors.push("autostart executable or command validation drifted".to_string());
        }
        if self.single_instance.startup_argument != AUTOSTART_ARGUMENT
            || self.single_instance.initial_instance_behavior != "startPosterWithoutControlCenter"
            || self.single_instance.secondary_instance_behavior
                != "keepExistingInstanceWithoutUiChange"
            || !self.single_instance.control_center_must_remain_hidden
            || self.single_instance.additional_arguments_allowed
        {
            errors.push("autostart single-instance behavior drifted".to_string());
        }
        if self.implementation_owner != "C-RLC"
            || self.cleanup.owner != "C-URV"
            || !self.cleanup.required_on_uninstall
            || !self.cleanup.required_on_recovery_failure
            || !self.cleanup.required_after_partial_restore_failure
            || !self.cleanup.missing_value_is_success
            || !self.cleanup.idempotent
            || self.cleanup.requires_elevation
        {
            errors.push("autostart implementation or cleanup ownership drifted".to_string());
        }
        if errors.is_empty() {
            Ok(())
        } else {
            Err(errors)
        }
    }
}

#[derive(Clone, Debug, Eq, PartialEq)]
pub enum AutostartRegistryObservation {
    Absent,
    Present { value_type: String, command: String },
    Unreadable { error_code: u32 },
    Unsupported,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AutostartDiagnosticSideEffects {
    pub reads_registry: bool,
    pub writes_registry: bool,
    pub writes_files: bool,
    pub launches_processes: bool,
    pub changes_autostart: bool,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AutostartDiagnosticGap {
    pub code: String,
    pub severity: String,
    pub owner: String,
    pub detail: String,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AutostartDiagnostics {
    pub diagnostic_version: u32,
    pub diagnostic_name: String,
    pub contract: AutostartContract,
    pub readiness: String,
    pub registration_state: String,
    pub effective_enabled: bool,
    pub registration_eligible: bool,
    pub expected_command: String,
    pub observed_value_type: String,
    pub observed_command: String,
    pub target_executable_kind: String,
    pub current_executable_matches_target: bool,
    pub current_runtime_integration: String,
    pub side_effects: AutostartDiagnosticSideEffects,
    pub gaps: Vec<AutostartDiagnosticGap>,
}

pub fn probe_autostart(paths: &RuntimePathResolution) -> AutostartDiagnostics {
    probe_autostart_with_observation(paths, platform::read_autostart_value())
}

fn probe_autostart_with_observation(
    paths: &RuntimePathResolution,
    observation: AutostartRegistryObservation,
) -> AutostartDiagnostics {
    let contract = AutostartContract::v1(paths);
    let contract_errors = contract.validate(paths).err().unwrap_or_default();
    let target_executable_path = PathBuf::from(&contract.expected_executable_path);
    let target_executable_kind = inspect_path_kind(&target_executable_path);
    let current_executable_matches_target =
        paths_equal(paths.executable_path(), &target_executable_path);
    let registration_eligible =
        target_executable_kind == "file" && current_executable_matches_target;
    let mut gaps = contract_errors
        .into_iter()
        .map(|detail| AutostartDiagnosticGap {
            code: "contractInvalid".to_string(),
            severity: "error".to_string(),
            owner: "C-IBC".to_string(),
            detail,
        })
        .collect::<Vec<_>>();

    if !registration_eligible {
        gaps.push(AutostartDiagnosticGap {
            code: "formalInstalledExecutableNotReady".to_string(),
            severity: "info".to_string(),
            owner: "C-RCG".to_string(),
            detail: format!(
                "expected {}, observed target kind {}, current executable {}",
                contract.expected_executable_path,
                target_executable_kind,
                path_to_string(paths.executable_path())
            ),
        });
    }

    let (registration_state, observed_value_type, observed_command, effective_enabled) =
        match observation {
            AutostartRegistryObservation::Absent => {
                ("absent".to_string(), String::new(), String::new(), false)
            }
            AutostartRegistryObservation::Present {
                value_type,
                command,
            } => {
                let command_matches = command == contract.expected_command;
                let type_matches = value_type == contract.registry.value_type;
                if command_matches && type_matches && registration_eligible {
                    ("matching".to_string(), value_type, command, true)
                } else {
                    gaps.push(AutostartDiagnosticGap {
                        code: if !registration_eligible {
                            "staleAutostartRegistration".to_string()
                        } else {
                            "autostartRegistryDrift".to_string()
                        },
                        severity: "error".to_string(),
                        owner: if !registration_eligible {
                            "C-URV".to_string()
                        } else {
                            "C-RLC".to_string()
                        },
                        detail: format!(
                            "expected {} {}, observed {} {}",
                            contract.registry.value_type,
                            contract.expected_command,
                            value_type,
                            command
                        ),
                    });
                    ("drifted".to_string(), value_type, command, false)
                }
            }
            AutostartRegistryObservation::Unreadable { error_code } => {
                gaps.push(AutostartDiagnosticGap {
                    code: "autostartRegistryUnreadable".to_string(),
                    severity: "error".to_string(),
                    owner: "C-RLC".to_string(),
                    detail: format!("Win32 error {error_code}"),
                });
                (
                    "unreadable".to_string(),
                    String::new(),
                    String::new(),
                    false,
                )
            }
            AutostartRegistryObservation::Unsupported => {
                gaps.push(AutostartDiagnosticGap {
                    code: "autostartRegistryUnsupported".to_string(),
                    severity: "error".to_string(),
                    owner: "C-RLC".to_string(),
                    detail: "HKCU Run diagnostics require Windows".to_string(),
                });
                (
                    "unsupported".to_string(),
                    String::new(),
                    String::new(),
                    false,
                )
            }
        };

    let has_error = gaps.iter().any(|gap| gap.severity == "error");
    let readiness = if has_error {
        "gapsDetected"
    } else if effective_enabled {
        "readyEnabled"
    } else if registration_eligible {
        "readyDisabled"
    } else {
        "developmentOrPortableDisabled"
    };

    AutostartDiagnostics {
        diagnostic_version: AUTOSTART_DIAGNOSTIC_VERSION,
        diagnostic_name: AUTOSTART_DIAGNOSTIC_NAME.to_string(),
        expected_command: contract.expected_command.clone(),
        contract,
        readiness: readiness.to_string(),
        registration_state,
        effective_enabled,
        registration_eligible,
        observed_value_type,
        observed_command,
        target_executable_kind,
        current_executable_matches_target,
        current_runtime_integration: "contractOnlyCrlcPending".to_string(),
        side_effects: AutostartDiagnosticSideEffects {
            reads_registry: cfg!(windows),
            writes_registry: false,
            writes_files: false,
            launches_processes: false,
            changes_autostart: false,
        },
        gaps,
    }
}

fn expected_command(executable_path: &str) -> String {
    format!("\"{executable_path}\" {AUTOSTART_ARGUMENT}")
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

fn paths_equal(left: &Path, right: &Path) -> bool {
    paths_equal_text(&path_to_string(left), &path_to_string(right))
}

fn paths_equal_text(left: &str, right: &str) -> bool {
    normalize_windows_path(left).eq_ignore_ascii_case(&normalize_windows_path(right))
}

fn normalize_windows_path(path: &str) -> String {
    path.trim_start_matches(r"\\?\")
        .trim_end_matches(|character| character == '\\' || character == '/')
        .replace('/', "\\")
}

fn path_to_string(path: &Path) -> String {
    path.to_string_lossy().into_owned()
}

#[cfg(windows)]
mod platform {
    use super::{
        AutostartRegistryObservation, AUTOSTART_REGISTRY_SUBKEY, AUTOSTART_REGISTRY_VALUE_NAME,
    };
    use windows::{
        core::PCWSTR,
        Win32::{
            Foundation::{ERROR_FILE_NOT_FOUND, ERROR_MORE_DATA, ERROR_SUCCESS},
            System::Registry::{
                RegCloseKey, RegOpenKeyExW, RegQueryValueExW, HKEY, HKEY_CURRENT_USER,
                KEY_QUERY_VALUE, REG_EXPAND_SZ, REG_SZ,
            },
        },
    };

    pub(super) fn read_autostart_value() -> AutostartRegistryObservation {
        let subkey = wide_null(AUTOSTART_REGISTRY_SUBKEY);
        let value_name = wide_null(AUTOSTART_REGISTRY_VALUE_NAME);
        let mut key = HKEY::default();
        let open_status = unsafe {
            RegOpenKeyExW(
                HKEY_CURRENT_USER,
                PCWSTR(subkey.as_ptr()),
                None,
                KEY_QUERY_VALUE,
                &mut key,
            )
        };
        if open_status == ERROR_FILE_NOT_FOUND {
            return AutostartRegistryObservation::Absent;
        }
        if open_status != ERROR_SUCCESS {
            return AutostartRegistryObservation::Unreadable {
                error_code: open_status.0,
            };
        }
        let result = read_value(key, &value_name);
        unsafe {
            let _ = RegCloseKey(key);
        }
        result
    }

    fn read_value(key: HKEY, value_name: &[u16]) -> AutostartRegistryObservation {
        let mut value_type = REG_SZ;
        let mut byte_len = 0u32;
        let first_status = unsafe {
            RegQueryValueExW(
                key,
                PCWSTR(value_name.as_ptr()),
                None,
                Some(&mut value_type),
                None,
                Some(&mut byte_len),
            )
        };
        if first_status == ERROR_FILE_NOT_FOUND {
            return AutostartRegistryObservation::Absent;
        }
        if first_status != ERROR_SUCCESS && first_status != ERROR_MORE_DATA {
            return AutostartRegistryObservation::Unreadable {
                error_code: first_status.0,
            };
        }
        if byte_len % 2 != 0 {
            return AutostartRegistryObservation::Unreadable { error_code: 13 };
        }

        let mut buffer = vec![0u16; (byte_len as usize / 2).saturating_add(1)];
        let mut actual_len = byte_len;
        let second_status = unsafe {
            RegQueryValueExW(
                key,
                PCWSTR(value_name.as_ptr()),
                None,
                Some(&mut value_type),
                Some(buffer.as_mut_ptr().cast()),
                Some(&mut actual_len),
            )
        };
        if second_status != ERROR_SUCCESS {
            return AutostartRegistryObservation::Unreadable {
                error_code: second_status.0,
            };
        }
        let actual_units = (actual_len as usize / 2).min(buffer.len());
        buffer.truncate(actual_units);
        while buffer.last() == Some(&0) {
            buffer.pop();
        }
        let command = String::from_utf16_lossy(&buffer);
        let value_type = if value_type == REG_SZ {
            "REG_SZ".to_string()
        } else if value_type == REG_EXPAND_SZ {
            "REG_EXPAND_SZ".to_string()
        } else {
            format!("REG_TYPE_{}", value_type.0)
        };
        AutostartRegistryObservation::Present {
            value_type,
            command,
        }
    }

    fn wide_null(value: &str) -> Vec<u16> {
        value.encode_utf16().chain(std::iter::once(0)).collect()
    }
}

#[cfg(not(windows))]
mod platform {
    use super::AutostartRegistryObservation;

    pub(super) fn read_autostart_value() -> AutostartRegistryObservation {
        AutostartRegistryObservation::Unsupported
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::install_paths::{resolve_runtime_paths, RuntimePathInputs};
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

    fn installed_fixture(label: &str) -> (PathBuf, RuntimePathResolution) {
        let root = unique_root(label);
        let executable = root
            .join("LocalAppData")
            .join("Programs")
            .join("DesktopUp")
            .join(EXPECTED_EXECUTABLE_NAME);
        fs::create_dir_all(executable.parent().expect("install root"))
            .expect("create isolated install fixture");
        fs::write(&executable, b"fixture").expect("write isolated executable fixture");
        let resolution = resolution_for(&root, executable);
        (root, resolution)
    }

    #[test]
    fn v1_contract_freezes_hkcu_opt_in_and_cleanup_ownership() {
        let root = unique_root("autostart-contract");
        let resolution = resolution_for(&root, root.join("desktop-up.exe"));
        let contract = AutostartContract::v1(&resolution);
        assert_eq!(contract.validate(&resolution), Ok(()));
        assert!(!contract.default_enabled);
        assert!(contract.explicit_opt_in_required);
        assert_eq!(contract.registry.hive, "HKEY_CURRENT_USER");
        assert_eq!(
            contract.registry.subkey,
            r"Software\Microsoft\Windows\CurrentVersion\Run"
        );
        assert_eq!(contract.registry.value_name, "DesktopUp");
        assert_eq!(contract.registry.value_type, "REG_SZ");
        assert!(!contract.registry.remove_parent_key_allowed);
        assert_eq!(contract.implementation_owner, "C-RLC");
        assert_eq!(contract.cleanup.owner, "C-URV");
        assert!(contract.cleanup.required_after_partial_restore_failure);
        assert!(!root.exists());
    }

    #[test]
    fn absent_registration_is_the_healthy_default_off_state() {
        let (root, resolution) = installed_fixture("autostart-absent");
        let report =
            probe_autostart_with_observation(&resolution, AutostartRegistryObservation::Absent);
        assert_eq!(report.readiness, "readyDisabled");
        assert_eq!(report.registration_state, "absent");
        assert!(!report.effective_enabled);
        assert!(report.registration_eligible);
        assert!(report.gaps.is_empty());
        fs::remove_dir_all(root).expect("remove isolated fixture");
    }

    #[test]
    fn exact_installed_command_is_the_only_enabled_state() {
        let (root, resolution) = installed_fixture("autostart-enabled");
        let contract = AutostartContract::v1(&resolution);
        let report = probe_autostart_with_observation(
            &resolution,
            AutostartRegistryObservation::Present {
                value_type: "REG_SZ".to_string(),
                command: contract.expected_command.clone(),
            },
        );
        assert_eq!(report.readiness, "readyEnabled");
        assert_eq!(report.registration_state, "matching");
        assert!(report.effective_enabled);
        assert!(report.gaps.is_empty());
        fs::remove_dir_all(root).expect("remove isolated fixture");
    }

    #[test]
    fn wrong_type_path_or_extra_argument_is_registry_drift() {
        let (root, resolution) = installed_fixture("autostart-drift");
        for (value_type, command) in [
            (
                "REG_EXPAND_SZ",
                AutostartContract::v1(&resolution).expected_command,
            ),
            (
                "REG_SZ",
                r#""C:\workspace\desktop-up.exe" --autostart"#.to_string(),
            ),
            (
                "REG_SZ",
                format!(
                    "{} --unexpected",
                    AutostartContract::v1(&resolution).expected_command
                ),
            ),
        ] {
            let report = probe_autostart_with_observation(
                &resolution,
                AutostartRegistryObservation::Present {
                    value_type: value_type.to_string(),
                    command,
                },
            );
            assert_eq!(report.registration_state, "drifted");
            assert!(!report.effective_enabled);
            assert!(report
                .gaps
                .iter()
                .any(|gap| gap.code == "autostartRegistryDrift"));
        }
        fs::remove_dir_all(root).expect("remove isolated fixture");
    }

    #[test]
    fn development_release_cannot_become_registration_eligible() {
        let root = unique_root("autostart-development");
        let resolution = resolution_for(
            &root,
            root.join("workspace")
                .join("target")
                .join("release")
                .join("desktop-up.exe"),
        );
        let report =
            probe_autostart_with_observation(&resolution, AutostartRegistryObservation::Absent);
        assert_eq!(report.readiness, "developmentOrPortableDisabled");
        assert!(!report.registration_eligible);
        assert!(!report.effective_enabled);
        assert!(report
            .gaps
            .iter()
            .any(|gap| gap.code == "formalInstalledExecutableNotReady"));
        assert!(!root.exists());
    }

    #[test]
    fn stale_registration_is_owned_by_uninstall_recovery() {
        let root = unique_root("autostart-stale");
        let resolution = resolution_for(&root, root.join("desktop-up.exe"));
        let contract = AutostartContract::v1(&resolution);
        let report = probe_autostart_with_observation(
            &resolution,
            AutostartRegistryObservation::Present {
                value_type: "REG_SZ".to_string(),
                command: contract.expected_command,
            },
        );
        assert!(report
            .gaps
            .iter()
            .any(|gap| { gap.code == "staleAutostartRegistration" && gap.owner == "C-URV" }));
        assert!(!report.effective_enabled);
        assert!(!root.exists());
    }

    #[test]
    fn diagnostics_are_read_only_and_single_instance_args_are_strict() {
        let root = unique_root("autostart-serialization");
        let resolution = resolution_for(&root, root.join("desktop-up.exe"));
        let value = serde_json::to_value(probe_autostart_with_observation(
            &resolution,
            AutostartRegistryObservation::Absent,
        ))
        .expect("serialize diagnostics");
        assert_eq!(value["diagnosticName"], "autostart-diagnostics-v1");
        assert_eq!(
            value["contract"]["singleInstance"]["startupArgument"],
            "--autostart"
        );
        assert_eq!(
            value["contract"]["singleInstance"]["additionalArgumentsAllowed"],
            false
        );
        assert_eq!(value["sideEffects"]["writesRegistry"], false);
        assert_eq!(value["sideEffects"]["writesFiles"], false);
        assert_eq!(value["sideEffects"]["changesAutostart"], false);
        assert!(!root.exists());
    }
}
