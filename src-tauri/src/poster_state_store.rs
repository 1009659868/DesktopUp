use std::{
    fmt,
    fs::{self, OpenOptions},
    io::Write,
    path::{Path, PathBuf},
    sync::Mutex,
    time::{Duration, SystemTime, UNIX_EPOCH},
};

use rusqlite::{params, Connection, OptionalExtension, TransactionBehavior};
use serde::{Deserialize, Serialize};
use serde_json::{json, Value};
use sha2::{Digest, Sha256};

use crate::poster_contract::{
    summarize_poster_contract_errors, validate_poster_contract_v1, POSTER_CONTRACT_VERSION,
};

pub const POSTER_STATE_SCHEMA_VERSION: i64 = 1;
pub const POSTER_STATE_APPLICATION_ID: i64 = 0x4455_5031;
pub const MAX_POSTER_STATE_REVISION: u64 = 9_007_199_254_740_991;
pub const LEGACY_POSTER_BACKUP_VERSION: u64 = 1;
pub const LEGACY_POSTER_BACKUP_DIRECTORY: &str = "poster-state-backups";

const POSTER_STATE_SCHEMA_V1: &str = r#"
CREATE TABLE poster_state (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    contract_version INTEGER NOT NULL CHECK (contract_version > 0),
    revision INTEGER NOT NULL CHECK (revision >= 0),
    selected_poster_id TEXT NOT NULL CHECK (length(trim(selected_poster_id)) > 0),
    snapshot_json TEXT NOT NULL CHECK (json_valid(snapshot_json))
) STRICT;
"#;

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum PosterStateStoreErrorCode {
    CreateDirectory,
    OpenDatabase,
    ConfigureDatabase,
    ForeignDatabase,
    UnsupportedSchema,
    InitializeSchema,
    QueryDatabase,
    WriteDatabase,
    RepositoryLock,
    BackupUnavailable,
    CreateBackupDirectory,
    ReadBackup,
    WriteBackup,
    BackupConflict,
}

impl PosterStateStoreErrorCode {
    pub const fn as_str(self) -> &'static str {
        match self {
            Self::CreateDirectory => "create_directory_failed",
            Self::OpenDatabase => "open_database_failed",
            Self::ConfigureDatabase => "configure_database_failed",
            Self::ForeignDatabase => "foreign_database",
            Self::UnsupportedSchema => "unsupported_schema_version",
            Self::InitializeSchema => "initialize_schema_failed",
            Self::QueryDatabase => "query_database_failed",
            Self::WriteDatabase => "write_database_failed",
            Self::RepositoryLock => "repository_lock_failed",
            Self::BackupUnavailable => "backup_unavailable",
            Self::CreateBackupDirectory => "create_backup_directory_failed",
            Self::ReadBackup => "read_backup_failed",
            Self::WriteBackup => "write_backup_failed",
            Self::BackupConflict => "backup_conflict",
        }
    }
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PosterStateStoreError {
    pub code: PosterStateStoreErrorCode,
    pub message: String,
}

impl PosterStateStoreError {
    fn new(code: PosterStateStoreErrorCode, message: impl Into<String>) -> Self {
        Self {
            code,
            message: message.into(),
        }
    }
}

impl fmt::Display for PosterStateStoreError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "{}: {}", self.code.as_str(), self.message)
    }
}

impl std::error::Error for PosterStateStoreError {}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum PosterStateLoadRejectionCode {
    CorruptJson,
    InvalidContract,
    UnsupportedContract,
    MetadataMismatch,
}

impl PosterStateLoadRejectionCode {
    pub const fn as_str(self) -> &'static str {
        match self {
            Self::CorruptJson => "corrupt_json",
            Self::InvalidContract => "invalid_contract",
            Self::UnsupportedContract => "unsupported_contract",
            Self::MetadataMismatch => "metadata_mismatch",
        }
    }
}

#[derive(Clone, Debug, PartialEq, Serialize)]
#[serde(
    tag = "status",
    rename_all = "camelCase",
    rename_all_fields = "camelCase"
)]
pub enum PosterStateLoadResult {
    NoData,
    Loaded {
        revision: u64,
        snapshot: Value,
    },
    Rejected {
        code: PosterStateLoadRejectionCode,
        message: String,
    },
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum PosterStateMutationRejectionCode {
    InvalidSnapshot,
    UnsupportedContract,
    RevisionExhausted,
    CurrentStateRejected,
}

impl PosterStateMutationRejectionCode {
    pub const fn as_str(self) -> &'static str {
        match self {
            Self::InvalidSnapshot => "invalid_snapshot",
            Self::UnsupportedContract => "unsupported_contract",
            Self::RevisionExhausted => "revision_exhausted",
            Self::CurrentStateRejected => "current_state_rejected",
        }
    }
}

#[derive(Clone, Debug, PartialEq, Serialize)]
#[serde(
    tag = "status",
    rename_all = "camelCase",
    rename_all_fields = "camelCase"
)]
pub enum PosterStateMutationResult {
    Stored {
        previous_revision: Option<u64>,
        revision: u64,
        snapshot: Value,
    },
    Conflict {
        expected_revision: Option<u64>,
        actual_revision: Option<u64>,
    },
    Rejected {
        code: PosterStateMutationRejectionCode,
        message: String,
    },
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum LegacyPosterSourceKind {
    SharedJson,
    LocalStorage,
}

#[derive(Clone, Debug, PartialEq, Eq, Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct LegacyPosterSource {
    pub kind: LegacyPosterSourceKind,
    pub key: String,
}

#[derive(Clone, Debug, PartialEq, Eq, Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct LegacyAdapterDiagnostic {
    pub path: String,
    pub code: String,
    pub message: String,
}

#[derive(Clone, Debug, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct LegacyPosterMigrationRequest {
    pub source: LegacyPosterSource,
    pub legacy_snapshot: Value,
    pub adapted_snapshot: Option<Value>,
    #[serde(default)]
    pub adapter_migrations: Vec<Value>,
    #[serde(default)]
    pub adapter_errors: Vec<LegacyAdapterDiagnostic>,
}

#[derive(Clone, Debug, PartialEq, Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct LegacyPosterBackupRecord {
    backup_version: u64,
    source: LegacyPosterSource,
    source_fingerprint: String,
    legacy_snapshot: Value,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum LegacyPosterMigrationRejectionCode {
    InvalidSource,
    InvalidLegacySnapshot,
    AdapterFailed,
    ScopeViolation,
    InvalidAdaptedSnapshot,
    CurrentStateRejected,
}

impl LegacyPosterMigrationRejectionCode {
    pub const fn as_str(self) -> &'static str {
        match self {
            Self::InvalidSource => "invalid_source",
            Self::InvalidLegacySnapshot => "invalid_legacy_snapshot",
            Self::AdapterFailed => "adapter_failed",
            Self::ScopeViolation => "scope_violation",
            Self::InvalidAdaptedSnapshot => "invalid_adapted_snapshot",
            Self::CurrentStateRejected => "current_state_rejected",
        }
    }
}

#[derive(Clone, Debug, PartialEq, Serialize)]
#[serde(
    tag = "status",
    rename_all = "camelCase",
    rename_all_fields = "camelCase"
)]
pub enum LegacyPosterMigrationResult {
    Migrated {
        source: LegacyPosterSource,
        source_fingerprint: String,
        backup_path: String,
        revision: u64,
        snapshot: Value,
        adapter_migrations: Vec<Value>,
    },
    SkippedExistingState {
        source: LegacyPosterSource,
        source_fingerprint: String,
        backup_path: Option<String>,
        revision: u64,
    },
    Rejected {
        source: LegacyPosterSource,
        source_fingerprint: Option<String>,
        backup_path: Option<String>,
        code: LegacyPosterMigrationRejectionCode,
        message: String,
        adapter_errors: Vec<LegacyAdapterDiagnostic>,
    },
}

