using DesktopUp.Domain;

namespace DesktopUp.Application;

public static class BuiltInStateIdentities
{
    public const string PrimaryPosterId = "poster-main";
}

public sealed record PosterStateSnapshot(
    long Revision,
    long PosterRevision,
    PosterDefinition Poster,
    DateTimeOffset UpdatedAtUtc,
    long EntityVersion = 1)
{
    public long LayoutGeneration { get; init; }
    public string? LastLayoutOperationId { get; init; }
}

public sealed record SettingsStateSnapshot(
    long Revision,
    long SettingsRevision,
    DesktopUpSettings Settings,
    DateTimeOffset UpdatedAtUtc,
    long EntityVersion = 1);

public sealed record AuthoritativeStateSnapshot(
    long Revision,
    long PosterRevision,
    long SettingsRevision,
    PosterDefinition Poster,
    DesktopUpSettings Settings,
    DateTimeOffset PosterUpdatedAtUtc,
    DateTimeOffset SettingsUpdatedAtUtc);

public enum RepositoryWriteStatus
{
    Committed,
    Conflict,
    Replayed,
    ValidationFailed,
    Failed
}

public sealed record RepositoryWriteResult<TSnapshot>(
    RepositoryWriteStatus Status,
    string OperationId,
    long ExpectedRevision,
    long CurrentRevision,
    long? CommittedRevision,
    TSnapshot? Snapshot,
    string? Error)
    where TSnapshot : class
{
    public bool Succeeded => Status is
        RepositoryWriteStatus.Committed or
        RepositoryWriteStatus.Replayed;
}

public interface IPosterRepository
{
    PosterStateSnapshot? Read(string posterId);
    PosterStateSnapshot GetOrCreateDefault(PosterDefinition defaultPoster);
    RepositoryWriteResult<PosterStateSnapshot> Save(
        long expectedRevision,
        string operationId,
        PosterDefinition poster);
}

public interface ISettingsRepository
{
    SettingsStateSnapshot Read();
    RepositoryWriteResult<SettingsStateSnapshot> Save(
        long expectedRevision,
        string operationId,
        DesktopUpSettings settings);
}

public interface IStateProjectionRepository
{
    AuthoritativeStateSnapshot Read(string posterId);
}
