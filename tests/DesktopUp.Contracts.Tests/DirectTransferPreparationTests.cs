using DesktopUp.Windows;
using DesktopUp.Application;
using DesktopUp.Infrastructure;
using DesktopUp.Domain;
using Fixture = DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class DirectTransferPreparationTests
{
    [Theory]
    [InlineData("plain.txt")]
    [InlineData("shortcut.lnk")]
    public void IncomingMovesObjectItselfAndPreservesIdentity(string name)
    {
        using var f=new Fixture();string source=f.File(name);
        var preview=DirectTransferPreparation.Incoming(f.State,f.Main,[source],new(f.Root,f.Collection));
        f.Journal.Begin(preview.Plan,preview.Files,preview.Plan.Fingerprint,f.Collection.Id);
        Assert.Equal(TransferFinishKind.Completed,Assert.Single(new AutomaticTransferCompletion(f.Journal).Execute(preview.Plan.Request.OperationId)).Kind);
        Assert.False(File.Exists(source));Assert.Equal("owned-content",File.ReadAllText(preview.Files[0].Destination));
        Assert.Equal(preview.Plan.Entries[0].Entry.Item.Id,Assert.Single(f.State.ContentCatalog.Items).Id);
    }
    [Fact] public void DirectoryMovesAsOneObject()
    {
        using var f=new Fixture();string folder=Path.Combine(f.Source,"folder");Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder,"child.txt"),"fixture");
        var p=DirectTransferPreparation.Incoming(f.State,f.Main,[folder],new(f.Root,f.Collection));
        f.Journal.Begin(p.Plan,p.Files,p.Plan.Fingerprint,f.Collection.Id);
        Assert.Equal(TransferFinishKind.Completed,Assert.Single(new AutomaticTransferCompletion(f.Journal).Execute(p.Plan.Request.OperationId)).Kind);
        Assert.False(Directory.Exists(folder));Assert.Equal("fixture",File.ReadAllText(Path.Combine(p.Files[0].Destination,"child.txt")));
    }
    [Fact] public void ExistingReferenceIsReusedWithoutLosingCard()
    {
        using var f=new Fixture();string source=f.File("shared.txt");var item=LocalContentReferenceProbe.Inspect(source).Item!;
        f.AddReference(item);var original=Assert.Single(f.State.ContentCatalog.PosterItems);
        var p=DirectTransferPreparation.Incoming(f.State,f.Main,[source],new(f.Root,f.Collection));
        Assert.Equal(item.Id,p.Plan.Entries[0].Entry.Item.Id);
        f.Journal.Begin(p.Plan,p.Files,p.Plan.Fingerprint,f.Collection.Id);
        new AutomaticTransferCompletion(f.Journal).Execute(p.Plan.Request.OperationId);
        Assert.Equal(original.Id,Assert.Single(f.State.ContentCatalog.PosterItems).Id);
    }
    [Fact] public void SameNameRejectedBeforeJournalAndFreshNameCanRetry()
    {
        using var f=new Fixture();string source=f.File("conflict.txt");
        File.WriteAllText(Path.Combine(f.Collection.StorageLocator!,"conflict.txt"),"other");
        Assert.Throws<IOException>(()=>DirectTransferPreparation.Incoming(f.State,f.Main,[source],new(f.Root,f.Collection)));
        Assert.Empty(f.Journal.Read());Assert.True(File.Exists(source));
        File.Move(source,source+".new");
        Assert.NotNull(DirectTransferPreparation.Incoming(f.State,f.Main,[source+".new"],new(f.Root,f.Collection)));
    }
    [Fact] public void DuplicatePayloadOrNestedSelectionCannotMoveTwice()
    {
        using var f=new Fixture();string source=f.File("one.txt");
        Assert.Throws<IOException>(()=>DirectTransferPreparation.Incoming(f.State,f.Main,[source,source],new(f.Root,f.Collection)));
        Assert.Throws<IOException>(()=>DirectTransferPreparation.Incoming(f.State,f.Main,[f.Source,source],new(f.Root,f.Collection)));
        Assert.Empty(f.Journal.Read());Assert.True(File.Exists(source));
    }
    [Fact] public void UnknownCardCannotTakeFilesOut()
    {
        using var f=new Fixture();
        Assert.Throws<IOException>(()=>DirectTransferPreparation.Outgoing(f.State,f.Main,["forged-card"]));
        Assert.Empty(f.Journal.Read());
    }
    [Fact] public void LimitedNativeGateRejectsCrossVolumeBeforeJournal()
    {
        using var f=new Fixture();var p=f.Stage(f.File("scope.txt"));
        var changed=new LocalTransferPreview(p.Plan,p.Files.Select(x=>x with{SameVolume=false}).ToArray());
        Assert.Throws<IOException>(()=>DirectTransferPreparation.RequireSupported(changed));
    }
}