struct StoredRow {
    contract_version: i64,
    revision: i64,
    selected_poster_id: String,
    snapshot_json: String,
}

struct PreparedSnapshot {
    contract_version: u64,
    revision: u64,
    selected_poster_id: String,
    snapshot_json: String,
    snapshot: Value,
}

pub struct PosterStateRepository {
    path: Option<PathBuf>,
    connection: Mutex<Connection>,
}

impl PosterStateRepository {
    pub fn open(path: PathBuf) -> Result<Self, PosterStateStoreError> {
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent).map_err(|error| {
                PosterStateStoreError::new(
                    PosterStateStoreErrorCode::CreateDirectory,
                    format!("failed to create {}: {error}", parent.display()),
                )
            })?;
        }

        let mut connection = Connection::open(&path).map_err(|error| {
            PosterStateStoreError::new(
                PosterStateStoreErrorCode::OpenDatabase,
                format!("failed to open {}: {error}", path.display()),
            )
        })?;
        configure_connection(&connection, true)?;
        initialize_schema(&mut connection)?;
        Ok(Self {
            path: Some(path),
            connection: Mutex::new(connection),
        })
    }

    pub fn path(&self) -> Option<&Path> {
        self.path.as_deref()
    }

    pub fn schema_version(&self) -> Result<i64, PosterStateStoreError> {
        let connection = self.connection.lock().map_err(|error| {
            PosterStateStoreError::new(PosterStateStoreErrorCode::RepositoryLock, error.to_string())
        })?;
        read_pragma_i64(&connection, "user_version").map_err(|error| {
            PosterStateStoreError::new(
                PosterStateStoreErrorCode::ConfigureDatabase,
                error.to_string(),
            )
        })
    }

    pub fn load(&self) -> Result<PosterStateLoadResult, PosterStateStoreError> {
        let connection = self.connection.lock().map_err(|error| {
            PosterStateStoreError::new(PosterStateStoreErrorCode::RepositoryLock, error.to_string())
        })?;
        load_from_connection(&connection).map_err(|error| {
            PosterStateStoreError::new(PosterStateStoreErrorCode::QueryDatabase, error.to_string())
        })
    }

    pub fn replace_with_expected_revision(
        &self,
        expected_revision: Option<u64>,
        snapshot: Value,
    ) -> Result<PosterStateMutationResult, PosterStateStoreError> {
        let mut connection = self.connection.lock().map_err(|error| {
            PosterStateStoreError::new(PosterStateStoreErrorCode::RepositoryLock, error.to_string())
        })?;
        let transaction = connection
            .transaction_with_behavior(TransactionBehavior::Immediate)
            .map_err(|error| {
                PosterStateStoreError::new(
                    PosterStateStoreErrorCode::WriteDatabase,
                    error.to_string(),
                )
            })?;

        let current = load_from_connection(&transaction).map_err(|error| {
            PosterStateStoreError::new(PosterStateStoreErrorCode::QueryDatabase, error.to_string())
        })?;
        let actual_revision = match current {
            PosterStateLoadResult::NoData => None,
            PosterStateLoadResult::Loaded { revision, .. } => Some(revision),
            PosterStateLoadResult::Rejected { code, message } => {
                return Ok(PosterStateMutationResult::Rejected {
                    code: PosterStateMutationRejectionCode::CurrentStateRejected,
                    message: format!("{}: {message}", code.as_str()),
                });
            }
        };

        if expected_revision != actual_revision {
            return Ok(PosterStateMutationResult::Conflict {
                expected_revision,
                actual_revision,
            });
        }

        let next_revision = match actual_revision {
            None => 0,
            Some(MAX_POSTER_STATE_REVISION) => {
                return Ok(PosterStateMutationResult::Rejected {
                    code: PosterStateMutationRejectionCode::RevisionExhausted,
                    message: "poster state revision reached the JavaScript-safe integer limit"
                        .to_string(),
                });
            }
            Some(revision) => revision + 1,
        };
        let prepared = match prepare_snapshot(snapshot, next_revision) {
            Ok(prepared) => prepared,
            Err((code, message)) => {
                return Ok(PosterStateMutationResult::Rejected { code, message });
            }
        };

        let changed = match actual_revision {
            None => transaction.execute(
                "INSERT INTO poster_state (
                    id, contract_version, revision, selected_poster_id, snapshot_json
                 ) VALUES (1, ?1, ?2, ?3, ?4)",
                params![
                    prepared.contract_version as i64,
                    prepared.revision as i64,
                    prepared.selected_poster_id,
                    prepared.snapshot_json
                ],
            ),
            Some(revision) => transaction.execute(
                "UPDATE poster_state
                 SET contract_version = ?1,
                     revision = ?2,
                     selected_poster_id = ?3,
                     snapshot_json = ?4
                 WHERE id = 1 AND revision = ?5",
                params![
                    prepared.contract_version as i64,
                    prepared.revision as i64,
                    prepared.selected_poster_id,
                    prepared.snapshot_json,
                    revision as i64
                ],
            ),
        }
        .map_err(|error| {
            PosterStateStoreError::new(PosterStateStoreErrorCode::WriteDatabase, error.to_string())
        })?;
        if changed != 1 {
            return Ok(PosterStateMutationResult::Conflict {
                expected_revision,
                actual_revision,
            });
        }
        transaction.commit().map_err(|error| {
            PosterStateStoreError::new(PosterStateStoreErrorCode::WriteDatabase, error.to_string())
        })?;
        Ok(PosterStateMutationResult::Stored {
            previous_revision: actual_revision,
            revision: prepared.revision,
            snapshot: prepared.snapshot,
        })
    }

    pub fn migrate_legacy(
        &self,
        request: LegacyPosterMigrationRequest,
    ) -> Result<LegacyPosterMigrationResult, PosterStateStoreError> {
        let LegacyPosterMigrationRequest {
            source,
            legacy_snapshot,
            adapted_snapshot,
            adapter_migrations,
            adapter_errors,
        } = request;

        if source.key.trim().is_empty() {
            return Ok(legacy_migration_rejected(
                &source,
                None,
                None,
                LegacyPosterMigrationRejectionCode::InvalidSource,
                "legacy source key must be a non-empty string".to_string(),
                &adapter_errors,
            ));
        }
        if !is_legacy_a0_snapshot(&legacy_snapshot) {
            return Ok(legacy_migration_rejected(
                &source,
                None,
                None,
                LegacyPosterMigrationRejectionCode::InvalidLegacySnapshot,
                "legacy source must be an unversioned object with a posters array".to_string(),
                &adapter_errors,
            ));
        }

        let source_fingerprint = legacy_source_fingerprint(&source, &legacy_snapshot)?;
        let backup_target = self.path.as_ref().and_then(|path| {
            path.parent().map(|parent| {
                parent
                    .join(LEGACY_POSTER_BACKUP_DIRECTORY)
                    .join(format!("legacy-{source_fingerprint}.json"))
            })
        });
        let backup_record = LegacyPosterBackupRecord {
            backup_version: LEGACY_POSTER_BACKUP_VERSION,
            source: source.clone(),
            source_fingerprint: source_fingerprint.clone(),
            legacy_snapshot: legacy_snapshot.clone(),
        };

        let mut connection = self.connection.lock().map_err(|error| {
            PosterStateStoreError::new(PosterStateStoreErrorCode::RepositoryLock, error.to_string())
        })?;
        let transaction = connection
            .transaction_with_behavior(TransactionBehavior::Immediate)
            .map_err(|error| {
                PosterStateStoreError::new(
                    PosterStateStoreErrorCode::WriteDatabase,
                    error.to_string(),
                )
            })?;
        match load_from_connection(&transaction).map_err(|error| {
            PosterStateStoreError::new(PosterStateStoreErrorCode::QueryDatabase, error.to_string())
        })? {
            PosterStateLoadResult::Loaded { revision, .. } => {
                let existing_backup_path = match backup_target.as_deref() {
                    Some(path) if path.exists() => {
                        verify_legacy_backup(path, &backup_record)?;
                        Some(path_to_diagnostic_string(path))
                    }
                    _ => None,
                };
                return Ok(LegacyPosterMigrationResult::SkippedExistingState {
                    source,
                    source_fingerprint,
                    backup_path: existing_backup_path,
                    revision,
                });
            }
            PosterStateLoadResult::Rejected { code, message } => {
                return Ok(legacy_migration_rejected(
                    &source,
                    Some(&source_fingerprint),
                    None,
                    LegacyPosterMigrationRejectionCode::CurrentStateRejected,
                    format!("{}: {message}", code.as_str()),
                    &adapter_errors,
                ));
            }
            PosterStateLoadResult::NoData => {}
        }

        let backup_target = backup_target.ok_or_else(|| {
            PosterStateStoreError::new(
                PosterStateStoreErrorCode::BackupUnavailable,
                "legacy migration requires a persistent repository path",
            )
        })?;
        retain_legacy_backup(&backup_target, &backup_record)?;
        let backup_path = path_to_diagnostic_string(&backup_target);

        if !adapter_errors.is_empty() || adapted_snapshot.is_none() {
            return Ok(legacy_migration_rejected(
                &source,
                Some(&source_fingerprint),
                Some(&backup_target),
                LegacyPosterMigrationRejectionCode::AdapterFailed,
                "legacy adapter did not produce a formal snapshot".to_string(),
                &adapter_errors,
            ));
        }
        let adapted_snapshot = adapted_snapshot.expect("checked adapted snapshot presence");
        if let Err(message) =
            validate_first_poster_migration_scope(&legacy_snapshot, &adapted_snapshot)
        {
            return Ok(legacy_migration_rejected(
                &source,
                Some(&source_fingerprint),
                Some(&backup_target),
                LegacyPosterMigrationRejectionCode::ScopeViolation,
                message,
                &adapter_errors,
            ));
        }
        let prepared = match prepare_snapshot(adapted_snapshot, 0) {
            Ok(prepared) => prepared,
            Err((code, message)) => {
                return Ok(legacy_migration_rejected(
                    &source,
                    Some(&source_fingerprint),
                    Some(&backup_target),
                    LegacyPosterMigrationRejectionCode::InvalidAdaptedSnapshot,
                    format!("{}: {message}", code.as_str()),
                    &adapter_errors,
                ));
            }
        };

        let changed = transaction
            .execute(
                "INSERT INTO poster_state (
                    id, contract_version, revision, selected_poster_id, snapshot_json
                 ) VALUES (1, ?1, ?2, ?3, ?4)",
                params![
                    prepared.contract_version as i64,
                    prepared.revision as i64,
                    prepared.selected_poster_id,
                    prepared.snapshot_json
                ],
            )
            .map_err(|error| {
                PosterStateStoreError::new(
                    PosterStateStoreErrorCode::WriteDatabase,
                    error.to_string(),
                )
            })?;
        if changed != 1 {
            return Err(PosterStateStoreError::new(
                PosterStateStoreErrorCode::WriteDatabase,
                format!("legacy migration insert changed {changed} rows"),
            ));
        }
        transaction.commit().map_err(|error| {
            PosterStateStoreError::new(PosterStateStoreErrorCode::WriteDatabase, error.to_string())
        })?;

        Ok(LegacyPosterMigrationResult::Migrated {
            source,
            source_fingerprint,
            backup_path,
            revision: prepared.revision,
            snapshot: prepared.snapshot,
            adapter_migrations,
        })
    }

    #[cfg(test)]
    fn open_in_memory() -> Result<Self, PosterStateStoreError> {
        let mut connection = Connection::open_in_memory().map_err(|error| {
            PosterStateStoreError::new(PosterStateStoreErrorCode::OpenDatabase, error.to_string())
        })?;
        configure_connection(&connection, false)?;
        initialize_schema(&mut connection)?;
        Ok(Self {
            path: None,
            connection: Mutex::new(connection),
        })
    }
}

