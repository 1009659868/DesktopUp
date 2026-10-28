using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record RegisterTemplateCommand(string RequestId, StateCommandSource Source, long ExpectedRevision,
    StateReadSet ReadSet, string SourceJson, string? SelectPosterId = null);

public sealed partial class StateProjectionCoordinator
{
    public StateCommandResult Execute(RegisterTemplateCommand command) => Run(command.RequestId, command.Source,
        command.ExpectedRevision, command.ReadSet, reads => command with { ReadSet = Required(reads) }, TemplateRecipeCommandPolicy.Prepare);
}

internal static class TemplateRecipeCommandPolicy
{
    public static StateMutationPlan Prepare(RegisterTemplateCommand command, DesktopStateSnapshot state)
    {
        if (command.Source != StateCommandSource.ControlCenter || command.ReadSet.SettingsVersion is null)
            return StateMutationPlan.Reject(StateCommandStatus.PermissionDenied, "Template registration requires control-center settings CAS.");
        var recipe = TemplateRecipeCodec.Parse(command.SourceJson);
        string catalog = TemplateRecipeCatalog.Add(state.Settings.Settings.TemplateCatalogJson, recipe);
        PosterDefinition[] updates = [];
        if (command.SelectPosterId is { } posterId)
        {
            if (!command.ReadSet.Posters.ContainsKey(posterId)) throw new ArgumentException("Import selection requires poster CAS.");
            var target = state.Catalog.Posters.FirstOrDefault(p => p.Poster.PosterId == posterId)?.Poster;
            if (target is null) return StateMutationPlan.Reject(StateCommandStatus.NotFound, "Poster not found.");
            var presentation = PosterPresentationPolicy.Resolve(target);
            if (!presentation.CanEdit) throw new ArgumentException("Unsupported poster parameters cannot be overwritten.");
            var desired = target with { TemplateKey = recipe.Id,
                LayoutParametersJson = PosterPresentationPolicy.Serialize(TemplateRecipeInstancePolicy.Select(presentation.Parameters, recipe) with
                { Canvas = presentation.Parameters.Canvas is { Current: not null } canvas ? new(checked(canvas.Revision + 1), null, canvas.Current, true, command.RequestId) : presentation.Parameters.Canvas }, target.LayoutParametersJson) };
            if (PosterPresentationPolicy.ValidateEdit(target, desired) is { } error) throw new ArgumentException(error);
            updates = [desired];
        }
        return new() { Settings = state.Settings.Settings with { TemplateCatalogJson = catalog }, Updates = updates, ResultPosterId = command.SelectPosterId };
    }
}

public static class TemplateRecipeCopy
{
    public static TemplateRecipe Create(TemplateRecipe source, string id, string name,
        IReadOnlyDictionary<string, TemplateParameterValue> values)
    {
        if (TemplateParameterBindingAdapter.ValidateValues(source.Schema, values) is { } error) throw new ArgumentException(error);
        return TemplateRecipeCodec.Freeze(source with { Schema = source.Schema with { TemplateId = id, Name = name, TemplateVersion = 1,
            Properties = source.Schema.Properties.Select(p => p with { DefaultValue = values[p.Key] }).ToArray() } });
    }
}
