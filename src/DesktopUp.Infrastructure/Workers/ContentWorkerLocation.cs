namespace DesktopUp.Infrastructure;

// Build and publish both place the complete helper payload under content-worker.
public static class ContentWorkerLocation
{
    public static string InApplication(string directory)=>Path.Combine(Path.GetFullPath(directory),"content-worker","DesktopUp.ContentWorker.exe");
}
