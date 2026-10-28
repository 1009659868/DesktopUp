using System.Security.AccessControl;
using System.Security.Principal;
using DesktopUp.Infrastructure;
using DesktopUp.Domain;
using F=DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class ManagedTransferSafetyTests
{
    [Fact] public void RealDeleteAclDenialRetainsSourceAndDoesNotCommitIndex()
    {
        using var f=new F();var p=f.Stage(f.File("acl-source.txt"));var file=new FileInfo(p.Files[0].Source);
        var original=file.GetAccessControl();var denied=file.GetAccessControl();
        var parent=new DirectoryInfo(f.Source);var parentOriginal=parent.GetAccessControl();var parentDenied=parent.GetAccessControl();
        parentDenied.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!,FileSystemRights.DeleteSubdirectoriesAndFiles,AccessControlType.Deny));
        denied.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!,FileSystemRights.Delete,AccessControlType.Deny));
        try
        {
            parent.SetAccessControl(parentDenied);file.SetAccessControl(denied);
            var row=Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId));
            Assert.Equal(DurableTransferPhase.Review,row.Phase);Assert.Contains("Win32 5:",row.Error);
            Assert.True(File.Exists(file.FullName));Assert.False(File.Exists(p.Files[0].Destination));Assert.Empty(f.State.ContentCatalog.Items);
        }
        finally
        {
            var parentRestore=new DirectorySecurity();parentRestore.SetSecurityDescriptorBinaryForm(parentOriginal.GetSecurityDescriptorBinaryForm(),AccessControlSections.Access);parent.SetAccessControl(parentRestore);
            var restore=new FileSecurity();restore.SetSecurityDescriptorBinaryForm(original.GetSecurityDescriptorBinaryForm(),AccessControlSections.Access);
            new FileInfo(File.Exists(file.FullName)?file.FullName:p.Files[0].Destination).SetAccessControl(restore);
        }
    }
    [Fact] public void RealDestinationCreateAclDenialDoesNotLoseSource()
    {
        using var f=new F();var p=f.Stage(f.File("acl-target.txt"));var directory=new DirectoryInfo(f.Collection.StorageLocator!);
        var original=directory.GetAccessControl();var denied=directory.GetAccessControl();
        denied.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!,FileSystemRights.CreateFiles,AccessControlType.Deny));
        try
        {
            directory.SetAccessControl(denied);
            var row=Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId));
            Assert.Equal(DurableTransferPhase.Review,row.Phase);Assert.Contains("Win32 5:",row.Error);
            Assert.True(File.Exists(p.Files[0].Source));Assert.False(File.Exists(p.Files[0].Destination));
        }
        finally {var restore=new DirectorySecurity();restore.SetSecurityDescriptorBinaryForm(original.GetSecurityDescriptorBinaryForm(),AccessControlSections.Access);directory.SetAccessControl(restore);}
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void TreeChangedDuringRenameRetainsActualMovedTreeForReview(bool add)
    {
        using var f=new F();string source=Path.Combine(f.Source,"tree");Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source,"first.txt"),"before");var p=f.Stage(source);
        var row=Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>
        {
            if(at==TransferExecutionCheckpoint.AfterMove)File.WriteAllText(Path.Combine(p.Files[0].Destination,add?"second.txt":"first.txt"),"concurrent change");
        }));
        Assert.Equal(DurableTransferPhase.Review,row.Phase);Assert.False(Directory.Exists(source));
        Assert.Equal("concurrent change",File.ReadAllText(Path.Combine(p.Files[0].Destination,add?"second.txt":"first.txt")));
        Assert.Empty(f.State.ContentCatalog.Items);Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Executor.Recover()).Phase);
    }
    [Theory] [InlineData(false)] [InlineData(true)] [Trait("Category","LocalMultiVolume")]
    public void InjectedPartialCopyOrWriteFullFailureKeepsBothAndNeverCleansSource(bool fullFailure)
    {
        using var f=new F();var temp=Directory.CreateTempSubdirectory("DesktopUp.Transfer.Safety.");
        try
        {
            string source=Path.Combine(temp.FullName,"copy.txt");File.WriteAllText(source,"complete owned original");
            var p=f.Stage(source);Assert.False(p.Files[0].SameVolume);
            var row=Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>
            {
                if(at!=TransferExecutionCheckpoint.AfterCopy)return;
                // Controlled post-copy fault injection, NOT proof of an actually full volume.
                File.WriteAllText(p.Files[0].Destination,"partial");
                if(fullFailure)throw new IOException("Injected write failure: ERROR_DISK_FULL (112)");
            }));
            Assert.Equal(DurableTransferPhase.Review,row.Phase);Assert.Null(row.Receipt);
            Assert.Equal("complete owned original",File.ReadAllText(source));Assert.Equal("partial",File.ReadAllText(p.Files[0].Destination));
            Assert.Empty(f.State.ContentCatalog.Items);Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Executor.Recover()).Phase);
        }
        finally {temp.Delete(true);} // Unique owned directory; never a user-supplied path.
    }
}