fn legacy_migration_rejected(
    source: &LegacyPosterSource,
    source_fingerprint: Option<&str>,
    backup_path: Option<&Path>,
    code: LegacyPosterMigrationRejectionCode,
    message: String,
    adapter_errors: &[LegacyAdapterDiagnostic],
) -> LegacyPosterMigrationResult {
    LegacyPosterMigrationResult::Rejected {
        source: source.clone(),
        source_fingerprint: source_fingerprint.map(str::to_string),
        backup_path: backup_path.map(path_to_diagnostic_string),
        code,
        message,
        adapter_errors: adapter_errors.to_vec(),
    }
}

fn is_legacy_a0_snapshot(snapshot: &Value) -> bool {
    snapshot.as_object().is_some_and(|object| {
        !object.contains_key("contractVersion")
            && object.get("posters").is_some_and(Value::is_array)
    })
}

fn legacy_source_fingerprint(
    source: &LegacyPosterSource,
    legacy_snapshot: &Value,
) -> Result<String, PosterStateStoreError> {
    let material = json!({
        "source": source,
        "legacySnapshot": legacy_snapshot,
    });
    let bytes = serde_json::to_vec(&material).map_err(|error| {
        PosterStateStoreError::new(PosterStateStoreErrorCode::WriteBackup, error.to_string())
    })?;
    let digest = Sha256::digest(bytes);
    Ok(digest
        .iter()
        .map(|byte| format!("{byte:02x}"))
        .collect::<String>())
}

fn validate_first_poster_migration_scope(
    legacy_snapshot: &Value,
    adapted_snapshot: &Value,
) -> Result<(), String> {
    let legacy_posters = legacy_snapshot
        .get("posters")
        .and_then(Value::as_array)
        .ok_or_else(|| "legacy source does not contain a posters array".to_string())?;
    let first_legacy_poster = legacy_posters
        .first()
        .ok_or_else(|| "legacy source does not contain a first poster".to_string())?;
    let adapted_posters = adapted_snapshot
        .get("posters")
        .and_then(Value::as_array)
        .ok_or_else(|| "adapted snapshot does not contain a posters array".to_string())?;
    if adapted_posters.len() != 1 {
        return Err(format!(
            "legacy migration must adapt exactly the first poster, found {} adapted posters",
            adapted_posters.len()
        ));
    }

    let expected_id = first_legacy_poster
        .get("id")
        .and_then(Value::as_str)
        .filter(|id| !id.trim().is_empty())
        .unwrap_or("legacy-poster-1");
    let actual_id = adapted_posters[0]
        .get("id")
        .and_then(Value::as_str)
        .unwrap_or_default();
    if actual_id != expected_id {
        return Err(format!(
            "adapted poster id '{actual_id}' does not match first legacy poster id '{expected_id}'"
        ));
    }
    Ok(())
}

fn path_to_diagnostic_string(path: &Path) -> String {
    path.to_string_lossy().into_owned()
}

fn verify_legacy_backup(
    path: &Path,
    expected: &LegacyPosterBackupRecord,
) -> Result<(), PosterStateStoreError> {
    let bytes = fs::read(path).map_err(|error| {
        PosterStateStoreError::new(
            PosterStateStoreErrorCode::ReadBackup,
            format!("failed to read {}: {error}", path.display()),
        )
    })?;
    let actual = serde_json::from_slice::<LegacyPosterBackupRecord>(&bytes).map_err(|error| {
        PosterStateStoreError::new(
            PosterStateStoreErrorCode::BackupConflict,
            format!("invalid legacy backup {}: {error}", path.display()),
        )
    })?;
    if &actual != expected {
        return Err(PosterStateStoreError::new(
            PosterStateStoreErrorCode::BackupConflict,
            format!(
                "legacy backup {} does not match its source fingerprint",
                path.display()
            ),
        ));
    }
    Ok(())
}

