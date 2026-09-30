using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DesktopUp.Application;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

public sealed class SqliteStateStore : IStateStore
{
    private const int ApplicationId = 0x44555033; // "DUP3"
    private const int BusyTimeoutMilliseconds = 5_000;
    // 11 registers support for content layout document v2 (single flow axis + optional heat-driven
    // size). It adds no column: those members live inside content_layout_json and are validated on read.
    private const int CurrentSchemaVersion = 12;
    private const string DatabaseFileName = "desktopup.sqlite3";
    private static readonly Migration[] Migrations =
    [
        new(
            1,
            "state-foundation",
            """
            CREATE TABLE schema_migrations (
                version INTEGER PRIMARY KEY NOT NULL,
                name TEXT NOT NULL,
                checksum_sha256 TEXT NOT NULL,
                applied_utc TEXT NOT NULL
            ) STRICT;

            CREATE TABLE state_head (
                singleton_id INTEGER PRIMARY KEY NOT NULL CHECK (singleton_id = 1),
                schema_version INTEGER NOT NULL,
                revision INTEGER NOT NULL CHECK (revision >= 0),
                last_operation_id TEXT NULL,
                updated_utc TEXT NOT NULL
            ) STRICT;

            CREATE TABLE state_operations (
                operation_id TEXT PRIMARY KEY NOT NULL,
                expected_revision INTEGER NOT NULL CHECK (expected_revision >= 0),
                committed_revision INTEGER NOT NULL UNIQUE,
                committed_utc TEXT NOT NULL,
                CHECK (committed_revision = expected_revision + 1)
            ) STRICT;
            """),
        new(
            2,
            "poster-and-settings-repositories",
            """
            CREATE TABLE posters (
                poster_id TEXT PRIMARY KEY NOT NULL,
                title TEXT NOT NULL,
                subtitle TEXT NOT NULL,
                monitor_device_name TEXT NOT NULL,
                fallback_x_dip REAL NOT NULL,
                fallback_y_dip REAL NOT NULL,
                width_dip REAL NOT NULL CHECK (width_dip >= 240.0),
                expanded_height_dip REAL NOT NULL CHECK (expanded_height_dip >= 240.0),
                horizontal_anchor REAL NOT NULL CHECK (horizontal_anchor >= 0.0 AND horizontal_anchor <= 1.0),
                vertical_anchor REAL NOT NULL CHECK (vertical_anchor >= 0.0 AND vertical_anchor <= 1.0),
                normalized_width REAL NOT NULL CHECK (normalized_width > 0.0 AND normalized_width <= 1.0),
                normalized_expanded_height REAL NOT NULL CHECK (normalized_expanded_height > 0.0 AND normalized_expanded_height <= 1.0),
                is_locked INTEGER NOT NULL CHECK (is_locked IN (0, 1)),
                is_collapsed INTEGER NOT NULL CHECK (is_collapsed IN (0, 1)),
                auto_collapse_enabled INTEGER NOT NULL CHECK (auto_collapse_enabled IN (0, 1)),
                layout_kind TEXT NOT NULL,
                layout_parameters_json TEXT NOT NULL CHECK (json_valid(layout_parameters_json)),
                template_key TEXT NOT NULL,
                state_revision INTEGER NOT NULL CHECK (state_revision >= 0),
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            ) STRICT;

            CREATE TABLE app_settings (
                singleton_id INTEGER PRIMARY KEY NOT NULL CHECK (singleton_id = 1),
                dynamic_effects_paused INTEGER NOT NULL CHECK (dynamic_effects_paused IN (0, 1)),
                performance_mode TEXT NOT NULL CHECK (performance_mode IN ('automatic', 'static', '30fps')),
                launch_at_startup_requested INTEGER NOT NULL CHECK (launch_at_startup_requested IN (0, 1)),
                state_revision INTEGER NOT NULL CHECK (state_revision >= 0),
                updated_utc TEXT NOT NULL
            ) STRICT;

            INSERT INTO app_settings (
                singleton_id,
                dynamic_effects_paused,
                performance_mode,
                launch_at_startup_requested,
                state_revision,
                updated_utc)
            VALUES (
                1,
                0,
                'automatic',
                0,
                0,
                strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
            """),
        new(
            3,
            "multi-poster-catalog",
            """
            ALTER TABLE posters ADD COLUMN purpose TEXT NOT NULL DEFAULT 'general';
            ALTER TABLE posters ADD COLUMN is_visible INTEGER NOT NULL DEFAULT 1 CHECK (is_visible IN (0, 1));
            ALTER TABLE posters ADD COLUMN stack_order INTEGER NOT NULL DEFAULT 0 CHECK (stack_order >= 0);
            ALTER TABLE posters ADD COLUMN entity_version INTEGER NOT NULL DEFAULT 1 CHECK (entity_version >= 1);
            ALTER TABLE posters ADD COLUMN collection_id TEXT NULL;
            ALTER TABLE posters ADD COLUMN theme_instance_id TEXT NOT NULL DEFAULT '';
            UPDATE posters SET theme_instance_id = 'theme-' || poster_id;
            UPDATE posters SET stack_order = (
                SELECT COUNT(*) FROM posters p WHERE p.poster_id < posters.poster_id);
            CREATE UNIQUE INDEX posters_theme_instance_id ON posters(theme_instance_id);
            CREATE INDEX posters_stack_order ON posters(stack_order, poster_id);

            CREATE TABLE poster_profile (
                singleton_id INTEGER PRIMARY KEY NOT NULL CHECK (singleton_id = 1),
                profile_initialized INTEGER NOT NULL CHECK (profile_initialized IN (0, 1)),
                catalog_version INTEGER NOT NULL CHECK (catalog_version >= 0),
                state_revision INTEGER NOT NULL CHECK (state_revision >= 0)
            ) STRICT;
            INSERT INTO poster_profile
            SELECT 1, EXISTS(SELECT 1 FROM posters),
                CASE WHEN EXISTS(SELECT 1 FROM posters) THEN 1 ELSE 0 END, revision
            FROM state_head WHERE singleton_id = 1;

            CREATE TABLE poster_catalog_operations (
                operation_id TEXT PRIMARY KEY NOT NULL,
                request_sha256 TEXT NOT NULL,
                result_poster_id TEXT NULL,
                FOREIGN KEY (operation_id) REFERENCES state_operations(operation_id)
                    DEFERRABLE INITIALLY DEFERRED
            ) STRICT;
            """),
        new(
            4,
            "scoped-command-receipts",
            """
            ALTER TABLE app_settings ADD COLUMN entity_version INTEGER NOT NULL DEFAULT 1 CHECK (entity_version >= 1);
            CREATE TABLE state_command_receipts (
                request_id TEXT PRIMARY KEY NOT NULL,
                request_sha256 TEXT NOT NULL,
                outcome TEXT NOT NULL CHECK (outcome IN ('committed', 'no_change')),
                observed_revision INTEGER NOT NULL CHECK (observed_revision >= 0),
                committed_revision INTEGER NULL,
                result_poster_id TEXT NULL,
                CHECK ((outcome = 'committed' AND committed_revision IS NOT NULL) OR
                    (outcome = 'no_change' AND committed_revision IS NULL)),
                FOREIGN KEY (committed_revision) REFERENCES state_operations(committed_revision)
                    DEFERRABLE INITIALLY DEFERRED
            ) STRICT;
            """),
        new(5, "spatial-layout-causality", """
            ALTER TABLE posters ADD COLUMN layout_generation INTEGER NOT NULL DEFAULT 0 CHECK (layout_generation >= 0);
            ALTER TABLE posters ADD COLUMN last_layout_operation_id TEXT NULL;
            CREATE TABLE spatial_operations (
                operation_id TEXT PRIMARY KEY NOT NULL,
                source_poster_id TEXT NOT NULL REFERENCES posters(poster_id) ON DELETE CASCADE,
                schema_version INTEGER NOT NULL CHECK (schema_version = 1),
                status TEXT NOT NULL CHECK (status IN ('active', 'consumed', 'invalidated')),
                body_json TEXT NOT NULL CHECK (json_valid(body_json)),
                committed_revision INTEGER NOT NULL REFERENCES state_operations(committed_revision) DEFERRABLE INITIALLY DEFERRED
            ) STRICT;
            CREATE INDEX spatial_source ON spatial_operations(source_poster_id, status);
            CREATE TABLE spatial_participants (
                operation_id TEXT NOT NULL REFERENCES spatial_operations(operation_id) ON DELETE CASCADE,
                poster_id TEXT NOT NULL,
                expected_layout_generation INTEGER NOT NULL CHECK (expected_layout_generation >= 1),
                proof_json TEXT NOT NULL CHECK (json_valid(proof_json)),
                invalidated_reason TEXT NULL,
                PRIMARY KEY(operation_id, poster_id)
            ) STRICT;
            """),
        new(
            6,
            "spatial-continuity",
            """
            CREATE TABLE spatial_continuity (
                workspace_id TEXT PRIMARY KEY NOT NULL,
                schema_version INTEGER NOT NULL CHECK (schema_version = 1),
                body_json TEXT NOT NULL CHECK (json_valid(body_json)),
                committed_revision INTEGER NOT NULL REFERENCES state_operations(committed_revision) DEFERRABLE INITIALLY DEFERRED
            ) STRICT;
            """),
        new(
            7,
            "poster-auto-posture-settings",
            """
            ALTER TABLE posters ADD COLUMN auto_collapse_delay_seconds INTEGER NOT NULL DEFAULT 3
                CHECK (auto_collapse_delay_seconds BETWEEN 1 AND 300);
            """),
        new(8, "canonical-created-poster-layout", """
            -- BUG-005: only the exact shipped basic-box payload AND a committed UI catalog receipt.
            -- Never interpret arbitrary grid/custom/future/edited JSON as this historical mistake.
            CREATE TEMP TABLE canonical_layout_repair_ids (poster_id TEXT PRIMARY KEY NOT NULL);
            INSERT INTO canonical_layout_repair_ids
            SELECT p.poster_id FROM posters p
            WHERE p.layout_kind = 'grid' AND p.template_key = 'basic-box'
              AND p.layout_parameters_json = '{"schemaVersion":1,"content":{"itemWidthDip":100,"itemHeightDip":72,"iconSizeDip":40,"gapDip":12,"labelFontSizeDip":12,"titleFontSizeDip":24},"theme":{"background":"#282B30","foreground":"#F2F2F2","accent":"#68BDFF"}}'
              AND EXISTS (SELECT 1 FROM state_command_receipts r
                  WHERE r.result_poster_id = p.poster_id AND r.outcome = 'committed'
                    AND (r.request_id GLOB 'controlcenter-catalog-*' OR r.request_id GLOB 'tray-create-*'));
            INSERT INTO state_operations (operation_id,expected_revision,committed_revision,committed_utc)
            SELECT 'migration-v8-canonical-created-layout',revision,revision+1,strftime('%Y-%m-%dT%H:%M:%fZ','now')
            FROM state_head WHERE singleton_id=1 AND EXISTS(SELECT 1 FROM canonical_layout_repair_ids);
            UPDATE posters SET layout_kind='adaptive-grid',entity_version=entity_version+1,
                state_revision=(SELECT revision+1 FROM state_head WHERE singleton_id=1),
                updated_utc=strftime('%Y-%m-%dT%H:%M:%fZ','now')
            WHERE poster_id IN (SELECT poster_id FROM canonical_layout_repair_ids);
            UPDATE state_head SET revision=revision+1,last_operation_id='migration-v8-canonical-created-layout'
            WHERE singleton_id=1 AND EXISTS(SELECT 1 FROM canonical_layout_repair_ids);
            DROP TABLE canonical_layout_repair_ids;
            """),
        new(9, "poster-placement-pending", """
            ALTER TABLE posters ADD COLUMN placement_pending INTEGER NOT NULL DEFAULT 0
                CHECK (placement_pending IN (0, 1));
            """),
        new(10, "poster-content-layout-document", """
            ALTER TABLE posters ADD COLUMN content_layout_json TEXT NULL;
            """),
        new(11, "poster-content-layout-v2-axis", """
            -- Registration only. The single scroll axis and the optional heat-driven size switch are
            -- carried inside content_layout_json and validated on read by ContentLayoutPolicy, so no
            -- column is added and no existing document is rewritten by this migration.
            SELECT 1;
            """),
        new(12, "declarative-template-catalog-and-center-theme", """
            ALTER TABLE app_settings ADD COLUMN center_theme TEXT NOT NULL DEFAULT 'light'
                CHECK (center_theme IN ('light', 'ink', 'blue', 'system'));
            ALTER TABLE app_settings ADD COLUMN template_catalog_json TEXT NOT NULL DEFAULT '[]'
                CHECK (json_valid(template_catalog_json));
            """)
    ];

