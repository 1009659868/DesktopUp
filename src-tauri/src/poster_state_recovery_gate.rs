use std::{
    fs::{self, OpenOptions},
    io::Write,
    path::{Path, PathBuf},
    time::{SystemTime, UNIX_EPOCH},
};

use rusqlite::{params, Connection};
use serde::Serialize;
use serde_json::{json, Value};

use crate::poster_state_store::{
    LegacyAdapterDiagnostic, LegacyPosterMigrationRejectionCode, LegacyPosterMigrationRequest,
    LegacyPosterMigrationResult, LegacyPosterSource, LegacyPosterSourceKind,
    PosterStateLoadRejectionCode, PosterStateLoadResult, PosterStateMutationRejectionCode,
    PosterStateMutationResult, PosterStateRepository, PosterStateStoreErrorCode,
    LEGACY_POSTER_BACKUP_DIRECTORY, POSTER_STATE_APPLICATION_ID, POSTER_STATE_SCHEMA_VERSION,
};

pub const RECOVERY_GATE_ARGUMENT_PREFIX: &str = "--cpss-recovery-gate-report=";
pub const RECOVERY_GATE_CONTRACT: &str = "poster-state-recovery-gate-v1";

const RECOVERY_GATE_WORKSPACE: &str = "poster-state-recovery-workspace-v1";

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PosterStateRecoveryGateReport {
    contract_version: &'static str,
    schema_version: i64,
    application_id: i64,
    isolated: bool,
    passed: bool,
    violations: Vec<String>,
    scenarios: Vec<PosterStateRecoveryScenario>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct PosterStateRecoveryScenario {
    id: &'static str,
    passed: bool,
    evidence: Value,
    violations: Vec<String>,
}

impl PosterStateRecoveryScenario {
    fn execute<F>(id: &'static str, operation: F) -> Self
    where
        F: FnOnce() -> Result<Value, String>,
    {
        match operation() {
            Ok(evidence) => Self {
                id,
                passed: true,
                evidence,
                violations: Vec::new(),
            },
            Err(message) => Self {
                id,
                passed: false,
                evidence: Value::Null,
                violations: vec![message],
            },
        }
    }
}

pub fn run_if_requested() -> Option<i32> {
    let report_path = std::env::args_os().find_map(|argument| {
        let argument = argument.to_string_lossy();
        argument
            .strip_prefix(RECOVERY_GATE_ARGUMENT_PREFIX)
            .filter(|path| !path.trim().is_empty())
            .map(PathBuf::from)
    })?;

    Some(match execute_release_probe(&report_path) {
        Ok(report) if report.passed => 0,
        Ok(_) => 2,
        Err(error) => {
            eprintln!("C-PSS recovery gate could not write its report: {error}");
            3
        }
    })
}

fn execute_release_probe(report_path: &Path) -> Result<PosterStateRecoveryGateReport, String> {
    let parent = report_path
        .parent()
        .filter(|parent| !parent.as_os_str().is_empty())
        .ok_or_else(|| "the recovery report path must have a parent directory".to_string())?;
    if !parent.is_dir() {
        return Err(format!(
            "the recovery report parent does not exist: {}",
            parent.display()
        ));
    }

    let workspace = parent.join(RECOVERY_GATE_WORKSPACE);
    fs::create_dir(&workspace).map_err(|error| {
        format!(
            "failed to create isolated recovery workspace {}: {error}",
            workspace.display()
        )
    })?;
    let report = run_isolated_recovery_matrix(&workspace);
    write_report_atomically(report_path, &report)?;
    Ok(report)
}

fn run_isolated_recovery_matrix(workspace: &Path) -> PosterStateRecoveryGateReport {
    let scenarios = vec![
        PosterStateRecoveryScenario::execute("restart-revision-and-schema-bootstrap", || {
            scenario_restart_revision_and_schema(workspace)
        }),
        PosterStateRecoveryScenario::execute("damaged-content-rejections", || {
            scenario_damaged_content_rejections(workspace)
        }),
        PosterStateRecoveryScenario::execute("schema-and-database-identity-rejections", || {
            scenario_schema_and_database_identity(workspace)
        }),
        PosterStateRecoveryScenario::execute("unreadable-and-query-failures", || {
            scenario_unreadable_and_query_failures(workspace)
        }),
        PosterStateRecoveryScenario::execute("cas-conflict-and-validation-preserve", || {
            scenario_cas_conflict_and_validation_preserve(workspace)
        }),
        PosterStateRecoveryScenario::execute("transactional-write-failure-rollback", || {
            scenario_transactional_write_failure(workspace)
        }),
        PosterStateRecoveryScenario::execute("legacy-backup-and-idempotence", || {
            scenario_legacy_backup_and_idempotence(workspace)
        }),
        PosterStateRecoveryScenario::execute("legacy-failure-source-retention", || {
            scenario_legacy_failure_source_retention(workspace)
        }),
    ];
    let violations = scenarios
        .iter()
        .filter(|scenario| !scenario.passed)
        .flat_map(|scenario| {
            scenario
                .violations
                .iter()
                .map(move |message| format!("{}: {message}", scenario.id))
        })
        .collect::<Vec<_>>();

    PosterStateRecoveryGateReport {
        contract_version: RECOVERY_GATE_CONTRACT,
        schema_version: POSTER_STATE_SCHEMA_VERSION,
        application_id: POSTER_STATE_APPLICATION_ID,
        isolated: true,
        passed: violations.is_empty(),
        violations,
        scenarios,
    }
}

fn scenario_restart_revision_and_schema(workspace: &Path) -> Result<Value, String> {
    let directory = scenario_directory(workspace, "restart")?;
    let database = directory.join("poster-state-v1.sqlite3");
    let repository = open_repository(&database)?;
    ensure(
        repository.schema_version().map_err(store_error)? == POSTER_STATE_SCHEMA_VERSION,
        "a new repository did not bootstrap schema v1",
    )?;
    match repository
        .replace_with_expected_revision(None, valid_snapshot())
        .map_err(store_error)?
    {
        PosterStateMutationResult::Stored {
            previous_revision: None,
            revision: 0,
            ..
        } => {}
        result => return Err(format!("unexpected create result: {result:?}")),
    }
    drop(repository);

    let repository = open_repository(&database)?;
    let snapshot = expect_loaded(&repository, 0)?;
    ensure(
        snapshot["posters"][0]["title"] == json!("DESKTOP COLLECTION"),
        "restart did not restore the created snapshot",
    )?;
    let mut updated = snapshot;
    updated["posters"][0]["title"] = json!("RECOVERED COLLECTION");
    match repository
        .replace_with_expected_revision(Some(0), updated)
        .map_err(store_error)?
    {
        PosterStateMutationResult::Stored {
            previous_revision: Some(0),
            revision: 1,
            ..
        } => {}
        result => return Err(format!("unexpected revision update result: {result:?}")),
    }
    drop(repository);

    let repository = open_repository(&database)?;
    let snapshot = expect_loaded(&repository, 1)?;
    ensure(
        snapshot["posters"][0]["title"] == json!("RECOVERED COLLECTION"),
        "the revision-1 value was not durable after a second restart",
    )?;
    Ok(json!({
        "schemaBootstrapped": true,
        "restartCount": 2,
        "finalRevision": 1,
        "durableTitle": snapshot["posters"][0]["title"]
    }))
}

fn scenario_damaged_content_rejections(workspace: &Path) -> Result<Value, String> {
    let directory = scenario_directory(workspace, "damaged-content")?;
    let corrupt = directory.join("corrupt.sqlite3");
    write_raw_row(&corrupt, 1, 0, "poster-primary", "{broken-json")?;
    expect_load_rejection(&corrupt, PosterStateLoadRejectionCode::CorruptJson)?;

    let invalid_contract = directory.join("invalid-contract.sqlite3");
    write_raw_row(
        &invalid_contract,
        1,
        0,
        "poster-primary",
        &json!({
            "contractVersion": 1,
            "revision": 0,
            "selectedPosterId": "poster-primary",
            "posters": []
        })
        .to_string(),
    )?;
    expect_load_rejection(
        &invalid_contract,
        PosterStateLoadRejectionCode::InvalidContract,
    )?;

    let future_contract = directory.join("future-contract.sqlite3");
    let mut future_snapshot = valid_snapshot();
    future_snapshot["contractVersion"] = json!(2);
    future_snapshot["revision"] = json!(0);
    write_raw_row(
        &future_contract,
        2,
        0,
        "poster-primary",
        &future_snapshot.to_string(),
    )?;
    expect_load_rejection(
        &future_contract,
        PosterStateLoadRejectionCode::UnsupportedContract,
    )?;

    let metadata_mismatch = directory.join("metadata-mismatch.sqlite3");
    let mut mismatched_snapshot = valid_snapshot();
    mismatched_snapshot["revision"] = json!(0);
    write_raw_row(
        &metadata_mismatch,
        1,
        9,
        "poster-primary",
        &mismatched_snapshot.to_string(),
    )?;
    expect_load_rejection(
        &metadata_mismatch,
        PosterStateLoadRejectionCode::MetadataMismatch,
    )?;

    Ok(json!({
        "rejections": ["corruptJson", "invalidContract", "unsupportedContract", "metadataMismatch"],
        "defaultResetObserved": false
    }))
}

fn scenario_schema_and_database_identity(workspace: &Path) -> Result<Value, String> {
    let directory = scenario_directory(workspace, "schema-identity")?;
    let future_schema = directory.join("future-schema.sqlite3");
    let connection = Connection::open(&future_schema).map_err(sqlite_error)?;
    connection
        .pragma_update(None, "application_id", POSTER_STATE_APPLICATION_ID)
        .and_then(|_| connection.pragma_update(None, "user_version", 2))
        .map_err(sqlite_error)?;
    drop(connection);
    let error = PosterStateRepository::open(future_schema.clone())
        .err()
        .ok_or_else(|| "future schema was accepted".to_string())?;
    ensure(
        error.code == PosterStateStoreErrorCode::UnsupportedSchema,
        format!("future schema returned {}", error.code.as_str()),
    )?;
    ensure(
        read_pragma(&future_schema, "user_version")? == 2,
        "future schema was silently rewritten",
    )?;

    let foreign_database = directory.join("foreign.sqlite3");
    let connection = Connection::open(&foreign_database).map_err(sqlite_error)?;
    connection
        .pragma_update(None, "application_id", 0x1357_2468_i64)
        .and_then(|_| connection.pragma_update(None, "user_version", 7))
        .map_err(sqlite_error)?;
    drop(connection);
    let error = PosterStateRepository::open(foreign_database.clone())
        .err()
        .ok_or_else(|| "foreign database was accepted".to_string())?;
    ensure(
        error.code == PosterStateStoreErrorCode::ForeignDatabase,
        format!("foreign database returned {}", error.code.as_str()),
    )?;
    ensure(
        read_pragma(&foreign_database, "user_version")? == 7,
        "foreign database was rewritten",
    )?;

    Ok(json!({
        "futureSchema": "rejectedWithoutRewrite",
        "foreignDatabase": "rejectedWithoutRewrite"
    }))
}

fn scenario_unreadable_and_query_failures(workspace: &Path) -> Result<Value, String> {
    let directory = scenario_directory(workspace, "database-failures")?;
    let directory_as_database = directory.join("directory.sqlite3");
    fs::create_dir(&directory_as_database).map_err(io_error)?;
    let error = PosterStateRepository::open(directory_as_database)
        .err()
        .ok_or_else(|| "a directory was accepted as a SQLite database".to_string())?;
    ensure(
        matches!(
            error.code,
            PosterStateStoreErrorCode::OpenDatabase | PosterStateStoreErrorCode::ConfigureDatabase
        ),
        format!("unreadable database returned {}", error.code.as_str()),
    )?;

    let missing_table = directory.join("missing-table.sqlite3");
    let repository = open_repository(&missing_table)?;
    let connection = Connection::open(&missing_table).map_err(sqlite_error)?;
    connection
        .execute_batch("DROP TABLE poster_state;")
        .map_err(sqlite_error)?;
    drop(connection);
    let error = repository
        .load()
        .err()
        .ok_or_else(|| "query failure did not return an error".to_string())?;
    ensure(
        error.code == PosterStateStoreErrorCode::QueryDatabase,
        format!("query failure returned {}", error.code.as_str()),
    )?;

    Ok(json!({
        "unreadablePath": "structuredError",
        "missingTable": "queryDatabaseFailed"
    }))
}

fn scenario_cas_conflict_and_validation_preserve(workspace: &Path) -> Result<Value, String> {
    let directory = scenario_directory(workspace, "cas")?;
    let database = directory.join("poster-state-v1.sqlite3");
    let repository = open_repository(&database)?;
    repository
        .replace_with_expected_revision(None, valid_snapshot())
        .map_err(store_error)?;

    let mut stale = valid_snapshot();
    stale["posters"][0]["title"] = json!("STALE WRITER");
    match repository
        .replace_with_expected_revision(Some(99), stale)
        .map_err(store_error)?
    {
        PosterStateMutationResult::Conflict {
            expected_revision: Some(99),
            actual_revision: Some(0),
        } => {}
        result => return Err(format!("unexpected CAS conflict result: {result:?}")),
    }
    match repository
        .replace_with_expected_revision(Some(0), json!({}))
        .map_err(store_error)?
    {
        PosterStateMutationResult::Rejected {
            code: PosterStateMutationRejectionCode::InvalidSnapshot,
            ..
        } => {}
        result => return Err(format!("unexpected validation result: {result:?}")),
    }
    let snapshot = expect_loaded(&repository, 0)?;
    ensure(
        snapshot["posters"][0]["title"] == json!("DESKTOP COLLECTION"),
        "CAS or validation failure overwrote the accepted snapshot",
    )?;

    Ok(json!({
        "conflictReloadRevision": 0,
        "validationFailurePreservedOldValue": true
    }))
}

fn scenario_transactional_write_failure(workspace: &Path) -> Result<Value, String> {
    let directory = scenario_directory(workspace, "transaction")?;
    let database = directory.join("poster-state-v1.sqlite3");
    let repository = open_repository(&database)?;
    repository
        .replace_with_expected_revision(None, valid_snapshot())
        .map_err(store_error)?;
    drop(repository);

    let connection = Connection::open(&database).map_err(sqlite_error)?;
    connection
        .execute_batch(
            "CREATE TRIGGER reject_recovery_update
             BEFORE UPDATE ON poster_state
             BEGIN
               SELECT RAISE(ABORT, 'injected recovery update failure');
             END;",
        )
        .map_err(sqlite_error)?;
    drop(connection);

    let repository = open_repository(&database)?;
    let mut update = valid_snapshot();
    update["posters"][0]["title"] = json!("MUST ROLL BACK");
    let error = repository
        .replace_with_expected_revision(Some(0), update)
        .err()
        .ok_or_else(|| "injected transaction failure was accepted".to_string())?;
    ensure(
        error.code == PosterStateStoreErrorCode::WriteDatabase,
        format!("transaction failure returned {}", error.code.as_str()),
    )?;
    let snapshot = expect_loaded(&repository, 0)?;
    ensure(
        snapshot["posters"][0]["title"] == json!("DESKTOP COLLECTION"),
        "failed transaction changed the prior value",
    )?;

    Ok(json!({
        "injectedFailure": "writeDatabaseFailed",
        "rollbackPreservedRevision": 0,
        "rollbackPreservedOldValue": true
    }))
}

fn scenario_legacy_backup_and_idempotence(workspace: &Path) -> Result<Value, String> {
    let directory = scenario_directory(workspace, "legacy-idempotence")?;
    let database = directory.join("poster-state-v1.sqlite3");
    let repository = open_repository(&database)?;
    let first = repository
        .migrate_legacy(legacy_migration_request())
        .map_err(store_error)?;
    let backup_path = match first {
        LegacyPosterMigrationResult::Migrated {
            revision: 0,
            backup_path,
            snapshot,
            ..
        } => {
            ensure(
                snapshot["posters"].as_array().map(Vec::len) == Some(1),
                "legacy migration did not keep the first-poster scope",
            )?;
            PathBuf::from(backup_path)
        }
        result => return Err(format!("unexpected legacy migration result: {result:?}")),
    };
    let backup = serde_json::from_slice::<Value>(&fs::read(&backup_path).map_err(io_error)?)
        .map_err(|error| error.to_string())?;
    ensure(
        backup["legacySnapshot"]["posters"].as_array().map(Vec::len) == Some(2),
        "legacy backup did not retain the complete two-poster source",
    )?;

    match repository
        .migrate_legacy(legacy_migration_request())
        .map_err(store_error)?
    {
        LegacyPosterMigrationResult::SkippedExistingState { revision: 0, .. } => {}
        result => {
            return Err(format!(
                "repeated legacy migration was not idempotent: {result:?}"
            ))
        }
    }
    ensure(
        count_backup_files(&directory)? == 1,
        "repeated legacy migration created a second backup",
    )?;
    expect_loaded(&repository, 0)?;

    Ok(json!({
        "formalPosterCount": 1,
        "retainedLegacyPosterCount": 2,
        "backupCountAfterRepeat": 1,
        "formalRevisionAfterRepeat": 0
    }))
}

fn scenario_legacy_failure_source_retention(workspace: &Path) -> Result<Value, String> {
    let directory = scenario_directory(workspace, "legacy-failures")?;

    let adapter_directory = directory.join("adapter");
    fs::create_dir(&adapter_directory).map_err(io_error)?;
    let repository = open_repository(&adapter_directory.join("poster-state-v1.sqlite3"))?;
    let mut adapter_request = legacy_migration_request();
    adapter_request.adapted_snapshot = None;
    adapter_request.adapter_errors = vec![LegacyAdapterDiagnostic {
        path: "$.posters[0]".to_string(),
        code: "injected_adapter_failure".to_string(),
        message: "recovery matrix adapter failure".to_string(),
    }];
    match repository
        .migrate_legacy(adapter_request)
        .map_err(store_error)?
    {
        LegacyPosterMigrationResult::Rejected {
            code: LegacyPosterMigrationRejectionCode::AdapterFailed,
            backup_path: Some(_),
            ..
        } => {}
        result => return Err(format!("unexpected adapter failure result: {result:?}")),
    }
    ensure(
        matches!(
            repository.load().map_err(store_error)?,
            PosterStateLoadResult::NoData
        ),
        "adapter failure created formal state",
    )?;
    ensure(
        count_backup_files(&adapter_directory)? == 1,
        "adapter failure did not retain the legacy source backup",
    )?;
    match repository
        .migrate_legacy(legacy_migration_request())
        .map_err(store_error)?
    {
        LegacyPosterMigrationResult::Migrated { revision: 0, .. } => {}
        result => return Err(format!("adapter recovery did not migrate: {result:?}")),
    }
    expect_loaded(&repository, 0)?;

    let backup_directory = directory.join("backup");
    fs::create_dir(&backup_directory).map_err(io_error)?;
    let repository = open_repository(&backup_directory.join("poster-state-v1.sqlite3"))?;
    fs::write(
        backup_directory.join(LEGACY_POSTER_BACKUP_DIRECTORY),
        b"blocks backup directory creation",
    )
    .map_err(io_error)?;
    let error = repository
        .migrate_legacy(legacy_migration_request())
        .err()
        .ok_or_else(|| "blocked backup directory did not fail".to_string())?;
    ensure(
        error.code == PosterStateStoreErrorCode::CreateBackupDirectory,
        format!("backup failure returned {}", error.code.as_str()),
    )?;
    ensure(
        matches!(
            repository.load().map_err(store_error)?,
            PosterStateLoadResult::NoData
        ),
        "backup failure created formal state",
    )?;
    fs::remove_file(backup_directory.join(LEGACY_POSTER_BACKUP_DIRECTORY)).map_err(io_error)?;
    match repository
        .migrate_legacy(legacy_migration_request())
        .map_err(store_error)?
    {
        LegacyPosterMigrationResult::Migrated { revision: 0, .. } => {}
        result => return Err(format!("backup recovery did not migrate: {result:?}")),
    }
    ensure(
        count_backup_files(&backup_directory)? == 1,
        "backup recovery did not create exactly one retained source",
    )?;
    expect_loaded(&repository, 0)?;

    let database_directory = directory.join("database");
    fs::create_dir(&database_directory).map_err(io_error)?;
    let database = database_directory.join("poster-state-v1.sqlite3");
    let repository = open_repository(&database)?;
    drop(repository);
    let connection = Connection::open(&database).map_err(sqlite_error)?;
    connection
        .execute_batch(
            "CREATE TRIGGER reject_recovery_migration
             BEFORE INSERT ON poster_state
             BEGIN
               SELECT RAISE(ABORT, 'injected recovery migration failure');
             END;",
        )
        .map_err(sqlite_error)?;
    drop(connection);
    let repository = open_repository(&database)?;
    let error = repository
        .migrate_legacy(legacy_migration_request())
        .err()
        .ok_or_else(|| "injected migration write failure was accepted".to_string())?;
    ensure(
        error.code == PosterStateStoreErrorCode::WriteDatabase,
        format!("migration write failure returned {}", error.code.as_str()),
    )?;
    ensure(
        matches!(
            repository.load().map_err(store_error)?,
            PosterStateLoadResult::NoData
        ),
        "migration write failure created formal state",
    )?;
    ensure(
        count_backup_files(&database_directory)? == 1,
        "migration write failure did not retain the legacy source backup",
    )?;
    drop(repository);
    let connection = Connection::open(&database).map_err(sqlite_error)?;
    connection
        .execute_batch("DROP TRIGGER reject_recovery_migration;")
        .map_err(sqlite_error)?;
    drop(connection);
    let repository = open_repository(&database)?;
    match repository
        .migrate_legacy(legacy_migration_request())
        .map_err(store_error)?
    {
        LegacyPosterMigrationResult::Migrated { revision: 0, .. } => {}
        result => return Err(format!("database recovery did not migrate: {result:?}")),
    }
    ensure(
        count_backup_files(&database_directory)? == 1,
        "database recovery duplicated the retained source backup",
    )?;
    expect_loaded(&repository, 0)?;

    Ok(json!({
        "recoveriesAfterRepair": ["adapter", "backup", "database"],
        "adapterFailure": { "backupRetained": true, "formalStateCreated": false },
        "backupFailure": { "formalStateCreated": false },
        "databaseFailure": { "backupRetained": true, "formalStateCreated": false }
    }))
}

fn scenario_directory(workspace: &Path, name: &str) -> Result<PathBuf, String> {
    let directory = workspace.join(name);
    fs::create_dir(&directory).map_err(io_error)?;
    Ok(directory)
}

fn open_repository(path: &Path) -> Result<PosterStateRepository, String> {
    PosterStateRepository::open(path.to_path_buf()).map_err(store_error)
}

fn expect_loaded(repository: &PosterStateRepository, revision: u64) -> Result<Value, String> {
    match repository.load().map_err(store_error)? {
        PosterStateLoadResult::Loaded {
            revision: actual,
            snapshot,
        } if actual == revision => Ok(snapshot),
        result => Err(format!(
            "expected loaded revision {revision}, received {result:?}"
        )),
    }
}

fn expect_load_rejection(
    path: &Path,
    expected: PosterStateLoadRejectionCode,
) -> Result<(), String> {
    let repository = open_repository(path)?;
    match repository.load().map_err(store_error)? {
        PosterStateLoadResult::Rejected { code, .. } if code == expected => Ok(()),
        result => Err(format!(
            "expected load rejection {}, received {result:?}",
            expected.as_str()
        )),
    }
}

fn create_raw_schema(path: &Path) -> Result<Connection, String> {
    let connection = Connection::open(path).map_err(sqlite_error)?;
    connection
        .execute_batch(
            "CREATE TABLE poster_state (
                id INTEGER PRIMARY KEY,
                contract_version INTEGER NOT NULL,
                revision INTEGER NOT NULL,
                selected_poster_id TEXT NOT NULL,
                snapshot_json TEXT NOT NULL
             ) STRICT;",
        )
        .and_then(|_| connection.pragma_update(None, "application_id", POSTER_STATE_APPLICATION_ID))
        .and_then(|_| connection.pragma_update(None, "user_version", POSTER_STATE_SCHEMA_VERSION))
        .map_err(sqlite_error)?;
    Ok(connection)
}

fn write_raw_row(
    path: &Path,
    contract_version: i64,
    revision: i64,
    selected_poster_id: &str,
    snapshot_json: &str,
) -> Result<(), String> {
    let connection = create_raw_schema(path)?;
    connection
        .execute(
            "INSERT INTO poster_state (
                id, contract_version, revision, selected_poster_id, snapshot_json
             ) VALUES (1, ?1, ?2, ?3, ?4)",
            params![
                contract_version,
                revision,
                selected_poster_id,
                snapshot_json
            ],
        )
        .map_err(sqlite_error)?;
    Ok(())
}

