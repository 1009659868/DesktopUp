use serde::Serialize;
use std::collections::HashSet;

pub const CONTRACT_VERSION: u32 = 1;
pub const CONTRACT_NAME: &str = "install-baseline-v1";
pub const APPLICATION_IDENTIFIER: &str = "com.allmight.desktopup";
pub const INSTALL_ROOT: &str = r"%LOCALAPPDATA%\Programs\DesktopUp";
pub const TARGET_DATA_ROOT: &str = r"%LOCALAPPDATA%\DesktopUp";
pub const CURRENT_RUNTIME_ROOT: &str = r"%APPDATA%\com.allmight.desktopup";

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct DataEntry {
    pub role: &'static str,
    pub relative_path: &'static str,
    pub critical: bool,
    pub removal_policy: &'static str,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct InstallBaselineContract {
    pub contract_version: u32,
    pub contract_name: &'static str,
    pub application_identifier: &'static str,
    pub distribution_scope: &'static str,
    pub license_payload_required: bool,
    pub install_scope: &'static str,
    pub elevation_required: bool,
    pub install_root: &'static str,
    pub mutable_install_writes_allowed: bool,
    pub required_install_entries: Vec<&'static str>,
    pub target_data_root: &'static str,
    pub current_runtime_root: &'static str,
    pub current_runtime_root_writable_until_migration: bool,
    pub data_layout: Vec<DataEntry>,
    pub data_migration_owner: &'static str,
    pub eager_data_move_allowed: bool,
    pub migration_source_retention_required: bool,
    pub migration_rollback_required: bool,
    pub autostart_default_enabled: bool,
    pub autostart_explicit_opt_in_required: bool,
    pub autostart_scope: &'static str,
    pub autostart_registration: &'static str,
    pub autostart_implementation_owner: &'static str,
    pub webview2_runtime: &'static str,
    pub webview2_install_mode: &'static str,
    pub webview2_network_may_be_required_when_missing: bool,
    pub static_vc_runtime: bool,
    pub bundled_sqlite: bool,
    pub final_installer_owner: &'static str,
    pub final_uninstaller_owner: &'static str,
}

impl InstallBaselineContract {
    pub fn v1() -> Self {
        Self {
            contract_version: CONTRACT_VERSION,
            contract_name: CONTRACT_NAME,
            application_identifier: APPLICATION_IDENTIFIER,
            distribution_scope: "privateUse",
            license_payload_required: false,
            install_scope: "currentUser",
            elevation_required: false,
            install_root: INSTALL_ROOT,
            mutable_install_writes_allowed: false,
            required_install_entries: vec![
                "DesktopUp.exe",
                "uninstall.exe",
                "resources/",
            ],
            target_data_root: TARGET_DATA_ROOT,
            current_runtime_root: CURRENT_RUNTIME_ROOT,
            current_runtime_root_writable_until_migration: true,
            data_layout: vec![
                DataEntry {
                    role: "posterState",
                    relative_path: r"data\poster-state-v1.sqlite3",
                    critical: true,
                    removal_policy: "removeOnlyAfterRecoveryGate",
                },
                DataEntry {
                    role: "backups",
                    relative_path: "backups",
                    critical: true,
                    removal_policy: "preserveOnMigrationOrRecoveryFailure",
                },
                DataEntry {
                    role: "shortcutVault",
                    relative_path: r"vault\shortcuts",
                    critical: true,
                    removal_policy: "restoreBeforeRemoval",
                },
                DataEntry {
                    role: "themes",
                    relative_path: "themes",
                    critical: false,
                    removal_policy: "removeAppOwnedCopiesOnly",
                },
                DataEntry {
                    role: "cache",
                    relative_path: "cache",
                    critical: false,
                    removal_policy: "rebuildable",
                },
                DataEntry {
                    role: "logs",
                    relative_path: "logs",
                    critical: false,
                    removal_policy: "appOwned",
                },
                DataEntry {
                    role: "recovery",
                    relative_path: "recovery",
                    critical: true,
                    removal_policy: "preserveOnRecoveryFailure",
                },
            ],
            data_migration_owner: "C-UGM",
            eager_data_move_allowed: false,
            migration_source_retention_required: true,
            migration_rollback_required: true,
            autostart_default_enabled: false,
            autostart_explicit_opt_in_required: true,
            autostart_scope: "currentUser",
            autostart_registration: "currentUserRegistryRun",
            autostart_implementation_owner: "C-RLC",
            webview2_runtime: "evergreen",
            webview2_install_mode: "downloadBootstrapper",
            webview2_network_may_be_required_when_missing: true,
            static_vc_runtime: true,
            bundled_sqlite: true,
            final_installer_owner: "C-RCG",
            final_uninstaller_owner: "C-URV",
        }
    }

