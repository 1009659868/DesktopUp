using DesktopUp.Domain;

namespace DesktopUp.Application;

// A paused prefix of the EXISTING schedule, not caller-supplied neighbor rectangles.
// AcceptedFrames counts successful group presentations, never attempted timer ticks.
public sealed record TitlePausedPosture(string OperationId, bool Collapsed, int AcceptedFrames);
public sealed record TitleSpatialStart(SpatialSnapshot Initial, TitlePress Press,
    PosterAnimationOptions Animation, TitlePausedPosture? Paused = null);
public sealed record TitleSpatialSample(TitlePointerInput? Pointer = null, int? PresentedFrames = null);
public sealed record TitleSpatialTrace(TitleSpatialStart Start, IReadOnlyList<TitleSpatialSample> Samples,
    SpatialSnapshot Presented, SpatialContinuityState Continuity)
{
    // Only the bound coordinator can seal this opaque in-process prefix. Plain commands reject it.
    public TitleProgressCheckpoint? Checkpoint { get; init; }
}
internal sealed record TitleSpatialState(SpatialSnapshot Scene, SpatialContinuityState Continuity,
    TitleInteractionState Intent, bool UserMoved = false, bool Complete = false, bool Cancelled = false);

// No native calls or persistence. Proposal identity and all-or-nothing presentation acceptance
// belong to TitleSpatialSession. The transaction repeats this computation from trusted state.
internal static class TitleSpatialKernel
{
    internal static int Compact(SpatialEnvironment env) =>
        Math.Max(1, checked((int)Math.Round(PosterFrameGeometryPolicy.CollapsedHeightDip * env.DpiY / 96d)));

    internal static TitleSpatialState Begin(TitleSpatialStart start, PosterContentMetrics metrics,
        SpatialContinuityState? previous)
    {
        start.Animation.Validate();
        var scene = start.Initial;
        if (scene.Posters.Count is < 1 or > 64 || PosterSpatialSolver.ValidateLayout(scene).Count != 0)
            throw new ArgumentException("Invalid or over-budget title scene.");
        string id = start.Press.Capture.Press.Session.PosterId;
        var source = scene.Posters.Single(p => p.PosterId == id);
        int compact = Compact(scene.Environment);
        if (!source.IsVisible || source.Bounds.Height < compact || start.Press.IsLocked != source.IsLocked)
            throw new ArgumentException("Title source is unavailable.");
        var nodes = Normalize(scene, previous, compact);
        TitlePresentedPosture stage = source.IsCollapsed ? TitlePresentedPosture.Compact : TitlePresentedPosture.Expanded;
        if (start.Paused is { } paused)
        {
            if (string.IsNullOrWhiteSpace(paused.OperationId) || paused.OperationId.Length > 128 ||
                paused.OperationId == start.Press.OperationId || source.IsCollapsed == paused.Collapsed)
                throw new ArgumentException("Invalid posture ownership transfer.");
            var plan = SpatialContinuitySolver.Plan(scene, id, paused.Collapsed, compact,
                paused.OperationId, metrics, nodes);
            if (!plan.Group.Plan.Succeeded || plan.Next is null) throw new ArgumentException("Invalid paused plan.");
            var schedule = SpatialAnimationSchedule.Create(scene, plan.Group.Frames, start.Animation);
            if (paused.AcceptedFrames < 0 || paused.AcceptedFrames >= schedule.Frames.Count)
                throw new ArgumentException("Only an unfinished accepted prefix can be transferred.");
            var prefix = schedule.Frames.Take(paused.AcceptedFrames).ToArray();
            var presented = Apply(scene, prefix);
            // Expansion creates/merges the causal cohort before completion. Collapse keeps the
            // original cohort until returns have finished; otherwise early dissolution loses Home.
            var basis = paused.Collapsed ? nodes : plan.Next;
            nodes = Rebase(presented, basis);
            if (!paused.Collapsed)
            {
                int up = source.Bounds.Y - presented.Posters.Single(p => p.PosterId == id).Bounds.Y;
                var original = Normalize(scene, previous, compact).Nodes.Single(n => n.Expected.PosterId == id);
                nodes = ChangeNode(nodes, id, n => n with { HomeY = original.HomeY - up, UpInset = up });
            }
            else
            {
                int restoredInset = presented.Posters.Single(p => p.PosterId == id).Bounds.Y - source.Bounds.Y;
                nodes = ChangeNode(nodes, id, n => n with { HomeY = n.HomeY + restoredInset,
                    UpInset = Math.Max(0, n.UpInset - restoredInset) });
            }
            scene = presented;
            stage = paused.Collapsed ? TitlePresentedPosture.Collapsing : TitlePresentedPosture.Expanding;
        }
        var bounds = scene.Posters.Single(p => p.PosterId == id).Bounds;
        if (start.Press.Posture != stage || start.Press.Capture.Press.Frame !=
            new DesktopPixelRect(bounds.X, bounds.Y, bounds.Width, bounds.Height))
            throw new ArgumentException("Press does not match the accepted presented frame/stage.");
        var intent = TitleInteractionModel.Begin(null, start.Press);
        if (intent.Action != TitleInteractionAction.PauseGroup || intent.State is null)
            throw new ArgumentException("Not a title interaction.");
        return new(scene, Rebase(scene, nodes), intent.State);
    }

