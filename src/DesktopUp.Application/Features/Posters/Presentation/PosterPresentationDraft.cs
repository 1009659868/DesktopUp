using DesktopUp.Domain;

namespace DesktopUp.Application;

// A detached draft is bound to the entity originally opened, never to a later UI selection.
public sealed record PosterPresentationDraft(PosterSurfaceProjection Basis, string Title, string Subtitle,
    string TemplateKey, PosterPresentationParameters Parameters)
{
    public static PosterPresentationDraft Open(PosterSurfaceProjection target)
    {
        var resolved = target.Presentation;
        if (!resolved.CanEdit) throw new ArgumentException(resolved.Warning);
        return new(target, target.Poster.Title, target.Poster.Subtitle, target.Poster.TemplateKey, resolved.Parameters);
    }
    public PosterStateCommand ToCommand(string requestId) => new(requestId, StateCommandSource.ControlCenter,
        PosterStateCommandKind.SetPresentation, Basis.Revision, Basis.Poster.PosterId, Basis.Poster with
        {
            Title = Title, Subtitle = Subtitle, TemplateKey = TemplateKey,
            LayoutParametersJson = PosterPresentationPolicy.Serialize(Parameters, Basis.Poster.LayoutParametersJson)
        }) { ReadSet = StateReadSet.ForPoster(Basis.Poster.PosterId, Basis.EntityVersion) };
}
