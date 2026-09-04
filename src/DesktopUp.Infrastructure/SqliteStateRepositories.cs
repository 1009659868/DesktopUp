using System.Globalization;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Infrastructure;

public sealed class SqlitePosterRepository(
    SqliteStateStore store,
    IStructuredLog? log = null) : IPosterRepository
{
    public PosterStateSnapshot? Read(string posterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(posterId);
        return store.ExecuteRead((connection, transaction) =>
            ReadPoster(connection, transaction, posterId));
    }

    public PosterStateSnapshot GetOrCreateDefault(PosterDefinition defaultPoster)
    {
        string? validation = AuthoritativeStateValidationPolicy.Validate(defaultPoster);
        if (validation is not null)
        {
            throw new ArgumentException(validation, nameof(defaultPoster));
        }

        for (int attempt = 1; attempt <= 4; attempt++)
        {
            PosterStateSnapshot? existing = Read(defaultPoster.PosterId);
            if (existing is not null)
            {
                return existing;
            }

            long expectedRevision = store.ReadHead().Revision;
            string operationId = $"create-default-poster-{Guid.NewGuid():N}";
            StateCompareAndSwapResult result = store.ExecuteCompareAndSwap(
                expectedRevision,
                operationId,
                (connection, transaction, committedRevision) =>
                    InsertPoster(
                        connection,
                        transaction,
                        defaultPoster,
                        committedRevision));
            if (result.Succeeded)
            {
                PosterStateSnapshot? created = Read(defaultPoster.PosterId);
                if (created is not null)
                {
                    log?.Write("info", "poster_repository.default_created", new
                    {
                        defaultPoster.PosterId,
                        created.Revision,
                        created.PosterRevision,
                        result.Status
                    });
                    return created;
                }
            }
            if (result.Status != StateCompareAndSwapStatus.Conflict)
            {
                break;
            }
        }

        throw new InvalidOperationException("The default poster could not be created.");
    }

    public RepositoryWriteResult<PosterStateSnapshot> Save(
        long expectedRevision,
        string operationId,
        PosterDefinition poster)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedRevision);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        string? validation = AuthoritativeStateValidationPolicy.Validate(poster);
        if (validation is not null)
        {
            PosterStateSnapshot? current = TryRead(poster.PosterId);
            return new RepositoryWriteResult<PosterStateSnapshot>(
                RepositoryWriteStatus.ValidationFailed,
                operationId,
                expectedRevision,
                current?.Revision ?? SafeReadRevision(),
                null,
                current,
                validation);
        }

        try
        {
            StateCompareAndSwapResult result = store.ExecuteCompareAndSwap(
                expectedRevision,
                operationId,
                (connection, transaction, committedRevision) =>
                    UpdatePoster(
                        connection,
                        transaction,
                        poster,
                        committedRevision));
            PosterStateSnapshot? snapshot = Read(poster.PosterId);
            RepositoryWriteStatus status = Map(result.Status);
            string? error = snapshot is null ? "The poster row is missing." : null;
            if (snapshot is null && status is not RepositoryWriteStatus.Conflict)
            {
                status = RepositoryWriteStatus.Failed;
            }

            log?.Write(
                status is RepositoryWriteStatus.Committed or RepositoryWriteStatus.Replayed
                    ? "info"
                    : "warning",
                "poster_repository.write_completed",
                new
                {
                    poster.PosterId,
                    operationId,
                    expectedRevision,
                    result.CurrentRevision,
                    result.CommittedRevision,
                    status = status.ToString(),
                    error
                });
            return new RepositoryWriteResult<PosterStateSnapshot>(
                status,
                operationId,
                expectedRevision,
                result.CurrentRevision,
                result.CommittedRevision,
                snapshot,
                error);
        }
        catch (Exception exception)
        {
            PosterStateSnapshot? current = TryRead(poster.PosterId);
            log?.Write("error", "poster_repository.write_failed", new
            {
                poster.PosterId,
                operationId,
                expectedRevision,
                exceptionType = exception.GetType().FullName,
                exception.HResult,
                exception.Message
            });
            return new RepositoryWriteResult<PosterStateSnapshot>(
                RepositoryWriteStatus.Failed,
                operationId,
                expectedRevision,
                current?.Revision ?? SafeReadRevision(),
                null,
                current,
                exception.Message);
        }
    }

    private PosterStateSnapshot? TryRead(string posterId)
    {
        try
        {
            return Read(posterId);
        }
        catch
        {
            return null;
        }
    }

    private long SafeReadRevision()
    {
        try
        {
            return store.ReadHead().Revision;
        }
        catch
        {
            return -1;
        }
    }

    internal static PosterStateSnapshot? ReadPoster(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string posterId)
    {
        long revision = ReadGlobalRevision(connection, transaction);
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                poster_id,
                title,
                subtitle,
                monitor_device_name,
                fallback_x_dip,
                fallback_y_dip,
                width_dip,
                expanded_height_dip,
                horizontal_anchor,
                vertical_anchor,
                normalized_width,
                normalized_expanded_height,
                is_locked,
                is_collapsed,
                auto_collapse_enabled,
                layout_kind,
                layout_parameters_json,
                template_key,
                state_revision,
                updated_utc
            FROM posters
            WHERE poster_id = $posterId;
            """;
        command.Parameters.AddWithValue("$posterId", posterId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var poster = new PosterDefinition(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetDouble(4),
            reader.GetDouble(5),
            reader.GetDouble(6),
            reader.GetDouble(7),
            reader.GetDouble(8),
            reader.GetDouble(9),
            reader.GetDouble(10),
            reader.GetDouble(11),
            reader.GetInt64(12) != 0,
            reader.GetInt64(13) != 0,
            reader.GetInt64(14) != 0,
            reader.GetString(15),
            reader.GetString(16),
            reader.GetString(17));
        return new PosterStateSnapshot(
            revision,
            reader.GetInt64(18),
            poster,
            ParseTimestamp(reader.GetString(19)));
    }

    private static void InsertPoster(
        SqliteConnection connection,
        SqliteTransaction transaction,
        PosterDefinition poster,
        long committedRevision)
    {
        string timestamp = TimestampNow();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO posters (
                poster_id, title, subtitle, monitor_device_name,
                fallback_x_dip, fallback_y_dip,
                width_dip, expanded_height_dip,
                horizontal_anchor, vertical_anchor,
                normalized_width, normalized_expanded_height,
                is_locked, is_collapsed, auto_collapse_enabled,
                layout_kind, layout_parameters_json, template_key,
                state_revision, created_utc, updated_utc)
            VALUES (
                $posterId, $title, $subtitle, $monitorDeviceName,
                $fallbackXDip, $fallbackYDip,
                $widthDip, $expandedHeightDip,
                $horizontalAnchor, $verticalAnchor,
                $normalizedWidth, $normalizedExpandedHeight,
                $isLocked, $isCollapsed, $autoCollapseEnabled,
                $layoutKind, $layoutParametersJson, $templateKey,
                $stateRevision, $createdUtc, $updatedUtc);
            """;
        AddPosterParameters(command, poster, committedRevision, timestamp);
        command.Parameters.AddWithValue("$createdUtc", timestamp);
        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException("The default poster insert did not affect one row.");
        }
    }

    private static void UpdatePoster(
        SqliteConnection connection,
        SqliteTransaction transaction,
        PosterDefinition poster,
        long committedRevision)
    {
        string timestamp = TimestampNow();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE posters
            SET title = $title,
                subtitle = $subtitle,
                monitor_device_name = $monitorDeviceName,
                fallback_x_dip = $fallbackXDip,
                fallback_y_dip = $fallbackYDip,
                width_dip = $widthDip,
                expanded_height_dip = $expandedHeightDip,
                horizontal_anchor = $horizontalAnchor,
                vertical_anchor = $verticalAnchor,
                normalized_width = $normalizedWidth,
                normalized_expanded_height = $normalizedExpandedHeight,
                is_locked = $isLocked,
                is_collapsed = $isCollapsed,
                auto_collapse_enabled = $autoCollapseEnabled,
                layout_kind = $layoutKind,
                layout_parameters_json = $layoutParametersJson,
                template_key = $templateKey,
                state_revision = $stateRevision,
                updated_utc = $updatedUtc
            WHERE poster_id = $posterId;
            """;
        AddPosterParameters(command, poster, committedRevision, timestamp);
        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException("The poster update did not affect one row.");
        }
    }

    private static void AddPosterParameters(
        SqliteCommand command,
        PosterDefinition poster,
        long revision,
        string updatedUtc)
    {
        command.Parameters.AddWithValue("$posterId", poster.PosterId);
        command.Parameters.AddWithValue("$title", poster.Title);
        command.Parameters.AddWithValue("$subtitle", poster.Subtitle);
        command.Parameters.AddWithValue("$monitorDeviceName", poster.MonitorDeviceName);
        command.Parameters.AddWithValue("$fallbackXDip", poster.FallbackXDip);
        command.Parameters.AddWithValue("$fallbackYDip", poster.FallbackYDip);
        command.Parameters.AddWithValue("$widthDip", poster.WidthDip);
        command.Parameters.AddWithValue("$expandedHeightDip", poster.ExpandedHeightDip);
        command.Parameters.AddWithValue("$horizontalAnchor", poster.HorizontalAnchor);
        command.Parameters.AddWithValue("$verticalAnchor", poster.VerticalAnchor);
        command.Parameters.AddWithValue("$normalizedWidth", poster.NormalizedWidth);
        command.Parameters.AddWithValue(
            "$normalizedExpandedHeight",
            poster.NormalizedExpandedHeight);
        command.Parameters.AddWithValue("$isLocked", poster.IsLocked ? 1 : 0);
        command.Parameters.AddWithValue("$isCollapsed", poster.IsCollapsed ? 1 : 0);
        command.Parameters.AddWithValue(
            "$autoCollapseEnabled",
            poster.AutoCollapseEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$layoutKind", poster.LayoutKind);
        command.Parameters.AddWithValue("$layoutParametersJson", poster.LayoutParametersJson);
        command.Parameters.AddWithValue("$templateKey", poster.TemplateKey);
        command.Parameters.AddWithValue("$stateRevision", revision);
        command.Parameters.AddWithValue("$updatedUtc", updatedUtc);
    }

    private static RepositoryWriteStatus Map(StateCompareAndSwapStatus status) => status switch
    {
        StateCompareAndSwapStatus.Committed => RepositoryWriteStatus.Committed,
        StateCompareAndSwapStatus.Conflict => RepositoryWriteStatus.Conflict,
        StateCompareAndSwapStatus.Replayed => RepositoryWriteStatus.Replayed,
        _ => RepositoryWriteStatus.Failed
    };

    internal static long ReadGlobalRevision(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT revision FROM state_head WHERE singleton_id = 1;";
        object? value = command.ExecuteScalar();
        return value is null || value is DBNull
            ? throw new InvalidDataException("The global state head is missing.")
            : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    internal static string TimestampNow() =>
        DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

    internal static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
}

public sealed class SqliteSettingsRepository(
    SqliteStateStore store,
    IStructuredLog? log = null) : ISettingsRepository
{
    public SettingsStateSnapshot Read() =>
        store.ExecuteRead(ReadSettings);

    public RepositoryWriteResult<SettingsStateSnapshot> Save(
        long expectedRevision,
        string operationId,
        DesktopUpSettings settings)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedRevision);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        string? validation = AuthoritativeStateValidationPolicy.Validate(settings);
        if (validation is not null)
        {
            SettingsStateSnapshot current = Read();
            return new RepositoryWriteResult<SettingsStateSnapshot>(
                RepositoryWriteStatus.ValidationFailed,
                operationId,
                expectedRevision,
                current.Revision,
                null,
                current,
                validation);
        }

        try
        {
            StateCompareAndSwapResult result = store.ExecuteCompareAndSwap(
                expectedRevision,
                operationId,
                (connection, transaction, committedRevision) =>
                {
                    using SqliteCommand command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText =
                        """
                        UPDATE app_settings
                        SET dynamic_effects_paused = $dynamicEffectsPaused,
                            performance_mode = $performanceMode,
                            launch_at_startup_requested = $launchAtStartupRequested,
                            state_revision = $stateRevision,
                            updated_utc = $updatedUtc
                        WHERE singleton_id = 1;
                        """;
                    command.Parameters.AddWithValue(
                        "$dynamicEffectsPaused",
                        settings.DynamicEffectsPaused ? 1 : 0);
                    command.Parameters.AddWithValue(
                        "$performanceMode",
                        ToStorage(settings.PerformanceMode));
                    command.Parameters.AddWithValue(
                        "$launchAtStartupRequested",
                        settings.LaunchAtStartupRequested ? 1 : 0);
                    command.Parameters.AddWithValue("$stateRevision", committedRevision);
                    command.Parameters.AddWithValue(
                        "$updatedUtc",
                        SqlitePosterRepository.TimestampNow());
                    if (command.ExecuteNonQuery() != 1)
                    {
                        throw new InvalidOperationException(
                            "The settings update did not affect one row.");
                    }
                });
            SettingsStateSnapshot snapshot = Read();
            RepositoryWriteStatus status = result.Status switch
            {
                StateCompareAndSwapStatus.Committed => RepositoryWriteStatus.Committed,
                StateCompareAndSwapStatus.Conflict => RepositoryWriteStatus.Conflict,
                StateCompareAndSwapStatus.Replayed => RepositoryWriteStatus.Replayed,
                _ => RepositoryWriteStatus.Failed
            };
            log?.Write(
                status is RepositoryWriteStatus.Committed or RepositoryWriteStatus.Replayed
                    ? "info"
                    : "warning",
                "settings_repository.write_completed",
                new
                {
                    operationId,
                    expectedRevision,
                    result.CurrentRevision,
                    result.CommittedRevision,
                    status = status.ToString()
                });
            return new RepositoryWriteResult<SettingsStateSnapshot>(
                status,
                operationId,
                expectedRevision,
                result.CurrentRevision,
                result.CommittedRevision,
                snapshot,
                null);
        }
        catch (Exception exception)
        {
            SettingsStateSnapshot? current = null;
            try
            {
                current = Read();
            }
            catch
            {
            }
            log?.Write("error", "settings_repository.write_failed", new
            {
                operationId,
                expectedRevision,
                exceptionType = exception.GetType().FullName,
                exception.HResult,
                exception.Message
            });
            return new RepositoryWriteResult<SettingsStateSnapshot>(
                RepositoryWriteStatus.Failed,
                operationId,
                expectedRevision,
                current?.Revision ?? -1,
                null,
                current,
                exception.Message);
        }
    }

    internal static SettingsStateSnapshot ReadSettings(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        long revision = SqlitePosterRepository.ReadGlobalRevision(connection, transaction);
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                dynamic_effects_paused,
                performance_mode,
                launch_at_startup_requested,
                state_revision,
                updated_utc
            FROM app_settings
            WHERE singleton_id = 1;
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidDataException("The singleton settings row is missing.");
        }
        var settings = new DesktopUpSettings(
            reader.GetInt64(0) != 0,
            FromStorage(reader.GetString(1)),
            reader.GetInt64(2) != 0);
        return new SettingsStateSnapshot(
            revision,
            reader.GetInt64(3),
            settings,
            SqlitePosterRepository.ParseTimestamp(reader.GetString(4)));
    }

    private static string ToStorage(DesktopUpPerformanceMode mode) => mode switch
    {
        DesktopUpPerformanceMode.Automatic => "automatic",
        DesktopUpPerformanceMode.Static => "static",
        DesktopUpPerformanceMode.FramesPerSecond30 => "30fps",
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    private static DesktopUpPerformanceMode FromStorage(string value) => value switch
    {
        "automatic" => DesktopUpPerformanceMode.Automatic,
        "static" => DesktopUpPerformanceMode.Static,
        "30fps" => DesktopUpPerformanceMode.FramesPerSecond30,
        _ => throw new InvalidDataException($"Unknown performance mode '{value}'.")
    };
}

public sealed class SqliteStateProjectionRepository(
    SqliteStateStore store) : IStateProjectionRepository
{
    public AuthoritativeStateSnapshot Read(string posterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(posterId);
        return store.ExecuteRead((connection, transaction) =>
        {
            PosterStateSnapshot poster =
                SqlitePosterRepository.ReadPoster(connection, transaction, posterId) ??
                throw new InvalidDataException($"Poster '{posterId}' is missing.");
            SettingsStateSnapshot settings =
                SqliteSettingsRepository.ReadSettings(connection, transaction);
            if (poster.Revision != settings.Revision)
            {
                throw new InvalidDataException(
                    "Poster and settings were not read from one committed revision.");
            }
            return new AuthoritativeStateSnapshot(
                poster.Revision,
                poster.PosterRevision,
                settings.SettingsRevision,
                poster.Poster,
                settings.Settings,
                poster.UpdatedAtUtc,
                settings.UpdatedAtUtc);
        });
    }
}