    private readonly object _gate = new();
    private readonly IUserDataPaths _paths;
    private readonly IStructuredLog? _log;
    private readonly string _databasePath;
    private bool _disposed;

    public SqliteStateStore(IUserDataPaths paths, IStructuredLog? log = null)
    {
        _paths = paths;
        _log = log;
        _databasePath = Path.Combine(paths.State, DatabaseFileName);
    }

    public int LatestSchemaVersion => CurrentSchemaVersion;

    public StateStoreInitializationResult Initialize()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _paths.EnsureCreated();
            bool existedWithContent = File.Exists(_databasePath) &&
                new FileInfo(_databasePath).Length > 0;
            string? backupFileName = null;
            int previousVersion = 0;

            try
            {
                using SqliteConnection connection = OpenWritableConnection();
                AssertIntegrity(connection);
                previousVersion = ReadPragmaInt32(connection, "user_version");
                int applicationId = ReadPragmaInt32(connection, "application_id");
                if (previousVersion > CurrentSchemaVersion)
                {
                    throw new InvalidDataException(
                        $"State schema {previousVersion} is newer than supported schema {CurrentSchemaVersion}.");
                }
                if (applicationId != 0 && applicationId != ApplicationId)
                {
                    throw new InvalidDataException(
                        $"Unexpected SQLite application_id {applicationId}.");
                }

                if (previousVersion < CurrentSchemaVersion && existedWithContent)
                {
                    StateBackupResult backup = CreateBackupFromOpenConnection(
                        connection,
                        $"pre-migration-v{previousVersion}",
                        previousVersion);
                    if (!backup.Succeeded)
                    {
                        throw new IOException(
                            $"Pre-migration backup failed: {backup.Error}");
                    }
                    backupFileName = backup.FileName;
                }

                ConfigureWritableConnection(connection);
                if (previousVersion < CurrentSchemaVersion)
                {
                    ApplyMigrations(connection, previousVersion);
                }

                StateStoreHead head = VerifyCurrentSchema(connection);
                StateStoreInitializationStatus status = previousVersion switch
                {
                    CurrentSchemaVersion => StateStoreInitializationStatus.Ready,
                    _ when existedWithContent => StateStoreInitializationStatus.Migrated,
                    _ => StateStoreInitializationStatus.Created
                };

                _log?.Write("info", "state_store.initialized", new
                {
                    status = status.ToString(),
                    previousVersion,
                    schemaVersion = head.SchemaVersion,
                    head.Revision,
                    backupFileName
                });
                return new StateStoreInitializationResult(
                    status,
                    previousVersion,
                    head,
                    backupFileName,
                    null);
            }
            catch (Exception exception)
            {
                _log?.Write("error", "state_store.initialization_failed", new
                {
                    previousVersion,
                    exceptionType = exception.GetType().FullName,
                    exception.HResult,
                    exception.Message
                });
                return new StateStoreInitializationResult(
                    StateStoreInitializationStatus.Failed,
                    previousVersion,
                    null,
                    backupFileName,
                    exception.Message);
            }
        }
    }

    public StateStoreHead ReadHead()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            using SqliteConnection connection = OpenWritableConnection();
            return VerifyCurrentSchema(connection);
        }
    }

    public StateBackupResult CreateBackup(string reason = "manual")
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            try
            {
                using SqliteConnection connection = OpenWritableConnection();
                VerifyCurrentSchema(connection);
                return CreateBackupFromOpenConnection(connection, reason);
            }
            catch (Exception exception)
            {
                _log?.Write("error", "state_store.backup_failed", new
                {
                    reason = SanitizeReason(reason),
                    exceptionType = exception.GetType().FullName,
                    exception.HResult,
                    exception.Message
                });
                return new StateBackupResult(false, null, null, exception.Message);
            }
        }
    }

    public StateBackupValidationResult ValidateBackup(string backupPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        lock (_gate)
        {
            ThrowIfDisposed();
            return ValidateBackupCore(Path.GetFullPath(backupPath));
        }
    }

    internal StateCompareAndSwapResult ExecuteCompareAndSwap(
        long expectedRevision,
        string operationId,
        Action<SqliteConnection, SqliteTransaction, long>? mutation = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedRevision);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);

        lock (_gate)
        {
            ThrowIfDisposed();
            using SqliteConnection connection = OpenWritableConnection();
            using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
            StateStoreHead head = ReadHead(connection, transaction);

            using (SqliteCommand scoped = connection.CreateCommand())
            {
                scoped.Transaction = transaction;
                scoped.CommandText = "SELECT COUNT(*) FROM state_command_receipts WHERE request_id = $id;";
                scoped.Parameters.AddWithValue("$id", operationId);
                if (Convert.ToInt64(scoped.ExecuteScalar(), CultureInfo.InvariantCulture) != 0)
                    throw new InvalidOperationException("This request id belongs to the scoped command protocol.");
            }

            (long Expected, long Committed)? prior = ReadOperation(
                connection,
                transaction,
                operationId);
            if (prior is not null)
            {
                if (prior.Value.Expected != expectedRevision)
                {
                    transaction.Rollback();
                    throw new InvalidOperationException(
                        "An operation id cannot be reused with another expected revision.");
                }
                transaction.Rollback();
                return new StateCompareAndSwapResult(
                    StateCompareAndSwapStatus.Replayed,
                    operationId,
                    expectedRevision,
                    head.Revision,
                    prior.Value.Committed);
            }

            if (head.Revision != expectedRevision)
            {
                transaction.Rollback();
                return new StateCompareAndSwapResult(
                    StateCompareAndSwapStatus.Conflict,
                    operationId,
                    expectedRevision,
                    head.Revision,
                    null);
            }

            long committedRevision = checked(expectedRevision + 1);
            mutation?.Invoke(connection, transaction, committedRevision);
            string committedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            using (SqliteCommand update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText =
                    """
                    UPDATE state_head
                    SET revision = $committedRevision,
                        last_operation_id = $operationId,
                        updated_utc = $committedUtc
                    WHERE singleton_id = 1 AND revision = $expectedRevision;
                    """;
                update.Parameters.AddWithValue("$committedRevision", committedRevision);
                update.Parameters.AddWithValue("$operationId", operationId);
                update.Parameters.AddWithValue("$committedUtc", committedUtc);
                update.Parameters.AddWithValue("$expectedRevision", expectedRevision);
                if (update.ExecuteNonQuery() != 1)
                {
                    transaction.Rollback();
                    return new StateCompareAndSwapResult(
                        StateCompareAndSwapStatus.Conflict,
                        operationId,
                        expectedRevision,
                        head.Revision,
                        null);
                }
            }

            using (SqliteCommand journal = connection.CreateCommand())
            {
                journal.Transaction = transaction;
                journal.CommandText =
                    """
                    INSERT INTO state_operations (
                        operation_id,
                        expected_revision,
                        committed_revision,
                        committed_utc)
                    VALUES ($operationId, $expectedRevision, $committedRevision, $committedUtc);
                    """;
                journal.Parameters.AddWithValue("$operationId", operationId);
                journal.Parameters.AddWithValue("$expectedRevision", expectedRevision);
                journal.Parameters.AddWithValue("$committedRevision", committedRevision);
                journal.Parameters.AddWithValue("$committedUtc", committedUtc);
                journal.ExecuteNonQuery();
            }

            transaction.Commit();
            _log?.Write("info", "state_store.cas_committed", new
            {
                operationId,
                expectedRevision,
                committedRevision
            });
            return new StateCompareAndSwapResult(
                StateCompareAndSwapStatus.Committed,
                operationId,
                expectedRevision,
                committedRevision,
                committedRevision);
        }
    }

    internal TResult ExecuteWrite<TResult>(Func<SqliteConnection, SqliteTransaction, TResult> mutation)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            using SqliteConnection connection = OpenWritableConnection();
            VerifyCurrentSchema(connection);
            using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
            TResult result = mutation(connection, transaction);
            transaction.Commit();
            return result;
        }
    }

    internal TResult ExecuteRead<TResult>(
        Func<SqliteConnection, SqliteTransaction, TResult> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        lock (_gate)
        {
            ThrowIfDisposed();
            using SqliteConnection connection = OpenWritableConnection();
            VerifyCurrentSchema(connection);
            using SqliteTransaction transaction = connection.BeginTransaction(deferred: true);
            TResult result = query(connection, transaction);
            transaction.Commit();
            return result;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }
    }

    private SqliteConnection OpenWritableConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
            DefaultTimeout = BusyTimeoutMilliseconds / 1_000
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        ConfigureConnectionSession(connection);
        return connection;
    }

    private static SqliteConnection OpenReadOnlyConnection(string path)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = BusyTimeoutMilliseconds / 1_000
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        ConfigureConnectionSession(connection);
        return connection;
    }

    private static void ConfigureConnectionSession(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA foreign_keys=ON; PRAGMA busy_timeout={BusyTimeoutMilliseconds};";
        command.ExecuteNonQuery();
    }

    private static void ConfigureWritableConnection(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL;";
        command.ExecuteNonQuery();
    }

    private static void ApplyMigrations(SqliteConnection connection, int previousVersion)
    {
        foreach (Migration migration in Migrations.Where(item => item.Version > previousVersion))
        {
            using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
            using (SqliteCommand schema = connection.CreateCommand())
            {
                schema.Transaction = transaction;
                schema.CommandText = migration.Sql;
                schema.ExecuteNonQuery();
            }

            if (migration.Version >= 3)
            {
                // Never certify a batched DDL script solely by its return value.
                // Verify the resulting contract before recording/committing the version.
                VerifyCatalogSchema(connection, transaction);
            }
            if (migration.Version >= 4) VerifyCommandSchema(connection, transaction);
            if (migration.Version >= 5) VerifySpatialSchema(connection, transaction);
            if (migration.Version >= 6) VerifyContinuitySchema(connection, transaction);
            if (migration.Version >= 7) VerifyAutoPostureSchema(connection, transaction);
            if (migration.Version >= 9) VerifyPendingPlacementSchema(connection, transaction);
            if (migration.Version >= 10) VerifyContentLayoutSchema(connection, transaction);
            if (migration.Version >= 12) VerifyTemplateSettingsSchema(connection, transaction);

            string appliedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            using (SqliteCommand ledger = connection.CreateCommand())
            {
                ledger.Transaction = transaction;
                ledger.CommandText =
                    """
                    INSERT INTO schema_migrations (version, name, checksum_sha256, applied_utc)
                    VALUES ($version, $name, $checksum, $appliedUtc);
                    """;
                ledger.Parameters.AddWithValue("$version", migration.Version);
                ledger.Parameters.AddWithValue("$name", migration.Name);
                ledger.Parameters.AddWithValue("$checksum", migration.Checksum);
                ledger.Parameters.AddWithValue("$appliedUtc", appliedUtc);
                ledger.ExecuteNonQuery();
            }

            using (SqliteCommand head = connection.CreateCommand())
            {
                head.Transaction = transaction;
                head.CommandText = migration.Version == 1
                    ? """
                      INSERT INTO state_head (
                          singleton_id,
                          schema_version,
                          revision,
                          last_operation_id,
                          updated_utc)
                      VALUES (1, $schemaVersion, 0, NULL, $updatedUtc);
                      """
                    : """
                      UPDATE state_head
                      SET schema_version = $schemaVersion,
                          updated_utc = $updatedUtc
                      WHERE singleton_id = 1;
                      """;
                head.Parameters.AddWithValue("$schemaVersion", migration.Version);
                head.Parameters.AddWithValue("$updatedUtc", appliedUtc);
                if (head.ExecuteNonQuery() != 1)
                {
                    throw new InvalidDataException(
                        $"State head migration failed at schema version {migration.Version}.");
                }
            }

            using (SqliteCommand pragma = connection.CreateCommand())
            {
                pragma.Transaction = transaction;
                pragma.CommandText =
                    $"PRAGMA application_id={ApplicationId}; PRAGMA user_version={migration.Version};";
                pragma.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }

    private static StateStoreHead VerifyCurrentSchema(SqliteConnection connection)
    {
        AssertIntegrity(connection);
        int version = ReadPragmaInt32(connection, "user_version");
        int applicationId = ReadPragmaInt32(connection, "application_id");
        if (version != CurrentSchemaVersion || applicationId != ApplicationId)
        {
            throw new InvalidDataException(
                $"State identity mismatch (schema={version}, application_id={applicationId}).");
        }

        return VerifySchemaVersion(connection, version);
    }

    private static StateStoreHead VerifySchemaVersion(
        SqliteConnection connection,
        int expectedVersion)
    {
        int version = ReadPragmaInt32(connection, "user_version");
        int applicationId = ReadPragmaInt32(connection, "application_id");
        if (version != expectedVersion || applicationId != ApplicationId)
        {
            throw new InvalidDataException(
                $"State identity mismatch (schema={version}, application_id={applicationId}).");
        }

        foreach (Migration migration in Migrations.Where(item => item.Version <= expectedVersion))
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT name, checksum_sha256
                FROM schema_migrations
                WHERE version = $version;
                """;
            command.Parameters.AddWithValue("$version", migration.Version);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read() ||
                reader.GetString(0) != migration.Name ||
                reader.GetString(1) != migration.Checksum)
            {
                throw new InvalidDataException(
                    $"Migration ledger mismatch at schema version {migration.Version}.");
            }
        }
        StateStoreHead head = ReadHead(connection, null);
        if (expectedVersion >= 3)
        {
            VerifyCatalogSchema(connection, null);
        }
        if (expectedVersion >= 4) VerifyCommandSchema(connection, null);
        if (expectedVersion >= 5) VerifySpatialSchema(connection, null);
        if (expectedVersion >= 6) VerifyContinuitySchema(connection, null);
        if (expectedVersion >= 7) VerifyAutoPostureSchema(connection, null);
        if (expectedVersion >= 9) VerifyPendingPlacementSchema(connection, null);
        if (expectedVersion >= 10) VerifyContentLayoutSchema(connection, null);
        if (expectedVersion >= 12) VerifyTemplateSettingsSchema(connection, null);
        if (head.SchemaVersion != version)
        {
            throw new InvalidDataException(
                $"State head schema {head.SchemaVersion} does not match user_version {version}.");
        }
        return head;
    }

    private static void VerifyTemplateSettingsSchema(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT center_theme, template_catalog_json FROM app_settings WHERE singleton_id = 1;";
        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.GetString(0) is not ("light" or "ink" or "blue" or "system"))
            throw new InvalidDataException("Invalid control-center theme.");
        try { DesktopUp.Domain.TemplateRecipeCatalog.Read(reader.GetString(1), stored: true); }
        catch (ArgumentException e) { throw new InvalidDataException("Invalid stored template catalog.", e); }
    }

    private static void VerifyContentLayoutSchema(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using var shape = connection.CreateCommand(); shape.Transaction = transaction;
        shape.CommandText = "SELECT type, [notnull] FROM pragma_table_info('posters') WHERE name = 'content_layout_json';";
        using (var reader = shape.ExecuteReader())
            if (!reader.Read() || reader.GetString(0) != "TEXT" || reader.GetInt64(1) != 0)
                throw new InvalidDataException("Invalid content-layout column.");
        // Unknown versions remain exact raw text. Reading/backup must not require the current parser.
        shape.CommandText = "SELECT COUNT(*) FROM posters WHERE content_layout_json IS NOT NULL AND typeof(content_layout_json) != 'text';";
        if (Convert.ToInt64(shape.ExecuteScalar(), CultureInfo.InvariantCulture) != 0)
            throw new InvalidDataException("Invalid content-layout storage type.");
    }

    private static void VerifyPendingPlacementSchema(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using var shape = connection.CreateCommand(); shape.Transaction = transaction;
        shape.CommandText = "SELECT type, [notnull], dflt_value FROM pragma_table_info('posters') WHERE name = 'placement_pending';";
        using (var reader = shape.ExecuteReader())
            if (!reader.Read() || reader.GetString(0) != "INTEGER" || reader.GetInt64(1) != 1 || reader.GetString(2) != "0")
                throw new InvalidDataException("Invalid pending-placement column.");
        shape.CommandText = "SELECT COUNT(*) FROM posters WHERE typeof(placement_pending) != 'integer' OR placement_pending NOT IN (0,1);";
        if (Convert.ToInt64(shape.ExecuteScalar(), CultureInfo.InvariantCulture) != 0)
            throw new InvalidDataException("Invalid persisted pending-placement state.");
    }

    private static void VerifyAutoPostureSchema(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using var shape = connection.CreateCommand(); shape.Transaction = transaction;
        shape.CommandText = "SELECT type, [notnull], dflt_value FROM pragma_table_info('posters') WHERE name = 'auto_collapse_delay_seconds';";
        using (var reader = shape.ExecuteReader())
            if (!reader.Read() || reader.GetString(0) != "INTEGER" || reader.GetInt64(1) != 1 || reader.GetString(2) != "3")
                throw new InvalidDataException("Invalid auto-posture delay column.");
        shape.CommandText = """
            SELECT COUNT(*) FROM posters WHERE typeof(auto_collapse_delay_seconds) != 'integer'
                OR auto_collapse_delay_seconds NOT BETWEEN 1 AND 300;
            """;
        if (Convert.ToInt64(shape.ExecuteScalar(), CultureInfo.InvariantCulture) != 0)
            throw new InvalidDataException("Invalid persisted auto-posture delay.");
    }

    private static void VerifySpatialSchema(SqliteConnection connection, SqliteTransaction? transaction)
    {
        foreach (string sql in new[]
        {
            "SELECT layout_generation, last_layout_operation_id FROM posters LIMIT 0;",
            "SELECT operation_id, source_poster_id, schema_version, status, body_json, committed_revision FROM spatial_operations LIMIT 0;",
            "SELECT operation_id, poster_id, expected_layout_generation, proof_json, invalidated_reason FROM spatial_participants LIMIT 0;"
        })
        {
            using var shape = connection.CreateCommand(); shape.Transaction = transaction; shape.CommandText = sql;
            using (shape.ExecuteReader()) { }
        }
    }

    private static void VerifyContinuitySchema(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT workspace_id, schema_version, body_json, committed_revision FROM spatial_continuity LIMIT 0;";
        using (command.ExecuteReader()) { }
    }

    private static void VerifyCommandSchema(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT request_id, request_sha256, outcome, observed_revision, committed_revision, result_poster_id FROM state_command_receipts LIMIT 0;";
        using (command.ExecuteReader()) { }
        command.CommandText = "SELECT entity_version FROM app_settings WHERE singleton_id = 1;";
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read() || reader.GetInt64(0) < 1 || reader.Read())
            throw new InvalidDataException("Invalid settings entity version.");
    }

    private static void VerifyCatalogSchema(SqliteConnection connection, SqliteTransaction? transaction)
    {
        foreach (string sql in new[]
        {
            "SELECT purpose, is_visible, stack_order, collection_id, theme_instance_id, entity_version FROM posters LIMIT 0;",
            "SELECT operation_id, request_sha256, result_poster_id FROM poster_catalog_operations LIMIT 0;"
        })
        {
            using SqliteCommand shape = connection.CreateCommand();
            shape.Transaction = transaction;
            shape.CommandText = sql;
            using SqliteDataReader reader = shape.ExecuteReader();
        }
        using (SqliteCommand profile = connection.CreateCommand())
        {
            profile.Transaction = transaction;
            profile.CommandText = """
                SELECT profile_initialized, catalog_version, state_revision
                FROM poster_profile WHERE singleton_id = 1;
                """;
            using SqliteDataReader reader = profile.ExecuteReader();
            if (!reader.Read() || reader.GetInt64(0) is < 0 or > 1 ||
                reader.GetInt64(1) < 0 || reader.GetInt64(2) < 0)
                throw new InvalidDataException("The poster profile contract is invalid.");
        }
        using SqliteCommand foreignKeys = connection.CreateCommand();
        foreignKeys.Transaction = transaction;
        foreignKeys.CommandText = "PRAGMA foreign_key_check;";
        using SqliteDataReader violations = foreignKeys.ExecuteReader();
        if (violations.Read())
            throw new InvalidDataException("State contains invalid foreign key references.");
    }

    private static StateStoreHead ReadHead(
        SqliteConnection connection,
        SqliteTransaction? transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT schema_version, revision, updated_utc
            FROM state_head
            WHERE singleton_id = 1;
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidDataException("The singleton state head is missing.");
        }
        int schemaVersion = reader.GetInt32(0);
        long revision = reader.GetInt64(1);
        DateTimeOffset updatedAt = DateTimeOffset.Parse(
            reader.GetString(2),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
        return new StateStoreHead(schemaVersion, revision, updatedAt);
    }

    private static (long Expected, long Committed)? ReadOperation(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string operationId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT expected_revision, committed_revision
            FROM state_operations
            WHERE operation_id = $operationId;
            """;
        command.Parameters.AddWithValue("$operationId", operationId);
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read()
            ? (reader.GetInt64(0), reader.GetInt64(1))
            : null;
    }

    private StateBackupResult CreateBackupFromOpenConnection(
        SqliteConnection source,
        string reason,
        int expectedSchemaVersion = CurrentSchemaVersion)
    {
        _paths.EnsureCreated();
        StateStoreHead? head = TryReadHead(source);
        string safeReason = SanitizeReason(reason);
        string stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        string revision = head is null ? "legacy" : $"r{head.Revision}";
        string fileName = $"desktopup-{stamp}-{revision}-{safeReason}.sqlite3";
        string destinationPath = Path.Combine(_paths.Backups, fileName);
        string temporaryPath = destinationPath + ".tmp";

        try
        {
            using (var destination = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = temporaryPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Pooling = false
                }.ToString()))
            {
                destination.Open();
                source.BackupDatabase(destination);
                // The backup API copies the source's WAL-mode header too. A later read-only
                // validation can then create sidecars beside the published snapshot, causing
                // strict restore admission to reject it. Normalize ONLY our unpublished copy;
                // never delete sidecars from an existing/user-supplied backup or touch source mode.
                using var seal = destination.CreateCommand();
                seal.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                seal.ExecuteNonQuery();
                seal.CommandText = "PRAGMA journal_mode=DELETE;";
                if (!string.Equals(Convert.ToString(seal.ExecuteScalar(), CultureInfo.InvariantCulture), "delete", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Backup copy could not be sealed as a standalone snapshot.");
            }

            if (File.Exists(temporaryPath + "-wal") || File.Exists(temporaryPath + "-shm"))
                throw new InvalidDataException("Backup copy still has SQLite sidecars.");
            StateBackupValidationResult validation = ValidateBackupCore(
                temporaryPath,
                allowLegacy: head is null,
                expectedSchemaVersion);
            if (!validation.IsValid)
            {
                throw new InvalidDataException(
                    $"Backup validation failed: {validation.Error}");
            }

            File.Move(temporaryPath, destinationPath, overwrite: false);
            _log?.Write("info", "state_store.backup_created", new
            {
                fileName,
                revision = head?.Revision,
                reason = safeReason
            });
            return new StateBackupResult(true, fileName, head?.Revision, null);
        }
        catch (Exception exception)
        {
            return new StateBackupResult(false, null, head?.Revision, exception.Message);
        }
        finally
        {
            DeleteTemporaryBackupArtifacts(temporaryPath);
        }
    }

    private static void DeleteTemporaryBackupArtifacts(string temporaryPath)
    {
        foreach (string candidate in new[]
        {
            temporaryPath,
            temporaryPath + "-wal",
            temporaryPath + "-shm"
        })
        {
            try
            {
                if (File.Exists(candidate))
                {
                    File.Delete(candidate);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static StateBackupValidationResult ValidateBackupCore(
        string backupPath,
        bool allowLegacy = false,
        int expectedSchemaVersion = CurrentSchemaVersion)
    {
        try
        {
            if (!File.Exists(backupPath))
            {
                return new StateBackupValidationResult(false, 0, null, "Backup file is missing.");
            }
            using SqliteConnection connection = OpenReadOnlyConnection(backupPath);
            AssertIntegrity(connection);
            int version = ReadPragmaInt32(connection, "user_version");
            int applicationId = ReadPragmaInt32(connection, "application_id");
            if (allowLegacy && version == 0 && applicationId == 0)
            {
                return new StateBackupValidationResult(true, 0, null, null);
            }
            if (version != expectedSchemaVersion || applicationId != ApplicationId)
            {
                return new StateBackupValidationResult(
                    false,
                    version,
                    null,
                    $"State identity mismatch (schema={version}, application_id={applicationId}).");
            }
            StateStoreHead head = VerifySchemaVersion(connection, expectedSchemaVersion);
            return new StateBackupValidationResult(true, version, head.Revision, null);
        }
        catch (Exception exception)
        {
            return new StateBackupValidationResult(false, 0, null, exception.Message);
        }
    }

    private static StateStoreHead? TryReadHead(SqliteConnection connection)
    {
        try
        {
            return ReadHead(connection, null);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 1)
        {
            return null;
        }
    }

    private static void AssertIntegrity(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        string? result = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"SQLite quick_check failed: {result ?? "no result"}.");
        }
    }

    private static int ReadPragmaInt32(SqliteConnection connection, string name)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA {name};";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static string SanitizeReason(string reason)
    {
        string source = string.IsNullOrWhiteSpace(reason) ? "manual" : reason.Trim();
        var result = new StringBuilder(source.Length);
        foreach (char character in source)
        {
            result.Append(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'
                ? char.ToLowerInvariant(character)
                : '-');
        }
        string value = result.ToString().Trim('-');
        return string.IsNullOrEmpty(value) ? "manual" : value[..Math.Min(value.Length, 48)];
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed record Migration(int Version, string Name, string Sql)
    {
        public string Checksum { get; } = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(Sql)));
    }
}
