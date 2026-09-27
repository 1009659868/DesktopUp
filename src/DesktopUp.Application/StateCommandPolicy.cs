using DesktopUp.Domain;

namespace DesktopUp.Application;

internal static class StateCommandPolicy
{
    public static StateMutationPlan Prepare(PosterStateCommand command, DesktopStateSnapshot state)
    {
        if (!Enum.IsDefined(command.Kind) || string.IsNullOrWhiteSpace(command.PosterId) || command.DesiredState.PosterId != command.PosterId)
            return Invalid("Invalid poster command or target.");
        if (command.ReadSet is { } reads && !reads.Posters.ContainsKey(command.PosterId))
            return Invalid("Target entity version is required.");
        PosterDefinition? current = Find(state, command.PosterId);
        if (current is null) return Missing();
        if (current.PlacementPending && command.Kind is PosterStateCommandKind.Move or PosterStateCommandKind.Resize or PosterStateCommandKind.SetCollapsed)
            return StateMutationPlan.Reject(StateCommandStatus.PermissionDenied, "Poster is waiting for placement; geometry and posture cannot change yet.");
        if (command.ControlCenterGeometry is not null) return ControlCenterSpatial.Prepare(command, state);
        if (command.Kind == PosterStateCommandKind.SetAutoPosture)
        {
            if (command.Source != StateCommandSource.ControlCenter) return Denied();
            if (command.ReadSet is null || command.SpatialGesture is not null || command.SpatialPosture is not null)
                return Invalid("Auto-posture settings require entity CAS and cannot contain spatial operations.");
            if (PosterAutoPosturePolicy.ValidateDelay(command.DesiredState.AutoCollapseDelaySeconds) is { } error)
                return Invalid(error);
        }
        if (command.Kind == PosterStateCommandKind.SetPresentation)
        {
            if (command.Source != StateCommandSource.ControlCenter) return Denied();
            if (command.ReadSet is null || command.SpatialGesture is not null || command.SpatialPosture is not null)
                return Invalid("Presentation edit requires entity CAS and cannot contain a spatial gesture.");
            if (PosterPresentationPolicy.ValidateEdit(current, command.DesiredState) is { } error) return Invalid(error);
            var recipeInstances = PosterPresentationPolicy.Resolve(command.DesiredState).Parameters.RecipeInstances;
            if (recipeInstances is not null)
            {
                var registered = TemplateRecipeCatalog.Read(state.Settings.Settings.TemplateCatalogJson).ToDictionary(r => r.Id, StringComparer.Ordinal);
                if (recipeInstances.Values.Any(i => !registered.TryGetValue(i.Recipe.Id, out var r) ||
                    TemplateRecipeCodec.Serialize(r) != TemplateRecipeCodec.Serialize(i.Recipe)))
                    return Invalid("Recipe definition is not registered or does not match the catalog.");
            }
            // Macro presentation changes must remain compatible with the independent authored grid.
            // Unknown documents stay exact read-only raw text; they are never guessed or rewritten here.
            if (ContentLayoutPolicy.TryRead(current.ContentLayoutJson, current.PosterId, out var content, out _))
            {
                var preferences = PosterPresentationPolicy.Resolve(command.DesiredState).Parameters.Preferences ?? new();
                try { ContentLayoutCommandPolicy.ValidateAuthoring(content!, preferences.Scale, preferences.Spacing); }
                catch (ArgumentException exception) { return Invalid("Presentation conflicts with the authored content layout: " + exception.Message); }
            }
        }
        if (command.SpatialPosture is not null) return SpatialPostureCommitPolicy.Prepare(command, state);
        string? spatialError = SpatialGestureCommitPolicy.Validate(command, state);
        if (spatialError is not null) return Invalid(spatialError);
        PosterDefinition desired = NormalizeCollapse(command.Kind, current, command.DesiredState);
        if (desired == current) return command.SpatialGesture?.HasUserAdjusted == true
            ? new() { Updates = new[] { current }, TouchLayoutIds = new[] { current.PosterId }, ResultPosterId = current.PosterId }
            : new() { ResultPosterId = current.PosterId };
        if (command.Kind is PosterStateCommandKind.Move or PosterStateCommandKind.Resize &&
            (current.IsLocked || (command.Kind == PosterStateCommandKind.Resize && current.IsCollapsed)))
            return StateMutationPlan.Reject(StateCommandStatus.PermissionDenied, "Locked/collapsed poster does not allow this geometry operation.");
        PosterDefinition allowed = command.Kind switch
        {
            PosterStateCommandKind.Move or PosterStateCommandKind.Resize => current with
            {
                MonitorDeviceName = desired.MonitorDeviceName,
                FallbackXDip = desired.FallbackXDip, FallbackYDip = desired.FallbackYDip,
                WidthDip = desired.WidthDip, ExpandedHeightDip = desired.ExpandedHeightDip,
                HorizontalAnchor = desired.HorizontalAnchor, VerticalAnchor = desired.VerticalAnchor,
                NormalizedWidth = desired.NormalizedWidth, NormalizedExpandedHeight = desired.NormalizedExpandedHeight
            },
            PosterStateCommandKind.SetLocked => current with { IsLocked = desired.IsLocked },
            PosterStateCommandKind.SetAutoPosture => current with { AutoCollapseEnabled = desired.AutoCollapseEnabled,
                AutoCollapseDelaySeconds = desired.AutoCollapseDelaySeconds },
            PosterStateCommandKind.SetCollapsed => current with { IsCollapsed = desired.IsCollapsed, VerticalAnchor = desired.VerticalAnchor },
            PosterStateCommandKind.SetVisible => current with { IsVisible = desired.IsVisible },
            PosterStateCommandKind.Rename => current with { Title = desired.Title },
            PosterStateCommandKind.SetPresentation => current with { Title = desired.Title, Subtitle = desired.Subtitle,
                TemplateKey = desired.TemplateKey, LayoutParametersJson = desired.LayoutParametersJson },
            _ => current
        };
        if (desired != allowed) return Invalid("The command changed unrelated fields.");
        return new() { Updates = new[] { desired }, ResultPosterId = current.PosterId };
    }

