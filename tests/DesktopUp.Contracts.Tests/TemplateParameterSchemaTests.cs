using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class TemplateParameterSchemaTests
{
    private static TemplateParameterSchema Schema => TemplateParameterSchemas.ForTemplate("lamp")!;
    private static readonly LampTemplateParameters Custom = new("#123456", 17.25, 89, "#264831", "#405162", 38);

    [Fact]
    public void DeclaredPaintAndMotionFieldsHaveCapabilityBindingsAndImmutableCDefaults()
    {
        Assert.Null(TemplateParameterSchemas.Validate(Schema));
        Assert.Equal(10, Schema.Properties.Count);
        Assert.Equal(3, Schema.Properties.Count(p => p.Kind == TemplateParameterKind.Color));
        Assert.Equal(5, Schema.Properties.Count(p => p.Kind == TemplateParameterKind.Range));
        Assert.Equal(LampTemplatePolicy.Defaults, TemplateParameterBindingAdapter.Reset(Schema, Custom));
        Assert.Throws<NotSupportedException>(() => ((IList<TemplateParameterDefinition>)Schema.Properties)[0] = Schema.Properties[1]);
        var defaults = TemplateParameterSchemas.Defaults(Schema);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, TemplateParameterValue>)defaults)["softness"] = TemplateParameterValue.FromNumber(2));
        Assert.Equal(60, TemplateParameterSchemas.Defaults(Schema)["softness"].Number);
    }

    [Fact]
    public void RenamedKeysLabelsGroupsAndOrderStillMapByBindingAndRetainFractionalSavedValues()
    {
        var described = TemplateParameterSchemas.Freeze(Schema with
        {
            TemplateId = "test.alias", Name = "声明别名测试",
            Properties = Schema.Properties.Reverse().Select((p, i) => p with { Key = "custom" + i, Label = "自定义" + i, Group = "另一分组" }).ToArray()
        });
        var values = TemplateParameterBindingAdapter.Read(described, Custom);
        Assert.DoesNotContain("softness", values.Keys);
        Assert.Null(TemplateParameterBindingAdapter.ValidateValues(described, values));
        Assert.Equal(Custom, TemplateParameterBindingAdapter.ComposeDraft(described, values, LampTemplatePolicy.Defaults));
        Assert.Equal(17.25, values[described.Properties.Single(p => p.Binding == "lamp.softness").Key].Number);
        Assert.Equal(LampTemplatePolicy.Defaults, TemplateParameterBindingAdapter.Reset(described, Custom));
    }

    [Theory]
    [InlineData("binding")] [InlineData("duplicate-binding")] [InlineData("duplicate-key")]
    [InlineData("type")] [InlineData("range")] [InlineData("nan-step")]
    [InlineData("default")] [InlineData("capability")] [InlineData("version")] [InlineData("count")]
    public void UnknownOrMalformedDescriptionCannotReachAdapter(string fault)
    {
        var fields = Schema.Properties.ToArray(); var schema = Schema;
        switch (fault)
        {
            case "binding": fields[0] = fields[0] with { Binding = "shell.execute" }; break;
            case "duplicate-binding": fields[3] = fields[3] with { Binding = fields[0].Binding }; break;
            case "duplicate-key": fields[1] = fields[1] with { Key = fields[0].Key }; break;
            case "type": fields[0] = fields[0] with { Kind = TemplateParameterKind.Range }; break;
            case "range": fields[1] = fields[1] with { Maximum = 101 }; break;
            case "nan-step": fields[1] = fields[1] with { Step = double.NaN }; break;
            case "default": fields[0] = fields[0] with { DefaultValue = TemplateParameterValue.FromColor("url(file:///private)") }; break;
            case "capability": schema = schema with { RendererCapability = "rain@1" }; break;
            case "version": schema = schema with { SchemaVersion = 9 }; break;
            case "count": fields = Enumerable.Repeat(fields[0], 21).ToArray(); break;
        }
        schema = schema with { Properties = fields };
        Assert.NotNull(TemplateParameterSchemas.Validate(schema));
        Assert.Throws<ArgumentException>(() => TemplateParameterBindingAdapter.Read(schema, Custom));
    }

    [Fact]
    public void ValuesRejectMissingUnknownAndWrongTypeWithoutSilentlyApplyingPartialDefaults()
    {
        var values = TemplateParameterBindingAdapter.Read(Schema, Custom).ToDictionary(p => p.Key, p => p.Value);
        values.Remove("lightColor");
        Assert.NotNull(TemplateParameterBindingAdapter.ValidateValues(Schema, values));
        Assert.Throws<ArgumentException>(() => TemplateParameterBindingAdapter.ComposeDraft(Schema, values, Custom));
        values["unknown"] = TemplateParameterValue.FromColor("#AABBCC");
        Assert.Throws<ArgumentException>(() => TemplateParameterBindingAdapter.ComposeDraft(Schema, values, Custom));
        values.Remove("unknown"); values["lightColor"] = TemplateParameterValue.FromNumber(2);
        Assert.Throws<ArgumentException>(() => TemplateParameterBindingAdapter.ComposeDraft(Schema, values, Custom));
    }

    [Fact]
    public void RecipeKeepsDefaultsSeparateFromCurrentOverridesAndDoesNotPromiseImportOrAi()
    {
        var before = TemplateParameterRecipeView.Create("lamp", PosterPresentationPolicy.Defaults() with { Lamp = Custom });
        var later = TemplateParameterRecipeView.Create("lamp", PosterPresentationPolicy.Defaults() with { Lamp = Custom with { Softness = 92 } });
        Assert.Equal(before.DefinitionJson, later.DefinitionJson);
        Assert.NotEqual(before.InstanceJson, later.InstanceJson);
        using var definition = JsonDocument.Parse(before.DefinitionJson);
        Assert.Equal("desktopup.template", definition.RootElement.GetProperty("format").GetString());
        Assert.Equal(60, definition.RootElement.GetProperty("properties").EnumerateArray().Single(p => p.GetProperty("bind").GetString() == "lamp.softness").GetProperty("default").GetDouble());
        Assert.Contains("鼠标跟随", before.CapabilitySummary);
        Assert.Contains("可装入", before.AvailabilitySummary);
        Assert.Empty(TemplateParameterSchemas.ForTemplate("basic-box")!.Properties);
        Assert.Null(TemplateParameterSchemas.ForTemplate("user.imported"));
        Assert.Contains("没有已支持", TemplateParameterRecipeView.Create("user.imported", PosterPresentationPolicy.Defaults()).CapabilitySummary);
    }

    [Fact]
    public void GenericDraftUsesExistingOwnerCasSqliteAndReopenWithoutChangingDefaultsOrOtherPoster()
    {
        using var fixture = new Fixture();
        var current = fixture.Coordinator.Refresh();
        var b = fixture.Coordinator.Execute(new CreatePosterCommand("add-b", StateCommandSource.ControlCenter, current.Revision,
            StateReadSet.ForCatalog(current.CatalogVersion), Fixture.Seed() with { Title = "B" }));
        Assert.True(b.Succeeded, b.Error);
        current = fixture.Coordinator.Refresh(); var target = current.PostersById["A"];
        string? other = current.PostersById[b.ResultPosterId!].Poster.LayoutParametersJson;
        string recipe = TemplateParameterRecipeView.Create("lamp", target.Presentation.Parameters).DefinitionJson;
        var values = TemplateParameterBindingAdapter.Read(Schema, Custom).ToDictionary(p => p.Key, p => p.Value);
        values["lightColor"] = TemplateParameterValue.FromColor("#12");
        var invalid = TemplateParameterBindingAdapter.ComposeDraft(Schema, values, LampTemplatePolicy.Defaults);
        var session = new ControlCenterSettingsSession();
        Assert.False(session.Update(new(target, PosterSetting.Lamp, Lamp: invalid)));
        Assert.True(session.ProtectsInteraction); Assert.Equal("#12", session.Preview!.Lamp!.LightColor);
        session.Finish(); Assert.Null(session.TakeNext());
        Assert.Equal(current.Revision, fixture.Coordinator.Refresh().Revision);
        session.CancelPreview(); Assert.False(session.ProtectsInteraction);
        values["lightColor"] = TemplateParameterValue.FromColor(Custom.LightColor);
        Assert.True(session.Update(new(target, PosterSetting.Lamp,
            Lamp: TemplateParameterBindingAdapter.ComposeDraft(Schema, values, LampTemplatePolicy.Defaults))));
        session.Finish(); var command = session.TakeNext()!;
        var saved = fixture.Coordinator.Execute(command); Assert.True(saved.Succeeded, saved.Error); Assert.True(session.Complete(saved));
        Assert.Equal(current.Revision + 1, saved.CurrentRevision);
        var after = fixture.Coordinator.Refresh();
        Assert.Equal(Custom, after.PostersById["A"].Presentation.Parameters.Lamp);
        Assert.Equal(other, after.PostersById[b.ResultPosterId!].Poster.LayoutParametersJson);
        Assert.Equal(recipe, TemplateParameterRecipeView.Create("lamp", after.PostersById["A"].Presentation.Parameters).DefinitionJson);
        var stale = new PosterSettingEdit(target, PosterSetting.Lamp, Lamp: Custom with { Intensity = 7 }).Command("stale-generic");
        Assert.Equal(StateCommandStatus.Conflict, fixture.Coordinator.Execute(stale).Status);
        fixture.Store.Dispose();
        using var reopened = new SqliteStateStore(new UserDataPaths(fixture.DirectoryPath));
        Assert.True(reopened.Initialize().Succeeded);
        var restored = new StateProjectionCoordinator(new SqliteStateCommandRepository(reopened)).Refresh();
        Assert.Equal(saved.CurrentRevision, restored.Revision);
        Assert.Equal(Custom, restored.PostersById["A"].Presentation.Parameters.Lamp);
        Assert.Equal(other, restored.PostersById[b.ResultPosterId!].Poster.LayoutParametersJson);
    }

    private sealed class Fixture : IDisposable
    {
        public static PosterDefinition Seed() => new("A", "Desk", "Items", "display", 0, 600, 300, 600, 0, .6, .3, .6,
            false, true, false, "adaptive-grid", "{}", "lamp");
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "DesktopUp.TemplateDescription.Tests", Guid.NewGuid().ToString("N"));
        public SqliteStateStore Store { get; }
        public StateProjectionCoordinator Coordinator { get; }
        public Fixture()
        {
            Store = new(new UserDataPaths(DirectoryPath)); Assert.True(Store.Initialize().Succeeded);
            Coordinator = new(new SqliteStateCommandRepository(Store)); Coordinator.EnsureInitialized(Seed());
        }
        public void Dispose() { Store.Dispose(); SqliteConnection.ClearAllPools(); Directory.Delete(DirectoryPath, true); }
    }
}