fn retain_legacy_backup(
    target: &Path,
    record: &LegacyPosterBackupRecord,
) -> Result<(), PosterStateStoreError> {
    let directory = target.parent().ok_or_else(|| {
        PosterStateStoreError::new(
            PosterStateStoreErrorCode::BackupUnavailable,
            "legacy backup target has no parent directory",
        )
    })?;
    fs::create_dir_all(directory).map_err(|error| {
        PosterStateStoreError::new(
            PosterStateStoreErrorCode::CreateBackupDirectory,
            format!("failed to create {}: {error}", directory.display()),
        )
    })?;
    if target.exists() {
        return verify_legacy_backup(target, record);
    }

    let bytes = serde_json::to_vec_pretty(record).map_err(|error| {
        PosterStateStoreError::new(PosterStateStoreErrorCode::WriteBackup, error.to_string())
    })?;
    let unique = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_nanos();
    let temporary = directory.join(format!(
        ".legacy-backup-{}-{unique}.tmp",
        std::process::id()
    ));
    let write_result = (|| -> std::io::Result<()> {
        let mut file = OpenOptions::new()
            .create_new(true)
            .write(true)
            .open(&temporary)?;
        file.write_all(&bytes)?;
        file.sync_all()
    })();
    if let Err(error) = write_result {
        let _ = fs::remove_file(&temporary);
        return Err(PosterStateStoreError::new(
            PosterStateStoreErrorCode::WriteBackup,
            format!("failed to write {}: {error}", temporary.display()),
        ));
    }

    if let Err(error) = fs::rename(&temporary, target) {
        if target.exists() {
            let verification = verify_legacy_backup(target, record);
            let _ = fs::remove_file(&temporary);
            return verification;
        }
        let _ = fs::remove_file(&temporary);
        return Err(PosterStateStoreError::new(
            PosterStateStoreErrorCode::WriteBackup,
            format!(
                "failed to atomically move {} to {}: {error}",
                temporary.display(),
                target.display()
            ),
        ));
    }
    Ok(())
}

fn configure_connection(
    connection: &Connection,
    persistent: bool,
) -> Result<(), PosterStateStoreError> {
    connection
        .busy_timeout(Duration::from_secs(5))
        .map_err(|error| {
            PosterStateStoreError::new(
                PosterStateStoreErrorCode::ConfigureDatabase,
                error.to_string(),
            )
        })?;
    connection
        .pragma_update(None, "foreign_keys", "ON")
        .and_then(|_| connection.pragma_update(None, "synchronous", "FULL"))
        .map_err(|error| {
            PosterStateStoreError::new(
                PosterStateStoreErrorCode::ConfigureDatabase,
                error.to_string(),
            )
        })?;
    if persistent {
        connection
            .pragma_update(None, "journal_mode", "WAL")
            .map_err(|error| {
                PosterStateStoreError::new(
                    PosterStateStoreErrorCode::ConfigureDatabase,
                    error.to_string(),
                )
            })?;
    }
    Ok(())
}

fn initialize_schema(connection: &mut Connection) -> Result<(), PosterStateStoreError> {
    let application_id = read_pragma_i64(connection, "application_id").map_err(|error| {
        PosterStateStoreError::new(
            PosterStateStoreErrorCode::ConfigureDatabase,
            error.to_string(),
        )
    })?;
    if application_id != 0 && application_id != POSTER_STATE_APPLICATION_ID {
        return Err(PosterStateStoreError::new(
            PosterStateStoreErrorCode::ForeignDatabase,
            format!("unexpected SQLite application_id {application_id}"),
        ));
    }

    let schema_version = read_pragma_i64(connection, "user_version").map_err(|error| {
        PosterStateStoreError::new(
            PosterStateStoreErrorCode::ConfigureDatabase,
            error.to_string(),
        )
    })?;
    match schema_version {
        POSTER_STATE_SCHEMA_VERSION => verify_schema(connection),
        0 => create_schema_v1(connection),
        other => Err(PosterStateStoreError::new(
            PosterStateStoreErrorCode::UnsupportedSchema,
            format!("expected schema version {POSTER_STATE_SCHEMA_VERSION}, found {other}"),
        )),
    }
}

fn create_schema_v1(connection: &mut Connection) -> Result<(), PosterStateStoreError> {
    let transaction = connection
        .transaction_with_behavior(TransactionBehavior::Immediate)
        .map_err(|error| {
            PosterStateStoreError::new(
                PosterStateStoreErrorCode::InitializeSchema,
                error.to_string(),
            )
        })?;
    transaction
        .execute_batch(POSTER_STATE_SCHEMA_V1)
        .and_then(|_| {
            transaction.pragma_update(None, "application_id", POSTER_STATE_APPLICATION_ID)
        })
        .and_then(|_| transaction.pragma_update(None, "user_version", POSTER_STATE_SCHEMA_VERSION))
        .and_then(|_| transaction.commit())
        .map_err(|error| {
            PosterStateStoreError::new(
                PosterStateStoreErrorCode::InitializeSchema,
                error.to_string(),
            )
        })
}

fn verify_schema(connection: &Connection) -> Result<(), PosterStateStoreError> {
    connection
        .prepare(
            "SELECT id, contract_version, revision, selected_poster_id, snapshot_json
             FROM poster_state
             LIMIT 1",
        )
        .map(|_| ())
        .map_err(|error| {
            PosterStateStoreError::new(
                PosterStateStoreErrorCode::InitializeSchema,
                format!("schema v1 verification failed: {error}"),
            )
        })
}

fn query_stored_row(connection: &Connection) -> rusqlite::Result<Option<StoredRow>> {
    connection
        .query_row(
            "SELECT contract_version, revision, selected_poster_id, snapshot_json
             FROM poster_state
             WHERE id = 1",
            [],
            |row| {
                Ok(StoredRow {
                    contract_version: row.get(0)?,
                    revision: row.get(1)?,
                    selected_poster_id: row.get(2)?,
                    snapshot_json: row.get(3)?,
                })
            },
        )
        .optional()
}

fn load_from_connection(connection: &Connection) -> rusqlite::Result<PosterStateLoadResult> {
    Ok(match query_stored_row(connection)? {
        None => PosterStateLoadResult::NoData,
        Some(row) => decode_stored_row(row),
    })
}