    internal static (TitleSpatialState State, IReadOnlyList<SpatialBatchFrame> Frames) Advance(
        TitleSpatialStart start, PosterContentMetrics metrics, TitleSpatialState before, TitleSpatialSample sample, SpatialContinuityState? initialContinuity)
    {
        if (before.Complete || before.Cancelled) return (before, []);
        var step = sample.Pointer is { } input
            ? TitleInteractionModel.Advance(before.Intent, input) : new TitleInteractionStep(before.Intent);
        var intent = step.State!;
        if (step.Ignored || step.State is null) return (before, []);
        if (step.Action == TitleInteractionAction.CancelGroup)
            return (before with { Intent = intent, Cancelled = true }, []);
        var current = before with { Intent = intent };
        var frames = new List<SpatialBatchFrame>();
        string id = intent.Token.Session.PosterId;
        int compact = Compact(before.Scene.Environment);
        bool drag = intent.Phase is TitleInteractionPhase.CompactDrag or TitleInteractionPhase.ExpandedDrag ||
            step.Action is TitleInteractionAction.FinishCompactDrag or TitleInteractionAction.FinishExpandedDrag;
        bool compactDrag = drag && intent.Press.Posture != TitlePresentedPosture.Expanded;
        if (drag && sample.Pointer?.Kind != TitlePointerEventKind.Interrupt)
        {
            var p = current.Scene.Posters.Single(p => p.PosterId == id);
            if (p.IsLocked) throw new ArgumentException("Locked title cannot move.");
            var area = current.Scene.Environment.WorkArea;
            // Absolute grab offset, not accumulated accepted deltas: clamping and reversing do not drift.
            var desired = p.Bounds with {
                X = checked((int)Math.Clamp((long)intent.LastPosition.X - intent.GrabOffset.X, area.X, area.Right - p.Bounds.Width)),
                Y = checked((int)Math.Clamp((long)intent.LastPosition.Y - intent.GrabOffset.Y, area.Y, area.Bottom - p.Bounds.Height)) };
            var move = SpatialCollisionSolver.Resolve(current.Scene,
                new(id, desired, PosterGestureKind.Move, PosterOperatingMode.Using, 1, 1));
            if (!move.Succeeded) throw new ArgumentException("No safe title movement.");
            foreach (var leg in move.Path.Where(l => l.From != l.To))
                frames.Add(new(Array.AsReadOnly(new[] { leg })));
            var moved = Apply(current.Scene, frames);
            bool changed = moved.Posters.Single(p => p.PosterId == id).Bounds != p.Bounds;
            current = current with { Scene = moved, Continuity = Rebase(moved, current.Continuity),
                UserMoved = current.UserMoved || changed };
            // Manual ownership reanchors ONLY the source; preserve peer Home/cohort evidence.
            current = current with { Continuity = ChangeNode(current.Continuity, id,
                n => n with { HomeY = n.Expected.Bounds.Y, UpInset = 0 }) };
        }
        if (compactDrag)
        {
            var p = current.Scene.Posters.Single(p => p.PosterId == id);
            if (p.Bounds.Height > compact)
            {
                // Solve the COMPLETE return first (including unrelated blockers). Never redefine
                // a blocked full return as a legal partial goal just because this is one animation tick.
                var collapseScene = Replace(current.Scene, p with { IsCollapsed = false });
                var relation = Rebase(collapseScene, current.Continuity);
                var plan = SpatialContinuitySolver.Plan(collapseScene, id, true, compact,
                    intent.Token.OperationId, metrics, relation);
                if (!plan.Group.Plan.Succeeded || plan.Next is null) throw new ArgumentException("No safe compact return.");
                var schedule = SpatialAnimationSchedule.Create(collapseScene, plan.Group.Frames, start.Animation);
                var take = intent.Phase is TitleInteractionPhase.Released or TitleInteractionPhase.Interrupted ? schedule.Frames :
                    schedule.Frames.Take(1).ToArray();
                frames.AddRange(take);
                var next = Apply(collapseScene, take);
                var target = next.Posters.Single(p => p.PosterId == id);
                next = Replace(next, target with { IsCollapsed = target.Bounds.Height == compact });
                // Keep uncompleted causal cohorts; rebasing does not mint per-frame layout generations.
                current = current with { Scene = next, Continuity = Rebase(next, relation) };
            }
            else
            {
                var next = Replace(current.Scene, p with { IsCollapsed = true });
                current = current with { Scene = next, Continuity = Rebase(next, current.Continuity) };
            }
        }
        if (step.Action == TitleInteractionAction.ResumeGroup && start.Paused is { } resume)
        {
            var plan = SpatialContinuitySolver.Plan(start.Initial, id, resume.Collapsed, compact,
                resume.OperationId, metrics, initialContinuity);
            var schedule = SpatialAnimationSchedule.Create(start.Initial, plan.Group.Frames, start.Animation);
            var remaining = schedule.Frames.Skip(resume.AcceptedFrames).ToArray();
            frames.AddRange(remaining);
            var next = Apply(current.Scene, remaining);
            next = Replace(next, next.Posters.Single(p => p.PosterId == id) with { IsCollapsed = resume.Collapsed });
            current = current with { Scene = next, Continuity = Rebase(next, plan.Next!) };
        }
        if (step.Action == TitleInteractionAction.FastExpand)
        {
            if (start.Paused is { Collapsed: true } reversing)
            {
                // Pending never moved: reverse only the accepted collapse prefix. Re-solving a
                // partial rectangle as a fresh compact seed loses the original upward anchor and
                // can silently alter the next collapse even when today's endpoint looks correct.
                var plan = SpatialContinuitySolver.Plan(start.Initial, id, true, compact,
                    reversing.OperationId, metrics, initialContinuity);
                var schedule = SpatialAnimationSchedule.Create(start.Initial, plan.Group.Frames, start.Animation);
                var reverse = schedule.Frames.Take(reversing.AcceptedFrames).Reverse().Select(f =>
                    new SpatialBatchFrame(Array.AsReadOnly(f.Changes.Select(l =>
                        new SpatialPathLeg(l.PosterId, l.To, l.From)).ToArray()))).ToArray();
                frames.AddRange(reverse);
                var next = Apply(current.Scene, reverse);
                next = Replace(next, next.Posters.Single(p => p.PosterId == id) with { IsCollapsed = false });
                current = current with { Scene = next,
                    Continuity = Rebase(next, Normalize(start.Initial, initialContinuity, compact)) };
            }
            else if (start.Paused is { Collapsed: false } paused)
            {
                // Fast completion of an expansion is the exact same accepted plan endpoint.
                var plan = SpatialContinuitySolver.Plan(start.Initial, id, false, compact,
                    paused.OperationId, metrics, initialContinuity);
                var schedule = SpatialAnimationSchedule.Create(start.Initial, plan.Group.Frames, start.Animation);
                frames.AddRange(schedule.Frames.Skip(paused.AcceptedFrames));
                var next = Apply(current.Scene, schedule.Frames.Skip(paused.AcceptedFrames).ToArray());
                var source = next.Posters.Single(p => p.PosterId == id);
                next = Replace(next, source with { IsCollapsed = false });
                current = current with { Scene = next, Continuity = Rebase(next, plan.Next!) };
            }
            else
            {
                var p = current.Scene.Posters.Single(p => p.PosterId == id);
                var seed = Replace(current.Scene, p with { IsCollapsed = true });
                var plan = SpatialContinuitySolver.Plan(seed, id, false, compact, intent.Token.OperationId,
                    metrics, Rebase(seed, current.Continuity));
                if (plan.Group.Plan.Succeeded && plan.Next is not null)
                {
                    frames.AddRange(plan.Group.Frames);
                    var next = Apply(seed, plan.Group.Frames);
                    next = Replace(next, next.Posters.Single(p => p.PosterId == id) with { IsCollapsed = false });
                    current = current with { Scene = next, Continuity = Rebase(next, plan.Next) };
                }
                else if (plan.Outcome != SpatialExpansionOutcome.InsufficientSpace)
                    throw new ArgumentException("Fast expansion rejected.");
                // Insufficient space is a finite no-change result, not automatic permission to grow.
            }
        }
        current = current with { Complete = intent.Phase is TitleInteractionPhase.Released or TitleInteractionPhase.Interrupted };
        // Verify the combined sample and each constituent swept group frame, not just endpoints.
        Apply(before.Scene, frames);
        // When safe, movement and retraction share one presented group frame. If a slide
        // requires a bent path, retain its swept-safe legs instead of cutting the corner.
        if (drag && frames.Count > 1 && !current.Complete)
        {
            var joint = new SpatialBatchFrame(Array.AsReadOnly(current.Scene.Posters.Select(p =>
                new SpatialPathLeg(p.PosterId, before.Scene.Posters.Single(q => q.PosterId == p.PosterId).Bounds, p.Bounds))
                .Where(l => l.From != l.To).ToArray()));
            try { Apply(before.Scene, [joint]); frames = [joint]; }
            catch (ArgumentException) { /* Keep the already verified ordered segments. */ }
        }
        if (sample.PresentedFrames is { } count)
        {
            if (!drag || current.Complete || current.Cancelled || count <= 0 || count >= frames.Count)
                throw new ArgumentException("Invalid presented title prefix.");
            // Recompute a bounded prefix of the original swept proof; never accept supplied
            // rectangles or the unpresented endpoint's causal changes.
            frames = frames.Take(count).ToList();
            var prefix = Apply(before.Scene, frames);
            var source = prefix.Posters.Single(p => p.PosterId == id);
            var original = before.Scene.Posters.Single(p => p.PosterId == id);
            prefix = Replace(prefix, source with { IsCollapsed = compactDrag && source.Bounds.Height == compact });
            var relation = ChangeNode(Rebase(prefix, before.Continuity), id,
                n => n with { HomeY = n.Expected.Bounds.Y, UpInset = 0 });
            current = current with { Scene = prefix, Continuity = relation,
                UserMoved = before.UserMoved || source.Bounds.X != original.Bounds.X || source.Bounds.Y != original.Bounds.Y };
        }
        return (current, Array.AsReadOnly(frames.ToArray()));
    }

