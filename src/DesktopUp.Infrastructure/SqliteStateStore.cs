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
    private const int CurrentSchemaVersion = 2;
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
        if (head.SchemaVersion != version)
        {
            throw new InvalidDataException(
                $"State head schema {head.SchemaVersion} does not match user_version {version}.");
        }
        return head;
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
            }

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