fn decode_stored_row(row: StoredRow) -> PosterStateLoadResult {
    let snapshot = match serde_json::from_str::<Value>(&row.snapshot_json) {
        Ok(snapshot) => snapshot,
        Err(error) => {
            return PosterStateLoadResult::Rejected {
                code: PosterStateLoadRejectionCode::CorruptJson,
                message: error.to_string(),
            };
        }
    };

    let snapshot_contract_version = snapshot.get("contractVersion").and_then(Value::as_u64);
    if row.contract_version != POSTER_CONTRACT_VERSION as i64
        || snapshot_contract_version.is_some_and(|version| version != POSTER_CONTRACT_VERSION)
    {
        return PosterStateLoadResult::Rejected {
            code: PosterStateLoadRejectionCode::UnsupportedContract,
            message: format!(
                "expected poster contract version {POSTER_CONTRACT_VERSION}, found column={} json={snapshot_contract_version:?}",
                row.contract_version
            ),
        };
    }

    if let Err(errors) = validate_poster_contract_v1(&snapshot) {
        let code = if errors
            .iter()
            .any(|error| error.code == "unsupported_contract_version")
        {
            PosterStateLoadRejectionCode::UnsupportedContract
        } else {
            PosterStateLoadRejectionCode::InvalidContract
        };
        return PosterStateLoadResult::Rejected {
            code,
            message: summarize_poster_contract_errors(&errors),
        };
    }

    let stored_revision = match u64::try_from(row.revision) {
        Ok(revision) => revision,
        Err(error) => {
            return PosterStateLoadResult::Rejected {
                code: PosterStateLoadRejectionCode::MetadataMismatch,
                message: format!("invalid revision column: {error}"),
            };
        }
    };
    let snapshot_revision = snapshot.get("revision").and_then(Value::as_u64);
    let snapshot_selected_poster_id = snapshot.get("selectedPosterId").and_then(Value::as_str);
    if snapshot_contract_version != Some(row.contract_version as u64)
        || snapshot_revision != Some(stored_revision)
        || snapshot_selected_poster_id != Some(row.selected_poster_id.as_str())
    {
        return PosterStateLoadResult::Rejected {
            code: PosterStateLoadRejectionCode::MetadataMismatch,
            message: format!(
                "SQLite metadata does not match snapshot envelope: revision column={stored_revision} json={snapshot_revision:?}; selectedPosterId column={} json={snapshot_selected_poster_id:?}",
                row.selected_poster_id
            ),
        };
    }

    PosterStateLoadResult::Loaded {
        revision: stored_revision,
        snapshot,
    }
}

fn prepare_snapshot(
    mut snapshot: Value,
    revision: u64,
) -> Result<PreparedSnapshot, (PosterStateMutationRejectionCode, String)> {
    let Some(object) = snapshot.as_object_mut() else {
        return Err((
            PosterStateMutationRejectionCode::InvalidSnapshot,
            "poster snapshot must be a JSON object".to_string(),
        ));
    };
    object.insert("revision".to_string(), json!(revision));

    if let Err(errors) = validate_poster_contract_v1(&snapshot) {
        let code = if errors
            .iter()
            .any(|error| error.code == "unsupported_contract_version")
        {
            PosterStateMutationRejectionCode::UnsupportedContract
        } else {
            PosterStateMutationRejectionCode::InvalidSnapshot
        };
        return Err((code, summarize_poster_contract_errors(&errors)));
    }

    let contract_version = snapshot["contractVersion"]
        .as_u64()
        .expect("validated contract version must be an unsigned integer");
    let selected_poster_id = snapshot["selectedPosterId"]
        .as_str()
        .expect("validated selected poster id must be a string")
        .to_string();
    let snapshot_json = serde_json::to_string(&snapshot).map_err(|error| {
        (
            PosterStateMutationRejectionCode::InvalidSnapshot,
            error.to_string(),
        )
    })?;
    Ok(PreparedSnapshot {
        contract_version,
        revision,
        selected_poster_id,
        snapshot_json,
        snapshot,
    })
}

fn read_pragma_i64(connection: &Connection, pragma: &str) -> rusqlite::Result<i64> {
    connection.query_row(&format!("PRAGMA {pragma}"), [], |row| row.get(0))
}

#[cfg(test)]
mod tests {
    use std::{
        fs,
        time::{SystemTime, UNIX_EPOCH},
    };

    use rusqlite::{params, Connection};
    use serde_json::{json, Value};

    use super::{
        initialize_schema, LegacyAdapterDiagnostic, LegacyPosterMigrationRejectionCode,
        LegacyPosterMigrationRequest, LegacyPosterMigrationResult, LegacyPosterSource,
        LegacyPosterSourceKind, PosterStateLoadRejectionCode, PosterStateLoadResult,
        PosterStateMutationRejectionCode, PosterStateMutationResult, PosterStateRepository,
        PosterStateStoreErrorCode, LEGACY_POSTER_BACKUP_DIRECTORY, MAX_POSTER_STATE_REVISION,
        POSTER_STATE_APPLICATION_ID, POSTER_STATE_SCHEMA_VERSION,
    };