    internal static SpatialSnapshot Apply(SpatialSnapshot scene, IReadOnlyList<SpatialBatchFrame> frames)
    {
        foreach (var frame in frames)
        {
            if (frame.Changes.Select(l => l.PosterId).Distinct(StringComparer.Ordinal).Count() != frame.Changes.Count)
                throw new ArgumentException("Duplicate group participant.");
            var byId = scene.Posters.ToDictionary(p => p.PosterId, StringComparer.Ordinal);
            foreach (var leg in frame.Changes)
            {
                if (!byId.TryGetValue(leg.PosterId, out var p) || p.Bounds != leg.From ||
                    !leg.To.IsValid || !scene.Environment.WorkArea.Contains(leg.To) || p.IsLocked && leg.From.X != leg.To.X)
                    throw new ArgumentException("Discontinuous or unsafe group frame.");
                byId[p.PosterId] = p with { Bounds = leg.To };
            }
            for (int a = 0; a < scene.Posters.Count; a++)
            for (int b = a + 1; b < scene.Posters.Count; b++)
            {
                var p = scene.Posters[a]; var q = scene.Posters[b];
                if (p.IsVisible && q.IsVisible && SpatialPathPolicy.CrossesMoving(p.Bounds, byId[p.PosterId].Bounds,
                    q.Bounds, byId[q.PosterId].Bounds)) throw new ArgumentException("Swept group collision.");
            }
            scene = new(scene.Environment, byId.Values);
            if (PosterSpatialSolver.ValidateLayout(scene).Count != 0) throw new ArgumentException("Unsafe group endpoint.");
        }
        return scene;
    }

