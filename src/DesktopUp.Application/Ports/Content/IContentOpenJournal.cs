namespace DesktopUp.Application;

// Admission and completion are atomic in the authoritative store. A completed
// activation remains a tombstone; callers must never replay external execution.
public interface IContentOpenJournal
{
    ContentOpenRequestAdmission TryStart(ContentOpenRequest request);
    ContentOpenFact? Complete(ContentOpenRequest request, ContentOpenCompletion completion);
}
