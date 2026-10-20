using DesktopUp.Application;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    private ManagedIngressUseCase? _managedIngress;
    private MappedIngressUseCase? _mappedIngress;
    private CrossPosterTransferUseCase? _crossPosterTransfers;
    private NativeShellExitUseCase? _nativeShellExit;
    internal void ConfigureTransferUseCases(ManagedIngressUseCase managed, MappedIngressUseCase mapped,
        CrossPosterTransferUseCase crossPoster, NativeShellExitUseCase nativeExit)
    { _managedIngress = managed; _mappedIngress = mapped; _crossPosterTransfers = crossPoster; _nativeShellExit = nativeExit; }

    private sealed class IncomingPresentation(Func<bool> current, Action<string, int> progress, Action publish) : IIncomingTransferPresentation
    {
        public bool IsCurrent => current();
        public void ReportProgress(string path, int completed) => progress(path, completed);
        public void PublishCommittedChanges() => publish();
    }
    private sealed class NativeExitPresentation(Action publish,
        Func<IReadOnlyList<string>, CancellationToken, Task<bool>> confirm) : INativeExitPresentation
    {
        public void PublishObservedChanges() => publish();
        public Task<bool> ConfirmMissingReferences(IReadOnlyList<string> paths, CancellationToken token) => confirm(paths, token);
    }
}
