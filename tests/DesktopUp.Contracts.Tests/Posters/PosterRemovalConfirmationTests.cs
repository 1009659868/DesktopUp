using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterRemovalConfirmationTests
{
    [Fact] public void CancelAndDoubleCompletionNeverWrite()
    {
        using var f = new Fixture(); var gate = new PosterRemovalConfirmation(); var ticket = f.Begin(gate);
        long revision = f.Current.Revision;
        Assert.Null(gate.Complete(ticket.Id, false)); Assert.Null(gate.Complete(ticket.Id, true));
        Assert.Equal(revision, f.Current.Revision); Assert.Single(f.Current.Posters);
    }
    [Fact] public void SelectionChangeCannotRetargetConfirmedRemoval()
    {
        using var f = new Fixture(); string b = f.Add(); var gate = new PosterRemovalConfirmation(); var ticket = f.Begin(gate, b);
        var selection = new ControlCenterSelection(); selection.ApplyProjection(f.Current.ControlCenter); selection.Select(Fixture.Main);
        var command = gate.Complete(ticket.Id, true)!;
        Assert.Equal(b, command.PosterId); Assert.Equal(StateCommandStatus.Committed, f.Core.Execute(command).Status);
        Assert.Single(f.Current.Posters); Assert.True(f.Current.PostersById.ContainsKey(Fixture.Main));
        Assert.Null(gate.Complete(ticket.Id, true));
        Assert.Equal(StateCommandStatus.Replayed, f.Core.Execute(command).Status);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void TargetOrCatalogChangeRejectsEvenIfCallerMissesUiInvalidation(bool catalog)
    {
        using var f = new Fixture(); var gate = new PosterRemovalConfirmation(); var ticket = f.Begin(gate);
        if (catalog) f.Add(); else f.Lock();
        Assert.False(gate.IsCurrent(f.Current.ControlCenter));
        Assert.Equal(StateCommandStatus.Conflict, f.Core.Execute(gate.Complete(ticket.Id, true)!).Status);
        Assert.True(f.Current.PostersById.ContainsKey(Fixture.Main));
    }
    [Fact] public void CloseExitOrEnvironmentCancellationMakesOldDialogCompletionHarmless()
    {
        using var f = new Fixture(); var gate = new PosterRemovalConfirmation(); var old = f.Begin(gate);
        gate.Cancel(); var current = f.Begin(gate);
        Assert.Null(gate.Complete(old.Id, true)); Assert.Equal(current, gate.Pending);
        gate.Cancel(); Assert.Null(gate.Complete(current.Id, true)); Assert.Single(f.Current.Posters);
    }
    [Fact] public void LastPosterRemovalPreservesZeroCatalogAcrossReinitialize()
    {
        using var f = new Fixture(); var gate = new PosterRemovalConfirmation(); var ticket = f.Begin(gate);
        Assert.Contains("最后一张", ticket.Message); Assert.Contains("返回收纳前的目录", ticket.Message); Assert.Contains("系统桌面", ticket.Message);
        Assert.Equal(StateCommandStatus.Committed, f.Core.Execute(gate.Complete(ticket.Id, true)!).Status);
        Assert.Empty(f.Core.EnsureInitialized(Fixture.Configuration()).Posters);
        Assert.Equal(0, f.Current.Tray.PosterCount);
    }
    [Fact] public void MismatchedOrStaleRequestCannotReplaceExistingTicket()
    {
        using var f = new Fixture(); var gate = new PosterRemovalConfirmation(); var p = f.Core.PrepareRemoval(Fixture.Main)!;
        var request = new PosterRemovalRequest(Fixture.Main, p.ReadSet.Posters[Fixture.Main], StateCommandSource.ControlCenter);
        Assert.Null(gate.Begin(request with { PosterId = "missing" }, p));
        Assert.Null(gate.Begin(request with { EntityVersion = 44 }, p));
        Assert.Null(gate.Begin(request with { Source = StateCommandSource.Tray }, p));
        var ticket = gate.Begin(request, p); Assert.NotNull(ticket);
        Assert.Null(gate.Begin(request, p)); Assert.Equal(ticket, gate.Pending);
    }
    [Fact] public void UnrelatedSettingsChangeDoesNotInvalidateConfirmation()
    {
        using var f = new Fixture(); var gate = new PosterRemovalConfirmation(); var ticket = f.Begin(gate);
        var p = f.Current;
        f.Core.Execute(new SettingsStateCommand(Guid.NewGuid().ToString(), StateCommandSource.Tray,
            SettingsStateCommandKind.SetDynamicEffectsPaused, p.Revision,
            p.ControlCenter.Settings.Settings with { DynamicEffectsPaused = true })
            { ReadSet = StateReadSet.ForSettings(p.ControlCenter.Settings.EntityVersion) });
        Assert.True(gate.IsCurrent(f.Current.ControlCenter));
        Assert.Equal(StateCommandStatus.Committed, f.Core.Execute(gate.Complete(ticket.Id, true)!).Status);
    }
    private sealed class Fixture : IDisposable
    {
        public const string Main = BuiltInStateIdentities.PrimaryPosterId;
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp-removal-" + Guid.NewGuid().ToString("N"));
        private readonly SqliteStateStore _store;
        public StateProjectionCoordinator Core { get; }
        public DesktopUpProjection Current { get { Core.TryGetCurrent(out var p); return p!; } }
        public static PosterDefinition Configuration() => new(Main, "NIGHT OPS", "TACTICAL ARCHIVE / 01", "DISPLAY1",
            24, 24, 420, 720, .02, .02, .22, .7, false, false, false, "grid", "{}", "night-ops");
        public Fixture()
        {
            _store = new(new UserDataPaths(_root)); Assert.True(_store.Initialize().Succeeded);
            Core = new(new SqliteStateCommandRepository(_store)); Core.EnsureInitialized(Configuration());
        }
        public PosterRemovalTicket Begin(PosterRemovalConfirmation gate, string id = Main)
        {
            var preview = Core.PrepareRemoval(id)!;
            return gate.Begin(new(id, preview.ReadSet.Posters[id], StateCommandSource.ControlCenter), preview)!;
        }
        public string Add() => Core.Execute(new CreatePosterCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter,
            Current.Revision, StateReadSet.ForCatalog(Current.CatalogVersion), Configuration() with { Title = "B" })).ResultPosterId!;
        public void Lock() => Core.Execute(new PosterStateCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter,
            PosterStateCommandKind.SetLocked, Current.Revision, Main, Current.PostersById[Main].Poster with { IsLocked = true })
            { ReadSet = StateReadSet.ForPoster(Main, Current.PostersById[Main].EntityVersion) });
        public void Dispose()
        {
            _store.Dispose(); SqliteConnection.ClearAllPools();
            Directory.Delete(_root, true);
        }
    }
}
