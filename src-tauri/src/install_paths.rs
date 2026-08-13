use std::{
    error::Error,
    fmt::{Display, Formatter},
    path::{Component, Path, PathBuf},
};

use serde::Serialize;
use tauri::Manager;

use crate::install_baseline::{InstallBaselineContract, CONTRACT_NAME, CONTRACT_VERSION};

pub const DIAGNOSTIC_VERSION: u32 = 1;
pub const DIAGNOSTIC_NAME: &str = "install-path-diagnostics-v1";
const CURRENT_POSTER_STATE_FILE: &str = "poster-state-v1.sqlite3";
const CURRENT_PLACEMENT_FILE: &str = "poster-placement-v1.json";
const CURRENT_SNAPSHOT_BRIDGE_FILE: &str = "poster-snapshot-bridge-v1.json";

#[derive(Clone, Debug, Eq, PartialEq)]
pub struct InstallPathResolutionError {
    message: String,
}

impl InstallPathResolutionError {
    fn new(message: impl Into<String>) -> Self {
        Self {
            message: message.into(),
        }
    }
}

impl Display for InstallPathResolutionError {
    fn fmt(&self, formatter: &mut Formatter<'_>) -> std::fmt::Result {
        formatter.write_str(&self.message)
    }
}

impl Error for InstallPathResolutionError {}