fn read_pragma(path: &Path, pragma: &str) -> Result<i64, String> {
    let connection = Connection::open(path).map_err(sqlite_error)?;
    connection
        .query_row(&format!("PRAGMA {pragma}"), [], |row| row.get(0))
        .map_err(sqlite_error)
}

fn count_backup_files(repository_directory: &Path) -> Result<usize, String> {
    fs::read_dir(repository_directory.join(LEGACY_POSTER_BACKUP_DIRECTORY))
        .map_err(io_error)?
        .try_fold(0_usize, |count, entry| {
            let entry = entry.map_err(io_error)?;
            Ok(count + usize::from(entry.path().extension().is_some_and(|ext| ext == "json")))
        })
}

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

fn write_report_atomically(
    report_path: &Path,
    report: &PosterStateRecoveryGateReport,
) -> Result<(), String> {
    let bytes = serde_json::to_vec_pretty(report).map_err(|error| error.to_string())?;
    let unique = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_nanos();
    let temporary =
        report_path.with_extension(format!("recovery-{}-{unique}.tmp", std::process::id()));
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
        return Err(format!(
            "failed to write temporary recovery report {}: {error}",
            temporary.display()
        ));
    }
    fs::rename(&temporary, report_path).map_err(|error| {
        let _ = fs::remove_file(&temporary);
        format!(
            "failed to publish recovery report {}: {error}",
            report_path.display()
        )
    })
}