    pub fn validate(&self) -> Result<(), Vec<String>> {
        let mut errors = Vec::new();
        if self.contract_version != CONTRACT_VERSION || self.contract_name != CONTRACT_NAME {
            errors.push("unsupported install baseline contract".to_string());
        }
        if self.application_identifier != APPLICATION_IDENTIFIER
            || self.distribution_scope != "privateUse"
            || self.license_payload_required
            || self.install_scope != "currentUser"
            || self.elevation_required
            || self.mutable_install_writes_allowed
        {
            errors.push("install identity, scope, or write ownership drifted".to_string());
        }
        if self.autostart_default_enabled || !self.autostart_explicit_opt_in_required {
            errors.push("autostart must default off and require opt-in".to_string());
        }
        if self.data_migration_owner != "C-UGM" || self.eager_data_move_allowed {
            errors.push("data relocation must remain owned by C-UGM".to_string());
        }
        if !self.static_vc_runtime || !self.bundled_sqlite || self.webview2_install_mode == "skip" {
            errors.push("runtime dependency baseline is incomplete".to_string());
        }
        let roles: HashSet<_> = self.data_layout.iter().map(|entry| entry.role).collect();
        let paths: HashSet<_> = self
            .data_layout
            .iter()
            .map(|entry| entry.relative_path)
            .collect();
        if roles.len() != self.data_layout.len()
            || paths.len() != self.data_layout.len()
            || self
                .data_layout
                .iter()
                .any(|entry| !is_safe_relative_path(entry.relative_path))
        {
            errors.push("data layout contains duplicate or unsafe entries".to_string());
        }
        if errors.is_empty() {
            Ok(())
        } else {
            Err(errors)
        }
    }
}

fn is_safe_relative_path(path: &str) -> bool {
    let separator = char::from(92);
    let normalized = path.replace(separator, "/");
    !path.is_empty()
        && !path.starts_with(separator)
        && !path.starts_with('/')
        && !path.contains(':')
        && normalized
            .split('/')
            .all(|component| !component.is_empty() && component != "." && component != "..")
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn v1_baseline_is_valid_and_serializes_stably() {
        let baseline = InstallBaselineContract::v1();
        assert_eq!(baseline.validate(), Ok(()));
        let value = serde_json::to_value(baseline).expect("serialize install baseline");
        assert_eq!(value["contractName"], "install-baseline-v1");
        assert_eq!(value["installScope"], "currentUser");
        assert_eq!(value["targetDataRoot"], r"%LOCALAPPDATA%\DesktopUp");
        assert_eq!(
            value["currentRuntimeRoot"],
            r"%APPDATA%\com.allmight.desktopup"
        );
        assert_eq!(value["dataMigrationOwner"], "C-UGM");
    }

    #[test]
    fn mutable_data_stays_outside_the_install_root() {
        let baseline = InstallBaselineContract::v1();
        assert!(!baseline.mutable_install_writes_allowed);
        assert_ne!(baseline.install_root, baseline.target_data_root);
        assert!(baseline
            .data_layout
            .iter()
            .all(|entry| is_safe_relative_path(entry.relative_path)));
    }

    #[test]
    fn current_sqlite_authority_is_retained_until_cugm() {
        let baseline = InstallBaselineContract::v1();
        assert!(baseline.current_runtime_root_writable_until_migration);
        assert_eq!(baseline.data_migration_owner, "C-UGM");
        assert!(!baseline.eager_data_move_allowed);
        assert!(baseline.migration_source_retention_required);
        assert!(baseline.migration_rollback_required);
    }

    #[test]
    fn autostart_and_dependencies_fail_safe() {
        let baseline = InstallBaselineContract::v1();
        assert!(!baseline.autostart_default_enabled);
        assert!(baseline.autostart_explicit_opt_in_required);
        assert_eq!(baseline.autostart_implementation_owner, "C-RLC");
        assert_eq!(baseline.webview2_install_mode, "downloadBootstrapper");
        assert!(baseline.static_vc_runtime);
        assert!(baseline.bundled_sqlite);
    }

    #[test]
    fn unsafe_or_duplicate_data_paths_are_rejected() {
        let mut baseline = InstallBaselineContract::v1();
        baseline.data_layout.push(DataEntry {
            role: "cache",
            relative_path: r"..\outside",
            critical: false,
            removal_policy: "rebuildable",
        });
        let errors = baseline.validate().expect_err("invalid baseline");
        assert!(errors
            .iter()
            .any(|error| error.contains("duplicate or unsafe")));
    }
}