    fn valid_snapshot() -> Value {
        json!({
            "contractVersion": 1,
            "revision": 77,
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

    fn temporary_repository(label: &str) -> (PosterStateRepository, std::path::PathBuf) {
        let unique = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("system clock")
            .as_nanos();
        let directory = std::env::temp_dir().join(format!(
            "desktop-up-cpss-{label}-{}-{unique}",
            std::process::id()
        ));
        let repository = PosterStateRepository::open(directory.join("poster-state-v1.sqlite3"))
            .expect("persistent repository");
        (repository, directory)
    }

    fn cleanup_repository(repository: PosterStateRepository, directory: &std::path::Path) {
        drop(repository);
        fs::remove_dir_all(directory).expect("remove temporary repository");
    }

    fn legacy_snapshot() -> Value {
        json!({
            "selectedPosterId": "night-ops",
            "posters": [
                {
                    "id": "night-ops",
                    "title": "NIGHT OPS",
                    "subtitle": "TACTICAL ARCHIVE / 01",
                    "x": 72,
                    "y": 154,
                    "width": 490,
                    "height": 392,
                    "collapsed": false,
                    "locked": false,
                    "items": [{ "id": "afterlight", "title": "AFTERLIGHT" }]
                },
                {
                    "id": "indie-arcade",
                    "title": "INDIE ARCADE",
                    "subtitle": "SMALL WORLDS / BIG IDEAS",
                    "x": 610,
                    "y": 226,
                    "width": 376,
                    "height": 314,
                    "collapsed": true,
                    "locked": false,
                    "items": [{ "id": "red-sector", "title": "RED SECTOR" }]
                }
            ]
        })
    }

    fn adapted_first_snapshot() -> Value {
        let mut snapshot = valid_snapshot();
        snapshot["selectedPosterId"] = json!("night-ops");
        snapshot["posters"][0]["id"] = json!("night-ops");
        snapshot["posters"][0]["title"] = json!("NIGHT OPS");
        snapshot["posters"][0]["subtitle"] = json!("TACTICAL ARCHIVE / 01");
        snapshot["posters"][0]["anchor"] = json!("free");
        snapshot
    }

    fn legacy_migration_request() -> LegacyPosterMigrationRequest {
        LegacyPosterMigrationRequest {
            source: LegacyPosterSource {
                kind: LegacyPosterSourceKind::SharedJson,
                key: "poster-snapshot.json".to_string(),
            },
            legacy_snapshot: legacy_snapshot(),
            adapted_snapshot: Some(adapted_first_snapshot()),
            adapter_migrations: vec![json!({
                "path": "$.posters[0].x/y",
                "target": "$.posters[0].normalizedRect"
            })],
            adapter_errors: Vec::new(),
        }
    }

    #[test]
    fn persistent_database_uses_wal_and_reopens_schema() {
        let unique = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("system clock")
            .as_nanos();
        let directory =
            std::env::temp_dir().join(format!("desktop-up-cpss-{}-{unique}", std::process::id()));
        let path = directory.join("poster-state-v1.sqlite3");

        let repository =
            PosterStateRepository::open(path.clone()).expect("persistent database should open");
        assert_eq!(repository.path(), Some(path.as_path()));
        {
            let connection = repository.connection.lock().expect("repository lock");
            let journal_mode: String = connection
                .query_row("PRAGMA journal_mode", [], |row| row.get(0))
                .expect("journal mode");
            let synchronous: i64 = connection
                .query_row("PRAGMA synchronous", [], |row| row.get(0))
                .expect("synchronous mode");
            assert_eq!(journal_mode, "wal");
            assert_eq!(synchronous, 2);
        }
        drop(repository);

        let reopened = PosterStateRepository::open(path.clone()).expect("schema v1 should reopen");
        assert_eq!(
            reopened.schema_version().expect("schema version"),
            POSTER_STATE_SCHEMA_VERSION
        );
        drop(reopened);

        for file in [
            path.clone(),
            directory.join("poster-state-v1.sqlite3-wal"),
            directory.join("poster-state-v1.sqlite3-shm"),
        ] {
            let _ = fs::remove_file(file);
        }
        let _ = fs::remove_dir(directory);
    }

    #[test]
    fn creates_the_singleton_json_schema_with_independent_versioning() {
        let repository = PosterStateRepository::open_in_memory().expect("schema should initialize");
        assert_eq!(
            repository.schema_version().expect("schema version"),
            POSTER_STATE_SCHEMA_VERSION
        );
        assert_eq!(repository.path(), None);

        let connection = repository.connection.lock().expect("repository lock");
        let application_id: i64 = connection
            .query_row("PRAGMA application_id", [], |row| row.get(0))
            .expect("application id");
        assert_eq!(application_id, POSTER_STATE_APPLICATION_ID);

        let mut columns = connection
            .prepare("PRAGMA table_info(poster_state)")
            .expect("table info");
        let column_names = columns
            .query_map([], |row| row.get::<_, String>(1))
            .expect("column query")
            .collect::<Result<Vec<_>, _>>()
            .expect("column values");
        assert_eq!(
            column_names,
            [
                "id",
                "contract_version",
                "revision",
                "selected_poster_id",
                "snapshot_json"
            ]
        );
        assert!(!column_names
            .iter()
            .any(|name| name.contains("runtime") || name.contains("interaction")));
    }

    #[test]
    fn schema_enforces_one_valid_json_envelope_row() {
        let repository = PosterStateRepository::open_in_memory().expect("schema should initialize");
        let connection = repository.connection.lock().expect("repository lock");
        connection
            .execute(
                "INSERT INTO poster_state (
                    id, contract_version, revision, selected_poster_id, snapshot_json
                 ) VALUES (1, 1, 0, 'poster-primary', '{}')",
                [],
            )
            .expect("valid singleton row");
        assert!(connection
            .execute(
                "INSERT INTO poster_state (
                    id, contract_version, revision, selected_poster_id, snapshot_json
                 ) VALUES (2, 1, 0, 'poster-other', '{}')",
                [],
            )
            .is_err());
        assert!(connection
            .execute(
                "UPDATE poster_state SET snapshot_json = 'not-json' WHERE id = 1",
                [],
            )
            .is_err());
    }

    #[test]
    fn future_schema_and_foreign_database_are_rejected_without_rewrite() {
        let mut future = Connection::open_in_memory().expect("future database");
        future
            .pragma_update(None, "application_id", POSTER_STATE_APPLICATION_ID)
            .expect("application id");
        future
            .pragma_update(None, "user_version", POSTER_STATE_SCHEMA_VERSION + 1)
            .expect("future version");
        let error = initialize_schema(&mut future).expect_err("future schema must fail");
        assert_eq!(error.code, PosterStateStoreErrorCode::UnsupportedSchema);
        let version: i64 = future
            .query_row("PRAGMA user_version", [], |row| row.get(0))
            .expect("future version remains");
        assert_eq!(version, POSTER_STATE_SCHEMA_VERSION + 1);

        let mut foreign = Connection::open_in_memory().expect("foreign database");
        foreign
            .pragma_update(None, "application_id", POSTER_STATE_APPLICATION_ID + 1)
            .expect("foreign id");
        let error = initialize_schema(&mut foreign).expect_err("foreign database must fail");
        assert_eq!(error.code, PosterStateStoreErrorCode::ForeignDatabase);
    }

    #[test]
    fn load_distinguishes_no_data_and_a_valid_snapshot() {
        let repository = PosterStateRepository::open_in_memory().expect("repository");
        assert_eq!(
            repository.load().expect("empty load"),
            PosterStateLoadResult::NoData
        );

        let stored = repository
            .replace_with_expected_revision(None, valid_snapshot())
            .expect("create");
        assert!(matches!(
            stored,
            PosterStateMutationResult::Stored {
                previous_revision: None,
                revision: 0,
                ..
            }
        ));
        let loaded = repository.load().expect("loaded");
        match loaded {
            PosterStateLoadResult::Loaded { revision, snapshot } => {
                assert_eq!(revision, 0);
                assert_eq!(snapshot["revision"], json!(0));
            }
            other => panic!("expected loaded state, got {other:?}"),
        }
    }

    #[test]
    fn load_rejects_invalid_contract_unsupported_contract_and_metadata_mismatch() {
        let repository = PosterStateRepository::open_in_memory().expect("repository");
        let insert =
            |contract_version: i64, revision: i64, selected_poster_id: &str, snapshot: Value| {
                let connection = repository.connection.lock().expect("repository lock");
                connection
                    .execute(
                        "INSERT OR REPLACE INTO poster_state (
                        id, contract_version, revision, selected_poster_id, snapshot_json
                     ) VALUES (1, ?1, ?2, ?3, ?4)",
                        params![
                            contract_version,
                            revision,
                            selected_poster_id,
                            serde_json::to_string(&snapshot).expect("snapshot json")
                        ],
                    )
                    .expect("raw insert");
            };

        let mut invalid = valid_snapshot();
        invalid["posters"][0]["runtimeMode"] = json!("paused");
        insert(1, 77, "poster-primary", invalid);
        assert!(matches!(
            repository.load().expect("invalid load"),
            PosterStateLoadResult::Rejected {
                code: PosterStateLoadRejectionCode::InvalidContract,
                ..
            }
        ));

        let mut unsupported = valid_snapshot();
        unsupported["contractVersion"] = json!(99);
        insert(99, 77, "poster-primary", unsupported);
        assert!(matches!(
            repository.load().expect("unsupported load"),
            PosterStateLoadResult::Rejected {
                code: PosterStateLoadRejectionCode::UnsupportedContract,
                ..
            }
        ));

        insert(1, 4, "poster-other", valid_snapshot());
        assert!(matches!(
            repository.load().expect("metadata load"),
            PosterStateLoadResult::Rejected {
                code: PosterStateLoadRejectionCode::MetadataMismatch,
                ..
            }
        ));
    }

    #[test]
    fn load_reports_database_failures_separately_from_content_rejections() {
        let repository = PosterStateRepository::open_in_memory().expect("repository");
        repository
            .connection
            .lock()
            .expect("repository lock")
            .execute("DROP TABLE poster_state", [])
            .expect("drop table");
        let error = repository.load().expect_err("query failure");
        assert_eq!(error.code, PosterStateStoreErrorCode::QueryDatabase);
    }

    #[test]
    fn create_and_replace_assign_revisions_and_enforce_cas() {
        let repository = PosterStateRepository::open_in_memory().expect("repository");
        let create = repository
            .replace_with_expected_revision(None, valid_snapshot())
            .expect("create");
        assert!(matches!(
            create,
            PosterStateMutationResult::Stored {
                previous_revision: None,
                revision: 0,
                ..
            }
        ));

        let conflict = repository
            .replace_with_expected_revision(None, valid_snapshot())
            .expect("create conflict");
        assert_eq!(
            conflict,
            PosterStateMutationResult::Conflict {
                expected_revision: None,
                actual_revision: Some(0),
            }
        );

        let mut update = valid_snapshot();
        update["posters"][0]["title"] = json!("UPDATED");
        let stored = repository
            .replace_with_expected_revision(Some(0), update)
            .expect("replace");
        match stored {
            PosterStateMutationResult::Stored {
                previous_revision,
                revision,
                snapshot,
            } => {
                assert_eq!(previous_revision, Some(0));
                assert_eq!(revision, 1);
                assert_eq!(snapshot["revision"], json!(1));
                assert_eq!(snapshot["posters"][0]["title"], json!("UPDATED"));
            }
            other => panic!("expected stored state, got {other:?}"),
        }
    }