fn ensure(condition: bool, message: impl Into<String>) -> Result<(), String> {
    if condition {
        Ok(())
    } else {
        Err(message.into())
    }
}

fn store_error(error: impl std::fmt::Display) -> String {
    error.to_string()
}

fn sqlite_error(error: rusqlite::Error) -> String {
    error.to_string()
}

fn io_error(error: std::io::Error) -> String {
    error.to_string()
}

#[cfg(test)]
mod tests {
    use std::{fs, time::SystemTime};

    use super::{run_isolated_recovery_matrix, RECOVERY_GATE_CONTRACT};

    #[test]
    fn isolated_release_recovery_matrix_passes() {
        let unique = SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .expect("system clock")
            .as_nanos();
        let directory = std::env::temp_dir().join(format!(
            "desktop-up-cpss05-matrix-{}-{unique}",
            std::process::id()
        ));
        fs::create_dir(&directory).expect("create isolated matrix directory");

        let report = run_isolated_recovery_matrix(&directory);
        assert_eq!(report.contract_version, RECOVERY_GATE_CONTRACT);
        assert!(report.isolated);
        assert_eq!(report.scenarios.len(), 8);
        assert!(
            report.passed,
            "recovery violations: {:?}",
            report.violations
        );

        fs::remove_dir_all(&directory).expect("remove isolated matrix directory");
    }
}
