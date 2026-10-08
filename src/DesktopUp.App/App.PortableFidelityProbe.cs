using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

public partial class App
{
    // Only called by --portable-smoke --portable-full in an isolated diagnostic root.
    // Uses normal commands so these are committed settings, not a second serialization fixture.
    private void PreparePortableFidelityProbe()
    {
        var coordinator = (StateProjectionCoordinator)_stateCoordinator!;
        void Commit(StateCommandResult result)
        { if (!result.Succeeded) throw new InvalidOperationException("Portable fidelity setup: " + result.Error); }
        var recipes = SceneCapabilityRegistry.Templates.Select(r => r with { Schema = r.Schema with {
            TemplateId = "portable-" + r.Id, Name = "往返 / " + r.Name } }).Append(
            TemplateRecipeCatalog.BuiltIns.Single(r => r.Id == "basic-box") with {
                Schema = TemplateParameterSchemas.ForTemplate("basic-box")! with { TemplateId = "portable-unused", Name = "未使用的样片" } }).ToArray();
        foreach (var recipe in recipes)
        {
            var current = coordinator.Refresh();
            Commit(coordinator.Execute(new RegisterTemplateCommand("portable-register-" + recipe.Id, StateCommandSource.ControlCenter,
                current.Revision, StateReadSet.ForSettings(current.ControlCenter.Settings.EntityVersion), TemplateRecipeCodec.Serialize(recipe))));
        }
        foreach (var id in coordinator.Refresh().Posters.Select(p => p.Poster.PosterId).ToArray())
        {
            foreach (var recipe in recipes.Where(r => r.Id != "portable-unused"))
            {
                Commit(coordinator.Execute(new PosterSettingEdit(coordinator.Refresh().PostersById[id], PosterSetting.Template,
                    Text: recipe.Id, Recipe: recipe).Command("portable-select-" + id + recipe.Id)));
                var basis = coordinator.Refresh().PostersById[id]; var values = basis.Presentation.TemplateValues;
                foreach (var property in recipe.Schema.Properties)
                    values = values.Set(property.Binding, property.Kind switch {
                        TemplateParameterKind.Boolean => TemplateParameterValue.FromBoolean(false),
                        TemplateParameterKind.Color => TemplateParameterValue.FromColor("#5879AB"),
                        _ => TemplateParameterValue.FromNumber(property.Minimum!.Value +
                            (property.Maximum!.Value - property.Minimum.Value) * .37) });
                Commit(coordinator.Execute(new PosterSettingEdit(basis, PosterSetting.TemplateParameters, TemplateValues: values)
                    .Command("portable-parameters-" + id + recipe.Id)));
            }
            Commit(coordinator.Execute(new PosterSettingEdit(coordinator.Refresh().PostersById[id], PosterSetting.TitleVisibility, Number: 0)
                .Command("portable-hide-title-" + id)));
            Commit(coordinator.Execute(new PosterSettingEdit(coordinator.Refresh().PostersById[id], PosterSetting.TitleColors,
                TitleColors: new("#CFDBED", "#EDCFB8")).Command("portable-title-ink-" + id)));
            Commit(coordinator.Execute(new PosterSettingEdit(coordinator.Refresh().PostersById[id], PosterSetting.Spacing, Number: .87)
                .Command("portable-spacing-" + id)));
        }
        var projection = coordinator.Refresh();
        Commit(coordinator.Execute(new SettingsStateCommand("portable-center-theme", StateCommandSource.ControlCenter,
            SettingsStateCommandKind.SetCenterTheme, projection.Revision, projection.ControlCenter.Settings.Settings with { CenterTheme = "blue" })
            { ReadSet = StateReadSet.ForSettings(projection.ControlCenter.Settings.EntityVersion) }));
    }
}