    #[test]
    fn conflicts_and_validation_failures_preserve_the_previous_snapshot() {
        let repository = PosterStateRepository::open_in_memory().expect("repository");
        repository
            .replace_with_expected_revision(None, valid_snapshot())
            .expect("create");

        let conflict = repository
            .replace_with_expected_revision(Some(7), valid_snapshot())
            .expect("conflict");
        assert_eq!(
            conflict,
            PosterStateMutationResult::Conflict {
                expected_revision: Some(7),
                actual_revision: Some(0),
            }
        );

        let mut invalid = valid_snapshot();
        invalid["posters"][0]["templateKey"] = json!("minimal-fallback");
        assert!(matches!(
            repository
                .replace_with_expected_revision(Some(0), invalid)
                .expect("rejection"),
            PosterStateMutationResult::Rejected {
                code: PosterStateMutationRejectionCode::InvalidSnapshot,
                ..
            }
        ));

        let loaded = repository.load().expect("load previous");
        assert!(matches!(
            loaded,
            PosterStateLoadResult::Loaded { revision: 0, .. }
        ));
    }

    #[test]
    fn structured_results_serialize_with_camel_case_fields() {
        assert_eq!(
            serde_json::to_value(PosterStateMutationResult::Conflict {
                expected_revision: Some(2),
                actual_revision: Some(3),
            })
            .expect("serialize conflict"),
            json!({
                "status": "conflict",
                "expectedRevision": 2,
                "actualRevision": 3
            })
        );
        assert_eq!(
            serde_json::to_value(PosterStateLoadResult::Rejected {
                code: PosterStateLoadRejectionCode::MetadataMismatch,
                message: "mismatch".to_string(),
            })
            .expect("serialize rejection"),
            json!({
                "status": "rejected",
                "code": "metadataMismatch",
                "message": "mismatch"
            })
        );

        assert_eq!(
            serde_json::to_value(super::PosterStateStoreError::new(
                PosterStateStoreErrorCode::QueryDatabase,
                "database unavailable",
            ))
            .expect("serialize database error"),
            json!({
                "code": "queryDatabase",
                "message": "database unavailable"
            })
        );
    }

    #[test]
    fn corrupt_json_is_distinct_from_contract_and_database_failures() {
        let result = super::decode_stored_row(super::StoredRow {
            contract_version: 1,
            revision: 0,
            selected_poster_id: "poster-primary".to_string(),
            snapshot_json: "{not-json".to_string(),
        });
        assert!(matches!(
            result,
            PosterStateLoadResult::Rejected {
                code: PosterStateLoadRejectionCode::CorruptJson,
                ..
            }
        ));
    }
    #[test]
    fn database_write_failure_rolls_back_and_preserves_the_previous_snapshot() {
        let repository = PosterStateRepository::open_in_memory().expect("repository");
        repository
            .replace_with_expected_revision(None, valid_snapshot())
            .expect("create");
        {
            let connection = repository.connection.lock().expect("repository lock");
            connection
                .execute_batch(
                    "CREATE TRIGGER reject_poster_state_update
                     BEFORE UPDATE ON poster_state
                     BEGIN
                         SELECT RAISE(ABORT, 'injected transaction failure');
                     END;",
                )
                .expect("failure trigger");
        }

        let mut update = valid_snapshot();
        update["posters"][0]["title"] = json!("MUST NOT COMMIT");
        let error = repository
            .replace_with_expected_revision(Some(0), update)
            .expect_err("injected write failure");
        assert_eq!(error.code, PosterStateStoreErrorCode::WriteDatabase);

        match repository.load().expect("load previous snapshot") {
            PosterStateLoadResult::Loaded { revision, snapshot } => {
                assert_eq!(revision, 0);
                assert_eq!(snapshot["posters"][0]["title"], json!("DESKTOP COLLECTION"));
            }
            other => panic!("expected previous loaded snapshot, got {other:?}"),
        }
    }

    #[test]
    fn rejected_current_state_and_revision_exhaustion_do_not_rewrite_data() {
        let repository = PosterStateRepository::open_in_memory().expect("repository");
        {
            let connection = repository.connection.lock().expect("repository lock");
            let mut corrupt = valid_snapshot();
            corrupt["posters"][0]["runtimeMode"] = json!("paused");
            connection
                .execute(
                    "INSERT INTO poster_state (
                        id, contract_version, revision, selected_poster_id, snapshot_json
                     ) VALUES (1, 1, 0, 'poster-primary', ?1)",
                    params![serde_json::to_string(&corrupt).expect("snapshot json")],
                )
                .expect("corrupt row");
        }
        assert!(matches!(
            repository
                .replace_with_expected_revision(Some(0), valid_snapshot())
                .expect("current rejection"),
            PosterStateMutationResult::Rejected {
                code: PosterStateMutationRejectionCode::CurrentStateRejected,
                ..
            }
        ));