    internal static SpatialSnapshot Replace(SpatialSnapshot scene, SpatialPoster value) =>
        new(scene.Environment, scene.Posters.Select(p => p.PosterId == value.PosterId ? value : p));

    internal static SpatialContinuityState Rebase(SpatialSnapshot scene, SpatialContinuityState previous) =>
        new(scene.Environment, Array.AsReadOnly(scene.Posters.Select(p =>
        {
            var n = previous.Nodes.FirstOrDefault(n => n.Expected.PosterId == p.PosterId);
            return n is null ? Fresh(p) : n with { Expected = p };
        }).ToArray()));

    internal static SpatialContinuityState Normalize(SpatialSnapshot scene, SpatialContinuityState? previous, int compact) =>
        new(scene.Environment, Array.AsReadOnly(scene.Posters.Select(p =>
        {
            var n = previous?.Environment == scene.Environment && previous.SchemaVersion == 1
                ? previous.Nodes.FirstOrDefault(n => n.Expected == p) : null;
            return n is not null && n.UpInset >= 0 && n.UpInset <= Math.Max(0, p.Bounds.Height - compact) &&
                n.HomeY >= int.MinValue * 2L && n.HomeY <= int.MaxValue * 2L && !string.IsNullOrWhiteSpace(n.Cohort)
                ? n : Fresh(p);
        }).ToArray()));
    private static SpatialContinuityNode Fresh(SpatialPoster p) => new(p, p.Bounds.Y, 0, "anchor:" + p.PosterId + ":" + p.LayoutGeneration);
    private static SpatialContinuityState ChangeNode(SpatialContinuityState state, string id, Func<SpatialContinuityNode, SpatialContinuityNode> change) =>
        state with { Nodes = Array.AsReadOnly(state.Nodes.Select(n => n.Expected.PosterId == id ? change(n) : n).ToArray()) };
}