    public static StateMutationPlan Prepare(SettingsStateCommand command, DesktopStateSnapshot state)
    {
        if (!Enum.IsDefined(command.Kind)) return Invalid("Invalid settings command.");
        if (command.ReadSet is { SettingsVersion: null }) return Invalid("Settings entity version is required.");
        DesktopUpSettings current = state.Settings.Settings;
        DesktopUpSettings allowed = command.Kind switch
        {
            SettingsStateCommandKind.SetDynamicEffectsPaused => current with { DynamicEffectsPaused = command.DesiredState.DynamicEffectsPaused },
            SettingsStateCommandKind.SetPerformanceMode => current with { PerformanceMode = command.DesiredState.PerformanceMode },
            SettingsStateCommandKind.SetCenterTheme => current with { CenterTheme = command.DesiredState.CenterTheme },
            _ => current with { LaunchAtStartupRequested = command.DesiredState.LaunchAtStartupRequested }
        };
        if (allowed != command.DesiredState) return Invalid("The command changed unrelated settings.");
        return new() { Settings = current == allowed ? null : allowed };
    }

    public static StateMutationPlan Prepare(CreatePosterCommand command, DesktopStateSnapshot state)
    {
        if (!IsManagement(command.Source)) return Denied();
        if (command.ReadSet.CatalogVersion is null) return Invalid("Create requires catalog version.");
        if (command.Placement is not { } placement) return Create(state, command.Configuration);
        if (!ControlCenterSpatial.CompleteReads(command.ReadSet, state) || !ControlCenterSpatial.SceneReadsCurrent(placement.Initial, state)) return Invalid("Placement requires complete current catalog/scene reads.");
        var placed = ControlCenterSpatial.Place(command.Configuration, placement.Initial);
        return placed is null ? StateMutationPlan.Reject(StateCommandStatus.InsufficientSpace, "没有可容纳海报的完整空位；未创建配置。") : Create(state, placed);
    }

    public static StateMutationPlan Prepare(ClonePosterCommand command, DesktopStateSnapshot state)
    {
        if (!IsManagement(command.Source)) return Denied();
        if (command.ReadSet.CatalogVersion is null || !command.ReadSet.Posters.ContainsKey(command.SourcePosterId))
            return Invalid("Clone requires source and catalog versions.");
        PosterDefinition? source = Find(state, command.SourcePosterId);
        if (source is null) return Missing();
        source = source with { Title = command.Title ?? source.Title };
        if (command.Placement is not { } placement) return Create(state, source);
        if (!ControlCenterSpatial.CompleteReads(command.ReadSet, state) || !ControlCenterSpatial.SceneReadsCurrent(placement.Initial, state)) return Invalid("Placement requires complete current catalog/scene reads.");
        var placed = ControlCenterSpatial.Place(source, placement.Initial);
        return placed is null ? StateMutationPlan.Reject(StateCommandStatus.InsufficientSpace, "没有可容纳副本的完整空位；未创建配置。") : Create(state, placed);
    }

