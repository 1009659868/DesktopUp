using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed partial class StateProjectionCoordinator
{
    public StateCommandResult Execute(ContentLayoutCommand command) => Run(
        command.RequestId, command.Source, command.ExpectedRevision, command.ReadSet,
        reads => command with { ReadSet = Required(reads), DesiredDocument = FreezeContent(command.DesiredDocument) },
        ContentLayoutCommandPolicy.Prepare);

    private static ContentLayoutDocument FreezeContent(ContentLayoutDocument document)
    {
        if (ContentLayoutPolicy.Validate(document) is { } error) throw new ArgumentException(error);
        return ContentLayoutPolicy.Freeze(document);
    }
}