        {
            let connection = repository.connection.lock().expect("repository lock");
            let mut exhausted = valid_snapshot();
            exhausted["revision"] = json!(MAX_POSTER_STATE_REVISION);
            connection
                .execute(
                    "UPDATE poster_state
                     SET revision = ?1, snapshot_json = ?2",
                    params![
                        MAX_POSTER_STATE_REVISION as i64,
                        serde_json::to_string(&exhausted).expect("snapshot json")
                    ],
                )
                .expect("exhausted row");
        }
        assert!(matches!(
            repository
                .replace_with_expected_revision(Some(MAX_POSTER_STATE_REVISION), valid_snapshot())
                .expect("revision exhaustion"),
            PosterStateMutationResult::Rejected {
                code: PosterStateMutationRejectionCode::RevisionExhausted,
                ..
            }
        ));
        assert!(matches!(
            repository.load().expect("exhausted load"),
            PosterStateLoadResult::Loaded {
                revision: MAX_POSTER_STATE_REVISION,
                ..
            }
        ));
    }

    #[test]
    fn legacy_migration_backs_up_full_source_and_is_idempotent() {
        let (repository, directory) = temporary_repository("legacy-success");
        let migrated = repository
            .migrate_legacy(legacy_migration_request())
            .expect("legacy migration");
        let (source_fingerprint, backup_path) = match migrated {
            LegacyPosterMigrationResult::Migrated {
                source,
                source_fingerprint,
                backup_path,
                revision,
                snapshot,
                adapter_migrations,
            } => {
                assert_eq!(source.kind, LegacyPosterSourceKind::SharedJson);
                assert_eq!(source.key, "poster-snapshot.json");
                assert_eq!(source_fingerprint.len(), 64);
                assert_eq!(revision, 0);
                assert_eq!(snapshot["revision"], json!(0));
                assert_eq!(snapshot["posters"].as_array().expect("posters").len(), 1);
                assert_eq!(snapshot["posters"][0]["id"], json!("night-ops"));
                assert_eq!(adapter_migrations.len(), 1);
                (source_fingerprint, std::path::PathBuf::from(backup_path))
            }
            other => panic!("expected migrated result, got {other:?}"),
        };

        let backup: Value =
            serde_json::from_slice(&fs::read(&backup_path).expect("read retained legacy backup"))
                .expect("backup JSON");
        assert_eq!(backup["backupVersion"], json!(1));
        assert_eq!(backup["source"]["kind"], json!("sharedJson"));
        assert_eq!(
            backup["legacySnapshot"]["posters"]
                .as_array()
                .expect("legacy posters")
                .len(),
            2
        );
        assert_eq!(
            backup["legacySnapshot"]["posters"][1]["id"],
            json!("indie-arcade")
        );

        let repeated = repository
            .migrate_legacy(legacy_migration_request())
            .expect("repeated migration");
        match repeated {
            LegacyPosterMigrationResult::SkippedExistingState {
                source_fingerprint: repeated_fingerprint,
                backup_path: Some(repeated_backup_path),
                revision,
                ..
            } => {
                assert_eq!(repeated_fingerprint, source_fingerprint);
                assert_eq!(std::path::PathBuf::from(repeated_backup_path), backup_path);
                assert_eq!(revision, 0);
            }
            other => panic!("expected idempotent skip, got {other:?}"),
        }
        let backup_count = fs::read_dir(directory.join(LEGACY_POSTER_BACKUP_DIRECTORY))
            .expect("backup directory")
            .count();
        assert_eq!(backup_count, 1);
        assert!(matches!(
            repository.load().expect("load migrated state"),
            PosterStateLoadResult::Loaded { revision: 0, .. }
        ));
        cleanup_repository(repository, &directory);
    }

    #[test]
    fn adapter_failure_retains_source_and_does_not_create_default_state() {
        let (repository, directory) = temporary_repository("adapter-failure");
        let mut request = legacy_migration_request();
        request.adapted_snapshot = None;
        request.adapter_errors = vec![LegacyAdapterDiagnostic {
            path: "$.posters[0].width".to_string(),
            code: "invalid_dimension".to_string(),
            message: "width must be positive".to_string(),
        }];

        let result = repository
            .migrate_legacy(request)
            .expect("adapter rejection");
        let backup_path = match result {
            LegacyPosterMigrationResult::Rejected {
                code: LegacyPosterMigrationRejectionCode::AdapterFailed,
                backup_path: Some(backup_path),
                adapter_errors,
                ..
            } => {
                assert_eq!(adapter_errors[0].code, "invalid_dimension");
                std::path::PathBuf::from(backup_path)
            }
            other => panic!("expected adapter rejection, got {other:?}"),
        };
        assert!(backup_path.exists());
        assert_eq!(
            repository.load().expect("no formal state"),
            PosterStateLoadResult::NoData
        );
        cleanup_repository(repository, &directory);
    }

    #[test]
    fn migration_rejects_more_than_the_first_poster_after_retaining_source() {
        let (repository, directory) = temporary_repository("scope-failure");
        let mut request = legacy_migration_request();
        let second = {
            let mut poster =
                request.adapted_snapshot.as_ref().expect("adapted")["posters"][0].clone();
            poster["id"] = json!("indie-arcade");
            poster["title"] = json!("INDIE ARCADE");
            poster
        };
        request.adapted_snapshot.as_mut().expect("adapted")["posters"]
            .as_array_mut()
            .expect("posters")
            .push(second);

        let result = repository.migrate_legacy(request).expect("scope rejection");
        assert!(matches!(
            result,
            LegacyPosterMigrationResult::Rejected {
                code: LegacyPosterMigrationRejectionCode::ScopeViolation,
                backup_path: Some(_),
                ..
            }
        ));
        assert_eq!(
            repository.load().expect("no formal state"),
            PosterStateLoadResult::NoData
        );
        cleanup_repository(repository, &directory);
    }

    #[test]
    fn backup_failure_prevents_any_database_write() {
        let (repository, directory) = temporary_repository("backup-failure");
        fs::write(
            directory.join(LEGACY_POSTER_BACKUP_DIRECTORY),
            b"blocks directory creation",
        )
        .expect("blocking backup file");

        let error = repository
            .migrate_legacy(legacy_migration_request())
            .expect_err("backup must fail");
        assert_eq!(error.code, PosterStateStoreErrorCode::CreateBackupDirectory);
        assert_eq!(
            repository.load().expect("no formal state"),
            PosterStateLoadResult::NoData
        );
        cleanup_repository(repository, &directory);
    }

    #[test]
    fn database_failure_keeps_backup_and_rolls_back_migration() {
        let (repository, directory) = temporary_repository("write-failure");
        {
            let connection = repository.connection.lock().expect("repository lock");
            connection
                .execute_batch(
                    "CREATE TRIGGER reject_legacy_insert
                     BEFORE INSERT ON poster_state
                     BEGIN
                         SELECT RAISE(ABORT, 'injected legacy write failure');
                     END;",
                )
                .expect("failure trigger");
        }

        let error = repository
            .migrate_legacy(legacy_migration_request())
            .expect_err("database insert must fail");
        assert_eq!(error.code, PosterStateStoreErrorCode::WriteDatabase);
        assert_eq!(
            repository.load().expect("no formal state"),
            PosterStateLoadResult::NoData
        );
        assert_eq!(
            fs::read_dir(directory.join(LEGACY_POSTER_BACKUP_DIRECTORY))
                .expect("retained backup directory")
                .count(),
            1
        );
        cleanup_repository(repository, &directory);
    }

    #[test]
    fn existing_formal_or_rejected_state_is_never_overwritten_by_legacy() {
        let (repository, directory) = temporary_repository("existing-state");
        repository
            .replace_with_expected_revision(None, valid_snapshot())
            .expect("formal create");
        let result = repository
            .migrate_legacy(legacy_migration_request())
            .expect("existing state skip");
        assert!(matches!(
            result,
            LegacyPosterMigrationResult::SkippedExistingState {
                backup_path: None,
                revision: 0,
                ..
            }
        ));
        match repository.load().expect("formal state remains") {
            PosterStateLoadResult::Loaded { snapshot, .. } => {
                assert_eq!(snapshot["posters"][0]["id"], json!("poster-primary"));
            }
            other => panic!("expected formal state, got {other:?}"),
        }
        assert!(!directory.join(LEGACY_POSTER_BACKUP_DIRECTORY).exists());
        cleanup_repository(repository, &directory);

        let (repository, directory) = temporary_repository("rejected-state");
        {
            let connection = repository.connection.lock().expect("repository lock");
            let mut corrupt = valid_snapshot();
            corrupt["posters"][0]["runtimeMode"] = json!("paused");
            connection
                .execute(
                    "INSERT INTO poster_state (
                        id, contract_version, revision, selected_poster_id, snapshot_json
                     ) VALUES (1, 1, 0, 'poster-primary', ?1)",
                    params![serde_json::to_string(&corrupt).expect("snapshot JSON")],
                )
                .expect("corrupt current state");
        }
        let result = repository
            .migrate_legacy(legacy_migration_request())
            .expect("current state rejection");
        assert!(matches!(
            result,
            LegacyPosterMigrationResult::Rejected {
                code: LegacyPosterMigrationRejectionCode::CurrentStateRejected,
                backup_path: None,
                ..
            }
        ));
        assert!(matches!(
            repository.load().expect("rejected state remains"),
            PosterStateLoadResult::Rejected { .. }
        ));
        assert!(!directory.join(LEGACY_POSTER_BACKUP_DIRECTORY).exists());
        cleanup_repository(repository, &directory);
    }

    #[test]
    fn legacy_migration_results_serialize_with_stable_camel_case_fields() {
        let value = serde_json::to_value(LegacyPosterMigrationResult::SkippedExistingState {
            source: LegacyPosterSource {
                kind: LegacyPosterSourceKind::LocalStorage,
                key: "desktop-up.a0-demo.v1".to_string(),
            },
            source_fingerprint: "abc123".to_string(),
            backup_path: Some("poster-state-backups/legacy-abc123.json".to_string()),
            revision: 0,
        })
        .expect("serialize migration result");
        assert_eq!(value["status"], json!("skippedExistingState"));
        assert_eq!(value["source"]["kind"], json!("localStorage"));
        assert_eq!(value["sourceFingerprint"], json!("abc123"));
        assert_eq!(
            value["backupPath"],
            json!("poster-state-backups/legacy-abc123.json")
        );
    }
}
