using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed class StartupRegistrationTests
{
    private sealed class Fake : IStartupRegistrationBackend
    {
        public InstalledStartupTarget Target = new(@"C:\Apps\中文 空格\DesktopUp.exe");
        public StartupEntry Entry = new(false, null);
        public bool Uninstalled, Reject, LoseWrite, FailRead;
        public int Writes;
        public InstalledStartupTarget ResolveInstallation() => !Uninstalled ? Target : throw new IOException("Not installed");
        public StartupEntry Read() => !FailRead ? Entry : throw new UnauthorizedAccessException("Denied");
        public bool TryReplace(StartupEntry expected, string? command)
        {
            if (Reject || expected != Entry) return false;
            Writes++;
            if (!LoseWrite) Entry = new(command is not null, command);
            return true;
        }
    }
    [Fact] public void ReadsNeverRegisterAndExplicitToggleVerifiesActualState()
    {
        var backend = new Fake(); var service = new StartupRegistrationService(backend);
        Assert.Equal(StartupRegistrationState.Off, service.Read().State); Assert.Equal(0, backend.Writes);
        Assert.True(service.SetEnabled(true).Succeeded);
        Assert.Equal("\"C:\\Apps\\中文 空格\\DesktopUp.exe\" --autostart", backend.Entry.Command);
        Assert.True(service.SetEnabled(true).Succeeded); Assert.Equal(1, backend.Writes);
        backend.Entry = new(false, null); // External disable does not replay saved intent.
        Assert.False(service.Read().RegisteredHere); Assert.Equal(1, backend.Writes);
        Assert.True(service.SetEnabled(false).Succeeded); Assert.Equal(1, backend.Writes);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void AnotherInstallationOrUnexpectedValueIsNeverReplaced(bool remove)
    {
        var backend = new Fake { Entry = new(true, "\"D:\\Other\\DesktopUp.exe\" --autostart") };
        var result = new StartupRegistrationService(backend).SetEnabled(!remove);
        Assert.False(result.Succeeded); Assert.Equal(0, backend.Writes);
        Assert.Equal(StartupRegistrationState.OtherInstallation, result.Snapshot.State);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void ReadbackOrCompetingWriterFailureDoesNotReportSuccess(bool readback)
    {
        var backend = new Fake { Reject = !readback, LoseWrite = readback };
        Assert.False(new StartupRegistrationService(backend).SetEnabled(true).Succeeded);
    }
    [Fact] public void InvalidInstallationAndReadFailureCannotWrite()
    {
        var backend = new Fake { Uninstalled = true }; var service = new StartupRegistrationService(backend);
        Assert.False(service.SetEnabled(true).Succeeded);
        backend.Uninstalled = false; backend.FailRead = true;
        Assert.False(service.SetEnabled(false).Succeeded); Assert.Equal(0, backend.Writes);
    }
    [Fact] public void RemovingOwnEntryPreservesOffWithoutAutomaticReregistration()
    {
        var backend = new Fake(); backend.Entry = new(true, backend.Target.Command);
        var service = new StartupRegistrationService(backend);
        Assert.True(service.SetEnabled(false).Succeeded); Assert.False(backend.Entry.Exists);
        Assert.Equal(StartupRegistrationState.Off, service.Read().State); Assert.Equal(1, backend.Writes);
    }
    [Fact] public void CommandLengthBoundaryRejectsBeforeWrite()
    {
        var backend = new Fake { Target = new("C:\\" + new string('a', 260) + "\\DesktopUp.exe") };
        Assert.False(new StartupRegistrationService(backend).SetEnabled(true).Succeeded); Assert.Equal(0, backend.Writes);
    }
    [Fact] public void RealAdapterRejectsUninstalledAndProfileOverrideBeforeRegistryWrite()
    {
        Assert.Throws<IOException>(() => new WindowsStartupRegistration(AppContext.BaseDirectory, false, false).ResolveInstallation());
        Assert.Throws<IOException>(() => new WindowsStartupRegistration(AppContext.BaseDirectory, true, true).ResolveInstallation());
        Assert.Throws<IOException>(() => new WindowsStartupRegistration(AppContext.BaseDirectory, true, false).ResolveInstallation());
    }
}