#[derive(Clone, Debug, Eq, PartialEq)]
pub struct RuntimePathInputs {
    pub local_data_dir: PathBuf,
    pub current_runtime_root: PathBuf,
    pub executable_path: PathBuf,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ResolvedDataPath {
    pub role: String,
    pub target_path: String,
    pub critical: bool,
    pub removal_policy: String,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct CurrentAuthorityDiagnostic {
    pub status: String,
    pub root: String,
    pub poster_state_path: String,
    pub writable_until_migration: bool,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct MigrationDiagnostic {
    pub status: String,
    pub owner: String,
    pub target_data_root_active: bool,
    pub eager_move_allowed: bool,
    pub source_retention_required: bool,
    pub rollback_required: bool,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ResolutionSideEffects {
    pub creates_directories: bool,
    pub writes_files: bool,
    pub mutates_authority: bool,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct InstallPathDiagnostics {
    pub diagnostic_version: u32,
    pub diagnostic_name: String,
    pub contract_version: u32,
    pub contract_name: String,
    pub install_root: String,
    pub target_data_root: String,
    pub current_runtime_root: String,
    pub current_poster_state_path: String,
    pub target_poster_state_path: String,
    pub executable_path: String,
    pub executable_directory: String,
    pub executable_placement: String,
    pub current_authority: CurrentAuthorityDiagnostic,
    pub migration: MigrationDiagnostic,
    pub target_data_paths: Vec<ResolvedDataPath>,
    pub side_effects: ResolutionSideEffects,
    pub compatibility_warnings: Vec<String>,
}

#[derive(Clone, Debug, Eq, PartialEq)]
pub struct RuntimePathResolution {
    install_root: PathBuf,
    target_data_root: PathBuf,
    current_runtime_root: PathBuf,
    current_poster_state_path: PathBuf,
    target_poster_state_path: PathBuf,
    executable_path: PathBuf,
    diagnostics: InstallPathDiagnostics,
}

impl RuntimePathResolution {
    pub fn install_root(&self) -> &Path {
        &self.install_root
    }

    pub fn target_data_root(&self) -> &Path {
        &self.target_data_root
    }

    pub fn current_runtime_root(&self) -> &Path {
        &self.current_runtime_root
    }

    pub fn current_poster_state_path(&self) -> &Path {
        &self.current_poster_state_path
    }

    pub fn target_poster_state_path(&self) -> &Path {
        &self.target_poster_state_path
    }

    pub fn executable_path(&self) -> &Path {
        &self.executable_path
    }

    pub fn placement_path(&self) -> PathBuf {
        self.current_runtime_root.join(CURRENT_PLACEMENT_FILE)
    }

    pub fn snapshot_bridge_path(&self) -> PathBuf {
        self.current_runtime_root.join(CURRENT_SNAPSHOT_BRIDGE_FILE)
    }

    pub fn poster_state_path(&self) -> PathBuf {
        self.current_poster_state_path.clone()
    }

    pub fn diagnostics(&self) -> &InstallPathDiagnostics {
        &self.diagnostics
    }
}

pub fn resolve_for_app(
    app: &tauri::AppHandle,
) -> Result<RuntimePathResolution, InstallPathResolutionError> {
    let local_data_dir = app
        .path()
        .local_data_dir()
        .map_err(|error| InstallPathResolutionError::new(format!("local data path: {error}")))?;
    let current_runtime_root = app
        .path()
        .app_config_dir()
        .map_err(|error| InstallPathResolutionError::new(format!("app config path: {error}")))?;
    let executable_path = std::env::current_exe()
        .map_err(|error| InstallPathResolutionError::new(format!("executable path: {error}")))?;
    resolve_runtime_paths(RuntimePathInputs {
        local_data_dir,
        current_runtime_root,
        executable_path,
    })
}

pub fn resolve_runtime_paths(
    inputs: RuntimePathInputs,
) -> Result<RuntimePathResolution, InstallPathResolutionError> {
    validate_input_path("local data directory", &inputs.local_data_dir)?;
    validate_input_path("current runtime root", &inputs.current_runtime_root)?;
    validate_input_path("executable path", &inputs.executable_path)?;

    let baseline = InstallBaselineContract::v1();
    if let Err(errors) = baseline.validate() {
        return Err(InstallPathResolutionError::new(format!(
            "install baseline is invalid: {}",
            errors.join("; ")
        )));
    }

    let install_root = inputs.local_data_dir.join("Programs").join("DesktopUp");
    let target_data_root = inputs.local_data_dir.join("DesktopUp");
    let current_runtime_root = inputs.current_runtime_root;
    let executable_path = inputs.executable_path;

    if path_is_within(&target_data_root, &install_root)
        || path_is_within(&current_runtime_root, &install_root)
    {
        return Err(InstallPathResolutionError::new(
            "mutable data roots must remain outside the install root",
        ));
    }
    if paths_equal(&target_data_root, &current_runtime_root) {
        return Err(InstallPathResolutionError::new(
            "target data root cannot become authoritative before C-UGM",
        ));
    }

    let target_data_paths = baseline
        .data_layout
        .iter()
        .map(|entry| {
            let target_path = join_contract_relative(&target_data_root, entry.relative_path);
            if !path_is_within(&target_path, &target_data_root) {
                return Err(InstallPathResolutionError::new(format!(
                    "target data role escaped its root: {}",
                    entry.role
                )));
            }
            Ok(ResolvedDataPath {
                role: entry.role.to_string(),
                target_path: path_to_string(&target_path),
                critical: entry.critical,
                removal_policy: entry.removal_policy.to_string(),
            })
        })
        .collect::<Result<Vec<_>, _>>()?;
    let target_poster_state_path = baseline
        .data_layout
        .iter()
        .find(|entry| entry.role == "posterState")
        .map(|entry| join_contract_relative(&target_data_root, entry.relative_path))
        .ok_or_else(|| InstallPathResolutionError::new("posterState target path is missing"))?;
    let current_poster_state_path = current_runtime_root.join(CURRENT_POSTER_STATE_FILE);
    let executable_directory = executable_path
        .parent()
        .ok_or_else(|| InstallPathResolutionError::new("executable directory is unavailable"))?
        .to_path_buf();
    let executable_matches_target = paths_equal(&executable_directory, &install_root);
    let compatibility_warnings = if executable_matches_target {
        Vec::new()
    } else {
        vec!["executableOutsideTargetInstallRoot".to_string()]
    };

    let diagnostics = InstallPathDiagnostics {
        diagnostic_version: DIAGNOSTIC_VERSION,
        diagnostic_name: DIAGNOSTIC_NAME.to_string(),
        contract_version: CONTRACT_VERSION,
        contract_name: CONTRACT_NAME.to_string(),
        install_root: path_to_string(&install_root),
        target_data_root: path_to_string(&target_data_root),
        current_runtime_root: path_to_string(&current_runtime_root),
        current_poster_state_path: path_to_string(&current_poster_state_path),
        target_poster_state_path: path_to_string(&target_poster_state_path),
        executable_path: path_to_string(&executable_path),
        executable_directory: path_to_string(&executable_directory),
        executable_placement: if executable_matches_target {
            "matchesTargetInstallRoot".to_string()
        } else {
            "outsideTargetInstallRoot".to_string()
        },
        current_authority: CurrentAuthorityDiagnostic {
            status: "preMigrationAuthoritative".to_string(),
            root: path_to_string(&current_runtime_root),
            poster_state_path: path_to_string(&current_poster_state_path),
            writable_until_migration: baseline.current_runtime_root_writable_until_migration,
        },
        migration: MigrationDiagnostic {
            status: "preMigrationAuthoritative".to_string(),
            owner: baseline.data_migration_owner.to_string(),
            target_data_root_active: false,
            eager_move_allowed: baseline.eager_data_move_allowed,
            source_retention_required: baseline.migration_source_retention_required,
            rollback_required: baseline.migration_rollback_required,
        },
        target_data_paths,
        side_effects: ResolutionSideEffects {
            creates_directories: false,
            writes_files: false,
            mutates_authority: false,
        },
        compatibility_warnings,
    };

    Ok(RuntimePathResolution {
        install_root,
        target_data_root,
        current_runtime_root,
        current_poster_state_path,
        target_poster_state_path,
        executable_path,
        diagnostics,
    })
}

fn validate_input_path(label: &str, path: &Path) -> Result<(), InstallPathResolutionError> {
    let clean = path.is_absolute()
        && path
            .components()
            .all(|component| !matches!(component, Component::CurDir | Component::ParentDir));
    if clean {
        Ok(())
    } else {
        Err(InstallPathResolutionError::new(format!(
            "{label} must be a clean absolute path"
        )))
    }
}

fn join_contract_relative(root: &Path, relative: &str) -> PathBuf {
    relative
        .split(['/', '\\'])
        .filter(|component| !component.is_empty())
        .fold(root.to_path_buf(), |path, component| path.join(component))
}

fn path_to_string(path: &Path) -> String {
    path.to_string_lossy().into_owned()
}

fn normalized_path_key(path: &Path) -> String {
    let mut key = path_to_string(path).replace('\\', "/");
    #[cfg(windows)]
    if let Some(stripped) = key.strip_prefix("//?/") {
        key = stripped.to_string();
    }
    while key.len() > 1 && key.ends_with('/') {
        key.pop();
    }
    #[cfg(windows)]
    key.make_ascii_lowercase();
    key
}

fn paths_equal(left: &Path, right: &Path) -> bool {
    normalized_path_key(left) == normalized_path_key(right)
}

fn path_is_within(path: &Path, root: &Path) -> bool {
    let path = normalized_path_key(path);
    let root = normalized_path_key(root);
    path == root || path.starts_with(&format!("{root}/"))
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::time::{SystemTime, UNIX_EPOCH};

    fn sample_inputs() -> RuntimePathInputs {
        let root = std::env::temp_dir().join("desktop-up-install-path-tests");
        RuntimePathInputs {
            local_data_dir: root.join("LocalAppData"),
            current_runtime_root: root.join("RoamingAppData").join("com.allmight.desktopup"),
            executable_path: root
                .join("workspace")
                .join("target")
                .join("release")
                .join("desktop-up.exe"),
        }
    }

    #[test]
    fn resolves_all_roles_without_changing_the_current_authority() {
        let inputs = sample_inputs();
        let resolution = resolve_runtime_paths(inputs.clone()).expect("resolve runtime paths");
        assert_eq!(
            resolution.install_root(),
            inputs.local_data_dir.join("Programs").join("DesktopUp")
        );
        assert_eq!(
            resolution.target_data_root(),
            inputs.local_data_dir.join("DesktopUp")
        );
        assert_eq!(
            resolution.current_runtime_root(),
            inputs.current_runtime_root
        );
        assert_eq!(
            resolution.poster_state_path(),
            inputs.current_runtime_root.join("poster-state-v1.sqlite3")
        );
        assert_eq!(resolution.diagnostics().target_data_paths.len(), 7);
        assert_eq!(
            resolution.diagnostics().current_authority.status,
            "preMigrationAuthoritative"
        );
        assert!(!resolution.diagnostics().migration.target_data_root_active);
    }

    #[test]
    fn serialization_is_versioned_camel_case_and_read_only() {
        let resolution =
            resolve_runtime_paths(sample_inputs()).expect("resolve runtime diagnostics");
        let value = serde_json::to_value(resolution.diagnostics()).expect("serialize diagnostics");
        assert_eq!(value["diagnosticName"], "install-path-diagnostics-v1");
        assert_eq!(value["contractName"], "install-baseline-v1");
        assert_eq!(
            value["currentAuthority"]["status"],
            "preMigrationAuthoritative"
        );
        assert_eq!(value["migration"]["owner"], "C-UGM");
        assert_eq!(value["sideEffects"]["createsDirectories"], false);
        assert_eq!(value["sideEffects"]["writesFiles"], false);
        assert_eq!(value["sideEffects"]["mutatesAuthority"], false);
    }

    #[test]
    fn resolving_paths_does_not_materialize_the_target_root() {
        let unique = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("system clock")
            .as_nanos();
        let root = std::env::temp_dir().join(format!("desktop-up-cibc02-{unique}"));
        let inputs = RuntimePathInputs {
            local_data_dir: root.join("LocalAppData"),
            current_runtime_root: root.join("Roaming").join("com.allmight.desktopup"),
            executable_path: root.join("bin").join("desktop-up.exe"),
        };
        assert!(!root.exists());
        let resolution = resolve_runtime_paths(inputs).expect("resolve without side effects");
        assert!(!root.exists());
        assert!(!resolution.target_data_root().exists());
    }

    #[test]
    fn rejects_relative_or_prematurely_activated_paths() {
        let mut relative = sample_inputs();
        relative.local_data_dir = PathBuf::from("relative-local-data");
        assert!(resolve_runtime_paths(relative)
            .expect_err("relative path must fail")
            .to_string()
            .contains("clean absolute"));

        let mut activated = sample_inputs();
        activated.current_runtime_root = activated.local_data_dir.join("DesktopUp");
        assert!(resolve_runtime_paths(activated)
            .expect_err("premature target activation must fail")
            .to_string()
            .contains("before C-UGM"));
    }

    #[test]
    fn development_release_is_diagnosed_outside_the_target_install_root() {
        let resolution = resolve_runtime_paths(sample_inputs()).expect("resolve runtime paths");
        assert_eq!(
            resolution.diagnostics().executable_placement,
            "outsideTargetInstallRoot"
        );
        assert_eq!(
            resolution.diagnostics().compatibility_warnings,
            vec!["executableOutsideTargetInstallRoot"]
        );
    }

    #[cfg(windows)]
    #[test]
    fn windows_extended_prefix_does_not_create_a_false_placement_warning() {
        assert!(paths_equal(
            Path::new(r"\\?\C:\Users\Test\AppData\Local\Programs\DesktopUp"),
            Path::new(r"C:\Users\Test\AppData\Local\Programs\DesktopUp")
        ));
    }
}