    public static StateMutationPlan Prepare(RemovePosterCommand command, DesktopStateSnapshot state)
    {
        if (command.Source is not (StateCommandSource.ControlCenter or StateCommandSource.Poster)) return Denied();
        if (!command.Confirmed) return StateMutationPlan.Reject(StateCommandStatus.ConfirmationRequired, "Deletion requires explicit consequence confirmation.");
        if (command.ReadSet.CatalogVersion is null || !command.ReadSet.Posters.ContainsKey(command.PosterId))
            return Invalid("Removal requires the confirmed target and catalog versions.");
        if (Find(state, command.PosterId) is null) return Missing();
        // Configuration only: this port has no filesystem operations.
        return new() { Removes = new[] { command.PosterId }, ResultPosterId = command.PosterId };
    }

    public static StateMutationPlan Prepare(BatchPosterCommand command, DesktopStateSnapshot state)
    {
        if (!IsManagement(command.Source)) return Denied();
        if (!Enum.IsDefined(command.Kind) || command.PosterIds.Any(string.IsNullOrWhiteSpace) ||
            command.PosterIds.Distinct(StringComparer.Ordinal).Count() != command.PosterIds.Count)
            return Invalid("Batch kind or target list is invalid.");
        if (command.PosterIds.Any(id => !command.ReadSet.Posters.ContainsKey(id))) return Invalid("Every batch target requires an entity version.");
        if (command.AllPosters && (command.ReadSet.CatalogVersion is null ||
            !command.PosterIds.ToHashSet(StringComparer.Ordinal).SetEquals(state.Catalog.Posters.Select(p => p.Poster.PosterId))))
            return Invalid("All-posters operation requires the exact catalog membership and version.");
        var updates = new List<PosterDefinition>();
        foreach (string id in command.PosterIds)
        {
            PosterDefinition? current = Find(state, id);
            if (current is null) return Missing();
            PosterDefinition desired = command.Kind == BatchPosterCommandKind.SetLocked
                ? current with { IsLocked = command.Value } : current with { IsVisible = command.Value };
            if (current != desired) updates.Add(desired);
        }
        return new() { Updates = updates.AsReadOnly() };
    }

    private static StateMutationPlan Create(DesktopStateSnapshot state, PosterDefinition configuration)
    {
        string id = "poster-" + Guid.NewGuid().ToString("N");
        PosterDefinition created = configuration with
        {
            PosterId = id, ThemeInstanceId = "theme-" + Guid.NewGuid().ToString("N"),
            CollectionId = null,
            ContentLayoutJson = ContentLayoutCommandPolicy.RemapStored(configuration, id),
            StackOrder = state.Catalog.Posters.Count == 0 ? 0 : checked(state.Catalog.Posters.Max(p => p.Poster.StackOrder) + 1)
        };
        return new() { Creates = new[] { created }, ResultPosterId = id };
    }

    private static PosterDefinition? Find(DesktopStateSnapshot state, string id) =>
        state.Catalog.Posters.FirstOrDefault(p => p.Poster.PosterId == id)?.Poster;
    private static bool IsManagement(StateCommandSource source) => source is StateCommandSource.ControlCenter or StateCommandSource.Tray;
    private static StateMutationPlan Invalid(string error) => StateMutationPlan.Reject(StateCommandStatus.ValidationFailed, error);
    private static StateMutationPlan Missing() => StateMutationPlan.Reject(StateCommandStatus.NotFound, "Poster no longer exists.");
    private static StateMutationPlan Denied() => StateMutationPlan.Reject(StateCommandStatus.PermissionDenied, "Command is not available from this surface.");

    // Legacy V3-04-03 path, retained until M04-04 connects the spatial expansion plan.
    // Fixed-top normalization is NOT the new boundary-expansion product contract.
    private static PosterDefinition NormalizeCollapse(PosterStateCommandKind kind, PosterDefinition current, PosterDefinition desired)
    {
        if (kind != PosterStateCommandKind.SetCollapsed || desired.IsCollapsed == current.IsCollapsed || current.NormalizedExpandedHeight <= 0)
            return desired;
        double workHeight = current.ExpandedHeightDip / current.NormalizedExpandedHeight;
        if (!double.IsFinite(workHeight) || workHeight <= 0) return desired;
        double collapsed = Math.Clamp(PosterFrameGeometryPolicy.CollapsedHeightDip / workHeight, 0.000001, 1);
        double oldHeight = current.IsCollapsed ? collapsed : current.NormalizedExpandedHeight;
        double newHeight = desired.IsCollapsed ? collapsed : current.NormalizedExpandedHeight;
        return desired with { VerticalAnchor = Math.Clamp(current.VerticalAnchor * Math.Max(0, 1 - oldHeight) / Math.Max(0.000001, 1 - newHeight), 0, 1) };
    }
}
