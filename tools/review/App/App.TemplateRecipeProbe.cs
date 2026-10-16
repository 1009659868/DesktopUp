using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

public partial class App
{
    // Invoked only behind App's explicit isolated-data-root gate. The companion script starts
    // a second clean process against the same isolated SQLite database for restart verification.
    private void RunTemplateRecipeProbe(string dataRoot, bool restart)
    {
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(100);
        var lease = _ledger!.Acquire("fixture.template-recipes", "actual-controls-and-restart");
        var operations = new Queue<(string Name, Action Action, Func<bool>? Ready)>();
        var observations = new List<object>();
        var captures = new List<string>();
        string expectedPath = Path.Combine(dataRoot, "template-recipe-expected.json");
        string a = "", b = "", importedId = "probe-imported-desk", copyId = "", step = "initializing";
        string? unchangedB = null, beforeCopy = null, beforeTheme = null;
        long baseline = 0, readyAfter = 0, deadline = _autoClock.ElapsedMilliseconds + 100000;
        bool shown = false, initialized = false, ticking = false, stopped = false;
        Task? capture = null;
        ControlCenterWindow? window = null;
        DesktopUpProjection Current() => _stateCoordinator!.Refresh();
        string Fingerprint(DesktopUpProjection projection) => StateRequestFingerprint.Compute(new
        {
            projection.Revision, projection.CatalogVersion, Settings = projection.ControlCenter.Settings,
            Posters = projection.Posters.Select(p => new { p.Poster, p.EntityVersion, p.PosterRevision }).ToArray()
        });
        string PosterJson(string id) => JsonSerializer.Serialize(Current().PostersById[id].Poster);
        IReadOnlyList<TemplateRecipe> Catalog() => TemplateRecipeCatalog.All(Current().ControlCenter.Settings.Settings.TemplateCatalogJson);
        LampTemplateParameters Parameters() => PosterPresentationPolicy.Resolve(Current().PostersById[a].Poster).TemplateParameters;
        void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(step + ": " + message); }
        void OtherUnchanged() => Require(PosterJson(b) == unchangedB, "Another poster's configuration changed.");
        void Step(string name, Action action, Func<bool>? ready = null) => operations.Enqueue((name, action, ready));
        void Snapshot(string name, bool dialog)
        {
            string path = Path.Combine(dataRoot, "template-recipe-" + name + ".png");
            captures.Add(path);
            capture = dialog ? window!.CaptureTemplateDialogForProbeAsync(path) : window!.CaptureSettingsForProbeAsync(path);
        }
        void VerifyDynamicFields(double intensity, bool follow)
        {
            var ui = window!.ReadTemplateRecipeUiForProbe();
            var range = ui.Fields.Single(f => f.Key == "radiance");
            var toggle = ui.Fields.Single(f => f.Key == "cursorFollow");
            Require(ui.TemplateId == importedId && ui.Fields.Length == TemplateParameterSchemas.ForTemplate("lamp")!.Properties.Count &&
                range.Binding == "lamp.intensity" && range.Label.StartsWith("自定义亮度", StringComparison.Ordinal) &&
                range.Kind == "Range" && range.AutomationId == "Lamp_radiance" && range.Connected && range.Value.Number == intensity &&
                toggle.Binding == "lamp.follow" && toggle.Kind == "Boolean" && toggle.Label == "鼠标追光" &&
                toggle.AutomationId == "Lamp_cursorFollow" && toggle.Connected && toggle.Value.Boolean == follow,
                "Actual controls did not follow the imported key, label, Boolean kind, binding and values.");
        }
        void Stop()
        {
            if (stopped) return;
            stopped = true; timer.Stop(); timer.Tick -= Tick; lease.Dispose(); _stopAutoSmoke = null;
        }
        _stopAutoSmoke = Stop;
        void Finish(bool passed, string? error)
        {
            Stop();
            _log!.Write(passed ? "info" : "error", "template.recipe_observed", new
            {
                passed, error, step, restart, observations, captures,
                actualControlHandlers = true, nativeMouse = false, nativeKeyboard = false, nativeFilePicker = false,
                processRestartTested = restart && passed, desktopPixelVisualCertified = false,
                captureIncludesSystemBackdrop = false, uiCProductAccepted = false, milestoneAccepted = false
            });
            ExitApplication();
        }
        void Initialize()
        {
            var initial = Current();
            Require(initial.Posters.Count == 2, "The recipe probe requires its isolated two-poster fixture.");
            a = initial.Posters[0].Poster.PosterId; b = initial.Posters[1].Poster.PosterId;
            unchangedB = PosterJson(b);
            window!.SelectSettingsTargetForProbe(a);
            double scale = window.Content.XamlRoot.RasterizationScale;
            window.PrepareUiCSampleForProbe(true, (int)Math.Ceiling(980 * scale), (int)Math.Ceiling(860 * scale));
            if (restart)
            {
                using var expected = JsonDocument.Parse(File.ReadAllText(expectedPath));
                Require(expected.RootElement.GetProperty("fingerprint").GetString() == Fingerprint(initial),
                    "Restart changed saved catalog, parameters, theme or versions.");
                a = expected.RootElement.GetProperty("posterId").GetString()!;
                importedId = expected.RootElement.GetProperty("templateId").GetString()!;
                copyId = expected.RootElement.GetProperty("copyId").GetString()!;
                window.SelectSettingsTargetForProbe(a);
                window.RevealLampParametersForProbe();
                Step("restart-restores-catalog-schema-values-and-theme", () =>
                {
                    Require(Catalog().Count == TemplateRecipeCatalog.BuiltIns.Count + 2 && Catalog().Any(r => r.Id == copyId) &&
                        Current().ControlCenter.Settings.Settings.CenterTheme == "system", "Restart lost the saved copy or center theme.");
                    Require(Parameters().Intensity == 61 && !Parameters().FollowPointer, "Restart lost imported per-poster values.");
                    VerifyDynamicFields(61, false); OtherUnchanged(); Snapshot("restart-center", false);
                });
                Step("restart-theme-normal-and-picker-states", () => capture = window.VerifyCenterThemeForProbeAsync(dataRoot, "restart-system"));
                return;
            }
            Require(Catalog().Count == TemplateRecipeCatalog.BuiltIns.Count, "Initial catalog must contain only the current build-linked built-ins.");
            var basis = TemplateRecipeCatalog.BuiltIns.Single(r => r.Id == "lamp");
            var imported = basis with { Schema = basis.Schema with
            {
                TemplateId = importedId, Name = "配方验证书桌",
                Properties = basis.Schema.Properties.Select(p => p.Binding switch
                {
                    "lamp.intensity" => p with { Key = "radiance", Label = "自定义亮度", DefaultValue = TemplateParameterValue.FromNumber(45) },
                    "lamp.follow" => p with { Key = "cursorFollow", Label = "鼠标追光" },
                    _ => p
                }).ToArray()
            }};
            string source = TemplateRecipeCodec.Serialize(imported);
            Step("open-production-import-dialog", () => { baseline = Current().Revision; window.OpenTemplateDialogForProbe("import"); });
            Step("check-new-recipe-without-write", () =>
            {
                window.FillTemplateJsonForProbe(source); window.InvokeTemplateDialogForProbe("check");
            }, () => window.TemplateDialogReadyForProbe);
            Step("capture-checked-import-dialog", () =>
            {
                Require(window.ReadTemplateRecipeUiForProbe().PrimaryEnabled && Current().Revision == baseline && Catalog().Count == TemplateRecipeCatalog.BuiltIns.Count,
                    "Checking a valid recipe must enable import without writing state.");
                Snapshot("import-dialog", true);
            });
            Step("apply-checked-import", () => window.InvokeTemplateDialogForProbe("primary"));
            Step("import-commits-once-and-builds-dynamic-controls", () =>
            {
                Require(Current().Revision == baseline + 1 && Catalog().Count == TemplateRecipeCatalog.BuiltIns.Count + 1 && Current().PostersById[a].Poster.TemplateKey == importedId,
                    "Import must add one recipe and select it in one transaction.");
                Require(Parameters().Intensity == 45 && Parameters().FollowPointer, "Imported defaults were not applied.");
                OtherUnchanged(); window.RevealLampParametersForProbe();
            }, () => window.TemplateDialogClosedForProbe);
            Step("observe-renamed-and-boolean-controls", () => VerifyDynamicFields(45, true));
            Step("verify-initial-center-theme-states", () => capture = window.VerifyCenterThemeForProbeAsync(dataRoot, "initial"));
            foreach (var choice in new[] { ("system", "跟随系统"), ("ink", "墨色"), ("blue", "深海"), ("light", "纸白") })
            {
                Step("open-theme-" + choice.Item1, () => window.OpenTemplateDialogForProbe("theme"), () => window.TemplateDialogClosedForProbe);
                Step("choose-theme-" + choice.Item1, () => window.ChooseCenterThemeForProbe(choice.Item2), () => window.TemplateDialogReadyForProbe);
                Step("close-theme-" + choice.Item1, () => window.InvokeTemplateDialogForProbe("close"));
                Step("verify-theme-" + choice.Item1, () => capture = window.VerifyCenterThemeForProbeAsync(dataRoot, choice.Item1), () => window.TemplateDialogClosedForProbe);
            }
            Step("open-duplicate-recipe", () => { baseline = Current().Revision; window.OpenTemplateDialogForProbe("import"); });
            Step("reject-duplicate-without-write", () =>
            {
                window.FillTemplateJsonForProbe(source); window.InvokeTemplateDialogForProbe("check");
            }, () => window.TemplateDialogReadyForProbe);
            Step("duplicate-remains-disabled", () =>
            {
                var ui = window.ReadTemplateRecipeUiForProbe();
                Require(!ui.PrimaryEnabled && ui.DialogText.Any(text => text.Contains("ID", StringComparison.OrdinalIgnoreCase) || text.Contains("已", StringComparison.Ordinal)) &&
                    Current().Revision == baseline && Catalog().Count == TemplateRecipeCatalog.BuiltIns.Count + 1, "Duplicate ID must leave state unchanged and import disabled.");
                OtherUnchanged(); window.InvokeTemplateDialogForProbe("close");
            });
            Step("page-real-template-catalog", () =>
            {
                window.PrepareUiCSampleForProbe(true, (int)Math.Ceiling(980 * scale), (int)Math.Ceiling(860 * scale));
                window.NextTemplatePageForProbe();
            }, () => window.TemplateDialogClosedForProbe);
            Step("capture-real-fourth-template-page", () =>
            {
                var ui = window.ReadTemplateRecipeUiForProbe();
                int total = TemplateRecipeCatalog.BuiltIns.Count + 1;
                Require(ui.CatalogCount == total && ui.Page == 1 && ui.Range == $"4–{Math.Min(6, total)} / {total}" &&
                    ui.Samples.SequenceEqual(Catalog().Skip(3).Take(3).Select(r => r.Id)) && ui.PreviousEnabled && ui.NextEnabled == (total > 6),
                    "Paging must display actual build-linked and imported templates with bounded arrows.");
                Snapshot("catalog-page", false);
            });
            Step("open-real-template-book", () => window.OpenTemplateDialogForProbe("book"));
            Step("capture-four-card-template-book", () =>
            {
                Require(window.ReadTemplateRecipeUiForProbe().BookCards == TemplateRecipeCatalog.BuiltIns.Count + 1,
                    "The actual template book must contain every built-in and imported card.");
                Snapshot("template-book", true);
            }, () => window.TemplateDialogReadyForProbe);
            Step("close-template-book-without-selection", () => window.InvokeTemplateDialogForProbe("close"));
            Step("preview-imported-number", () =>
            {
                baseline = Current().Revision; window.RevealLampParametersForProbe();
                window.ChangeRecipeNumberForProbe("radiance", 67, false);
            }, () => window.TemplateDialogClosedForProbe);
            Step("preview-does-not-write", () =>
            {
                Require(Current().Revision == baseline && Parameters().Intensity == 45 && window.ReadTemplateRecipeUiForProbe().DraftActive,
                    "A held parameter preview must stay a draft.");
                VerifyDynamicFields(67, true); OtherUnchanged(); Snapshot("parameter-preview", false);
            });
            Step("cancel-imported-number-preview", () => window.CancelLampDraftForProbe());
            Step("cancel-restores-value-without-write", () =>
            {
                Require(Current().Revision == baseline && !window.ReadTemplateRecipeUiForProbe().DraftActive && Parameters().Intensity == 45,
                    "Cancel must restore the imported parameter without a transaction.");
                VerifyDynamicFields(45, true); OtherUnchanged(); window.ChangeRecipeNumberForProbe("radiance", 72, true);
            });
            Step("number-commits-only-selected-poster", () =>
            {
                Require(Current().Revision == baseline + 1 && Parameters().Intensity == 72, "Number control must commit exactly once.");
                OtherUnchanged(); baseline = Current().Revision; window.ChangeRecipeBooleanForProbe("cursorFollow", false);
            });
            Step("boolean-commits-only-selected-poster", () =>
            {
                Require(Current().Revision == baseline + 1 && !Parameters().FollowPointer, "Imported Boolean control must commit exactly once.");
                VerifyDynamicFields(72, false); OtherUnchanged(); baseline = Current().Revision; window.ResetRecipeControlsForProbe();
            });
            Step("reset-uses-imported-defaults", () =>
            {
                Require(Current().Revision == baseline + 1 && Parameters().Intensity == 45 && Parameters().FollowPointer,
                    "Reset must apply this recipe's declared defaults in one transaction.");
                VerifyDynamicFields(45, true); OtherUnchanged(); window.ChangeRecipeNumberForProbe("radiance", 61, true);
            });
            Step("set-boolean-for-persistence", () => window.ChangeRecipeBooleanForProbe("cursorFollow", false));
            Step("open-save-copy-dialog", () =>
            {
                VerifyDynamicFields(61, false); baseline = Current().Revision; beforeCopy = PosterJson(a);
                window.OpenTemplateDialogForProbe("copy");
            });
            Step("capture-save-copy-dialog", () =>
            {
                window.RenameTemplateCopyForProbe("我的配方验证书桌"); Snapshot("save-copy-dialog", true);
            }, () => window.TemplateDialogReadyForProbe);
            Step("save-copy-through-production-dialog", () => window.InvokeTemplateDialogForProbe("primary"));
            Step("saved-copy-does-not-select-or-alter-current", () =>
            {
                Require(Current().Revision == baseline + 1 && Catalog().Count == TemplateRecipeCatalog.BuiltIns.Count + 2 && PosterJson(a) == beforeCopy,
                    "Saving a copy must add to the catalog without switching or changing the selected poster.");
                var copy = Catalog().Single(r => r.Name == "我的配方验证书桌"); copyId = copy.Id;
                Require(copy.Schema.Properties.Single(p => p.Binding == "lamp.intensity").DefaultValue.Number == 61 &&
                    copy.Schema.Properties.Single(p => p.Binding == "lamp.follow").DefaultValue.Boolean == false,
                    "Saved copy defaults must reflect the current imported parameters.");
                OtherUnchanged(); baseline = Current().Revision; beforeTheme = PosterJson(a); window.OpenTemplateDialogForProbe("theme");
            }, () => window.TemplateDialogClosedForProbe);
            Step("choose-center-theme-through-actual-card", () => window.ChooseCenterThemeForProbe("墨色"), () => window.TemplateDialogReadyForProbe);
            Step("theme-keeps-poster-style-and-values", () =>
            {
                Require(Current().Revision == baseline + 1 && Current().ControlCenter.Settings.Settings.CenterTheme == "ink" &&
                    PosterJson(a) == beforeTheme, "Center theme must commit once without changing the poster.");
                OtherUnchanged(); window.InvokeTemplateDialogForProbe("close");
            });
            Step("reveal-themed-parameters", () => window.RevealLampParametersForProbe(), () => window.TemplateDialogClosedForProbe);
            Step("capture-theme-and-custom-parameters", () =>
            {
                VerifyDynamicFields(61, false);
                Require(window.ReadTemplateRecipeUiForProbe().RenderedTheme == "Dark", "Center theme was not rendered.");
                Snapshot("theme-parameters", false);
            });
            Step("open-system-theme-for-restart", () => window.OpenTemplateDialogForProbe("theme"));
            Step("save-system-theme-for-restart", () => window.ChooseCenterThemeForProbe("跟随系统"), () => window.TemplateDialogReadyForProbe);
            Step("close-system-theme-for-restart", () => window.InvokeTemplateDialogForProbe("close"));
            Step("verify-final-system-theme", () => capture = window.VerifyCenterThemeForProbeAsync(dataRoot, "final-system"), () => window.TemplateDialogClosedForProbe);
            Step("save-restart-expectations", () =>
            {
                Require(!File.Exists(expectedPath), "Restart expectation must not overwrite prior evidence.");
                OtherUnchanged();
                File.WriteAllText(expectedPath, JsonSerializer.Serialize(new { fingerprint = Fingerprint(Current()), posterId = a,
                    otherPosterId = b, templateId = importedId, copyId }, new JsonSerializerOptions { WriteIndented = true }));
            });
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            if (ticking || stopped) return; ticking = true;
            try
            {
                Require(_autoClock.ElapsedMilliseconds < deadline, "Template recipe integration timed out.");
                if (!_autoReady || _spatialRuntime!.IsSpatialBusy || _autoClock.ElapsedMilliseconds < readyAfter) return;
                if (!shown) { _controlCenter!.Show(); shown = true; return; }
                window = _controlCenter!.SettingsWindowForContractProbe;
                if (window?.Content.XamlRoot is null) return;
                if (capture is not null)
                {
                    if (!capture.IsCompleted) return;
                    capture.GetAwaiter().GetResult(); capture = null;
                }
                if (!initialized) { Initialize(); initialized = true; readyAfter = _autoClock.ElapsedMilliseconds + 500; return; }
                if (operations.Count == 0) { Finish(true, null); return; }
                var operation = operations.Peek(); step = operation.Name;
                if (operation.Ready is not null && !operation.Ready()) return;
                operations.Dequeue(); operation.Action();
                observations.Add(new { name = step, revision = Current().Revision, ui = window.ReadTemplateRecipeUiForProbe() });
                readyAfter = _autoClock.ElapsedMilliseconds + 250;
            }
            catch (Exception error) { Finish(false, error.ToString()); }
            finally { ticking = false; }
        }
        timer.Tick += Tick; timer.Start();
    }
}
