using System.Diagnostics;
using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

public partial class App
{
    private sealed record ContentRestartProof(long Revision, string Fingerprint, int Posters, string Scenario);

    // Opt-in isolated process gate. All mutations use the production editor / transaction /
    // R4 channel. This is deliberately NOT certification of native pointer or keyboard input.
    private void RunContentLayoutSmoke(string dataRoot)
    {
        bool restart = _startupIntent.Arguments.Contains("--content-layout-restart", StringComparer.Ordinal);
        string[] selectors = _startupIntent.Arguments.Where(a => a.StartsWith("--content-layout-case=", StringComparison.Ordinal)).ToArray();
        string scenario = selectors.Length == 1 ? selectors[0]["--content-layout-case=".Length..] : "";
        string[] allowed = ["three", "empty", "hundred", "thousand", "resource-one", "resource-three", "resource-six"];
        var timer = _dispatcher!.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(120);
        var lease = _ledger!.Acquire("fixture.content-layout", "production-layout-process-gate");
        long deadline = _autoClock.ElapsedMilliseconds + 70000;
        bool ticking = false, stopped = false, initialized = false, awaitingPosture = false, idleSampling = false;
        int checks = 0, count = 0, resourceSamples = 0, sampledControls = 0, postureReadySamples = 0;
        string target = ""; string? asynchronousError = null;
        string proofPath = Path.Combine(dataRoot, "content-layout-restart-proof.json");
        var cases = new List<string>(); var operations = new Queue<Action>();
        var resourceReadings = new List<object>();
        var process = Process.GetCurrentProcess();
        TimeSpan cpuStart = process.TotalProcessorTime; long measureStart = 0;
        ContentSurfaceProbe? guideBasis = null;
        ContentHeatProposal? staleHeat = null;
        ContentLayoutDocument? heatAuthor = null;
        string? heatGeometry = null;
        long heatRevision = 0;
        PosterPresentationParameters? themeBasis=null;
        string? themeGeometry=null;
        long themeHandle=0;
        bool suspensionProbePending=false;
        ContentSuspensionSmokeProbe? suspensionBasis=null;
        long suspensionRevision=0;
        void Require(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
        DesktopUpProjection Current() => _stateCoordinator!.Refresh();
        string Fingerprint(DesktopUpProjection state) => StateRequestFingerprint.Compute(new
        { Posters = state.Posters.Select(p => p.Poster).ToArray(), Settings = state.ControlCenter.Settings.Settings });
        string[] NativeResources() => _ledger.ActiveResources.Where(p => p.Contains("window.", StringComparison.Ordinal)).Order().ToArray();
        ContentSurfaceProbe Probe(string id)
        {
            var read = _spatialRuntime!.ReadContentSurfaceForSmoke(id);
            Require(read.CanvasMatches && read.InputMatches && read.Clipped, "Production Canvas/snapshot/published input disagree: " + JsonSerializer.Serialize(read));
            Require(read.Controls <= read.Items && read.Controls < 200, "Offscreen content materialized without bound.");
            return read;
        }
        void Stop()
        {
            if (stopped) return; stopped = true; timer.Stop(); timer.Tick -= Tick;
            lease.Dispose(); process.Dispose(); _stopAutoSmoke = null;
        }
        _stopAutoSmoke = Stop;
        void Finish(bool passed, string? error)
        {
            double elapsed = measureStart == 0 ? 0 : (_autoClock.ElapsedMilliseconds - measureStart) / 1000d;
            double cpu = elapsed <= 0 ? 0 : (process.TotalProcessorTime - cpuStart).TotalSeconds / elapsed * 100;
            if (passed && !restart)
            {
                var committed = Current();
                File.WriteAllText(proofPath, JsonSerializer.Serialize(new ContentRestartProof(committed.Revision, Fingerprint(committed), count, scenario)));
            }
            Stop();
            _log!.Write(passed ? "info" : "error", passed ? restart ? "content_layout.restart_passed" : "content_layout.smoke_passed" : "content_layout.smoke_failed",
                new { passed, error, scenario, restart, posters=count, checks, cases, resourceSamples,
                    resources=resourceReadings, sampledSeconds=elapsed, processCpuPercent=cpu,
                    nativeMouse=false, nativeKeyboard=false, nativeWheel=false, pixelVisualCertified=false });
            ExitApplication();
        }
        void Mutation(string name, Func<string?> action, bool expectChange=true)
        {
            var before = Current();
            var peers = before.Posters.Where(p => p.Poster.PosterId != target).Select(p => p.Poster).ToArray();
            var error = action(); Require(error is null, name + ": " + error);
            var after = Current();
            Require(after.Revision == before.Revision + (expectChange ? 1 : 0), name + " did not commit exactly once.");
            Require(peers.All(p => after.PostersById[p.PosterId].Poster == p), name + " changed another poster.");
            Probe(target); cases.Add(name);
        }
        void SyntheticInput(string scenario,bool expectChange)
        {
            Mutation("content-input-"+scenario,()=>
            {
                var result=_spatialRuntime!.RunContentInputForSmoke(target,scenario);
                Require(result.LatestPending && result.MotionReleased && !result.NativeMouse && !result.NativeKeyboard,
                    "Synthetic input case did not prove pending/latest and released timer ownership.");
                _log!.Write("info","content_layout.synthetic_input_case",result);
                return null;
            },expectChange);
        }
        void Posture(bool collapsed)
        {
            var before = Current(); var p = before.PostersById[target];
            string[] surfaces = NativeResources();
            long[] handles = before.Posters.Select(p => _spatialRuntime!.ReadContentSurfaceForSmoke(p.Poster.PosterId).Handle).ToArray();
            awaitingPosture = true;postureReadySamples=0;
            _spatialRuntime!.SetCollapsed(new("content-smoke-posture-" + Guid.NewGuid().ToString("N"), StateCommandSource.ControlCenter,
                PosterStateCommandKind.SetCollapsed, before.Revision, target, p.Poster with { IsCollapsed=collapsed })
                { ReadSet=StateReadSet.ForPoster(target,p.EntityVersion) }, result =>
                {
                    try
                    {
                        Require(result.Succeeded, "R4 posture failed: " + result.Error);
                        Require(result.CurrentRevision == before.Revision + 1, "R4 committed more than one revision.");
                        var end = _spatialRuntime.ReadContentSurfaceForSmoke(target);
                        Require(postureReadySamples>0,"R4 completed without observing a native-ready intermediate content surface: "+JsonSerializer.Serialize(end));
                        Require(!end.Transition && end.TransitionValid, "R4 did not restore content layout ownership: "+JsonSerializer.Serialize(end));
                        Require(surfaces.SequenceEqual(NativeResources()), "R4 allocated an additional native window.");
                        Require(handles.SequenceEqual(Current().Posters.Select(p => _spatialRuntime.ReadContentSurfaceForSmoke(p.Poster.PosterId).Handle)), "R4 replaced a content HWND.");
                        cases.Add(collapsed ? "r4-collapse-prepare-and-restoration" : "r4-expand-prepare-and-restoration");
                    }
                    catch (Exception error) { asynchronousError=error.ToString(); }
                    finally { awaitingPosture=false; }
                });
            if (awaitingPosture)
            {
                var prepared = _spatialRuntime.ReadContentSurfaceForSmoke(target);
                // Native allocation/layout readiness needs the production Rendering callbacks.
                // Do not require VerifyTransitionSurfaceReady synchronously before they run.
                Require(prepared.Transition && prepared.TransitionStructureReady && prepared.Clipped,
                    "R4 content preparation/clip structure was not active: "+JsonSerializer.Serialize(prepared));
                Require(surfaces.SequenceEqual(NativeResources()), "R4 preparation allocated an additional native window.");
            }
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            if (ticking || stopped) return; ticking=true;
            try
            {
                Require(_autoClock.ElapsedMilliseconds < deadline, "Content-layout gate timed out.");
                if (asynchronousError is not null) throw new InvalidOperationException(asynchronousError);
                if(suspensionProbePending)
                {
                    // Exactly one explicit lifecycle bridge bypasses the normal ReadyCount
                    // gate: the target's intentionally invalidated input must remain invalid.
                    var read=_spatialRuntime!.ReadContentSuspensionForSmoke(target);
                    Require(!read.InputReady && _inputRouter.ReadyCount==count-1 && read.HeatPending && read.ActiveHeatVersion!=99 &&
                        read.Frame==suspensionBasis!.Frame && read.GeometryFingerprint==suspensionBasis.GeometryFingerprint &&
                        Current().Revision==suspensionRevision,"Queued proposal ran while input was suspended: "+JsonSerializer.Serialize(read));
                    cases.Add("queued-proposal-suspended-input-remains-invalid");
                    Require(_spatialRuntime.RecoverContentForSmoke(target),"Shared production recovery refused actual host/topology.");
                    suspensionProbePending=false;
                    return;
                }
                if(awaitingPosture)
                {
                    var frame=_spatialRuntime!.ReadContentSurfaceForSmoke(target);
                    Require(frame.Transition && frame.TransitionStructureReady && frame.Clipped,
                        "R4 lost stable content layout/current clip during animation: "+JsonSerializer.Serialize(frame));
                    if(frame.TransitionValid)
                    {
                        Require(frame.CanvasMatches && frame.InputMatches,
                            "Native-ready R4 content snapshot/Canvas/input disagree: "+JsonSerializer.Serialize(frame));
                        if(postureReadySamples++==0)_log!.Write("info","content_layout.r4_surface_observed",new{frame,nativeMouse=false,nativeKeyboard=false});
                    }
                    return;
                }
                if (idleSampling)
                {
                    // Keep the actual idle interval free of diagnostic layout/DB queries.
                    // Controls were verified and cached once before entering this phase.
                    process.Refresh();
                    resourceReadings.Add(new {workingSet=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,handles=process.HandleCount,
                        ledgerResources=_ledger.ActiveResources.Count,posters=count,controls=sampledControls});
                    if(++resourceSamples>=12){cases.Add("twelve-idle-resource-samples-exit-ledger-checked-by-runner");Finish(true,null);}
                    return;
                }
                if (_controlCenterEnvironmentPending || _spatialRuntime is not { IsSpatialBusy:false } ||
                    !_stateCoordinator!.TryGetCurrent(out var current) || current is null ||
                    _inputRouter.ReadyCount != current.Posters.Count(p => p.IsEffectivelyVisible)) return;
                if (!initialized)
                {
                    Require(allowed.Contains(scenario,StringComparer.Ordinal), "Unknown isolated content scenario.");
                    count=current.Posters.Count;
                    Require(count == (scenario is "three" or "resource-three" ? 3 : scenario=="resource-six" ? 6 : 1), "Unexpected fixture poster count.");
                    Require(current.Posters.All(p => !p.AutoPosture.Enabled && p.IsEffectivelyVisible), "Fixture requires visible automatic-off posters.");
                    target=current.Posters[0].Poster.PosterId;
                    foreach(var poster in current.Posters) Probe(poster.Poster.PosterId);
                    cases.Add("actual-canvas-snapshot-input-and-bounded-controls");
                    if(scenario.StartsWith("resource-",StringComparison.Ordinal))
                    {
                        string equal=ContentLayoutPolicy.Serialize(ContentLayoutSamples.Equal("resource-workload"));
                        foreach(var p in current.Posters)
                        {
                            var doc=_spatialRuntime.ReadContentEditor(p.Poster.PosterId)!.Document;
                            Require(p.Poster.TemplateKey=="basic-box" && p.Poster.WidthDip==300 && p.Poster.ExpandedHeightDip==360 &&
                                !p.Poster.IsCollapsed && !p.Poster.IsLocked && p.Presentation.Parameters==PosterPresentationPolicy.Defaults("basic-box") &&
                                ContentLayoutPolicy.Serialize(ContentLayoutPolicy.Remap(doc,"resource-workload"))==equal,
                                "Resource workload differs from six equal cards / basic-box / 300x360 / expanded-unlocked baseline.");
                        }
                        cases.Add("comparable-six-equal-basic-box-300x360-workload");
                    }
                    if (restart)
                    {
                        var proof=JsonSerializer.Deserialize<ContentRestartProof>(File.ReadAllText(proofPath))!;
                        Require(proof.Scenario==scenario && proof.Posters==count && proof.Revision==current.Revision && proof.Fingerprint==Fingerprint(current), "Restart changed committed content state.");
                        cases.Add("same-root-restart-preserves-content-identities-and-settings");
                    }
                    else if (scenario=="three")
                    {
                        Require(_spatialRuntime.SetContentEditing(target,true) is null, "Cannot enter production content editing.");
                        operations.Enqueue(()=>Mutation("heat-fixture-equal",()=>_spatialRuntime.ApplyContentSample(target,"equal")));
                        operations.Enqueue(()=>
                        {
                            var doc=_spatialRuntime.ReadContentEditor(target)!.Document;
                            Mutation("heat-manual-size-override",()=>_spatialRuntime.ApplyContentEditor(target,new(ContentLayoutEditKind.Resize,doc.Placements[0].ItemId){ColumnSpan=6,RowSpan=4}));
                        });
                        operations.Enqueue(()=>
                        {
                            heatAuthor=_spatialRuntime.ReadContentEditor(target)!.Document;
                            heatRevision=Current().Revision;heatGeometry=Probe(target).GeometryFingerprint;
                            ContentHeatProposal Heat(long version,double scale)=>new(target,heatAuthor.ItemSetVersion,version,
                                heatAuthor.Placements.Select((p,i)=>new ContentHeatItem(p.ItemId,heatAuthor.Placements.Count-i,scale)).ToArray());
                            Require(_spatialRuntime.OfferContentHeatForSmoke(target,Heat(1,.5)),"First heat proposal was refused.");
                            staleHeat=Heat(2,1.5);
                            Require(_spatialRuntime.OfferContentHeatForSmoke(target,staleHeat),"Latest heat proposal was refused.");
                            var read=_spatialRuntime.ReadContentProposalForSmoke(target);
                            Require(read.HeatPending && read.ActiveHeatVersion is null && Current().Revision==heatRevision && Probe(target).GeometryFingerprint==heatGeometry,
                                "Heat reflowed/persisted while editing instead of retaining latest pending.");
                            cases.Add("heat-two-proposals-protected-latest-pending");
                        });
                        operations.Enqueue(()=>
                        {
                            Require(_spatialRuntime.SetContentEditing(target,false) is null,"Heat safe point could not leave editing.");
                        });
                        // Production proposal draining is queued; observe only on the next
                        // dispatcher-timer turn, never force DrainContentProposals from a test.
                        operations.Enqueue(()=>
                        {
                            var read=_spatialRuntime.ReadContentProposalForSmoke(target);
                            var authored=heatAuthor!.Placements[0];
                            var effective=read.EffectiveIntent.Placements.Single(p=>p.ItemId==authored.ItemId);
                            Require(read.ActiveHeatVersion==2 && !read.HeatPending && Current().Revision==heatRevision &&
                                effective.ManualSize && effective.ColumnSpan==authored.ColumnSpan && effective.RowSpan==authored.RowSpan,
                                "Safe point did not apply only latest heat or lost manual size override.");
                            Require(Probe(target).GeometryFingerprint!=heatGeometry,"Latest effective heat was not presented.");
                            Require(ContentLayoutPolicy.Serialize(read.AuthorIntent)==ContentLayoutPolicy.Serialize(heatAuthor!),"Heat overwrote authored intent.");
                            Require(!_spatialRuntime.OfferContentHeatForSmoke(target,staleHeat! with{Version=1}),"Older heat proposal was accepted.");
                            cases.Add("heat-safe-point-latest-only-override-preserved-no-persistence");
                            Require(_spatialRuntime.OfferContentHeatForSmoke(target,new(target,heatAuthor.ItemSetVersion,3,[])),"Neutral heat restoration was refused.");
                        });
                        operations.Enqueue(()=>
                        {
                            var neutral=_spatialRuntime.ReadContentProposalForSmoke(target);
                            Require(neutral.ActiveHeatVersion==3 && !neutral.HeatPending,"Neutral restoration did not reach its queued safe point.");
                            Require(_spatialRuntime.SetContentEditing(target,true) is null,"Cannot resume content editing.");
                            themeBasis=_spatialRuntime.ReadContentThemeForSmoke(target).Parameters;
                            var surface=Probe(target);themeGeometry=surface.GeometryFingerprint;themeHandle=surface.Handle;
                            var first=themeBasis with{Preferences=(themeBasis.Preferences??new()) with{Scale=.8}};
                            var latest=themeBasis with{Preferences=(themeBasis.Preferences??new()) with{Scale=1.2}};
                            Require(_spatialRuntime.OfferContentThemeForSmoke(target,new(1,first)) &&
                                _spatialRuntime.OfferContentThemeForSmoke(target,new(2,latest)),"Protected theme proposals were refused.");
                            var read=_spatialRuntime.ReadContentThemeForSmoke(target);
                            Require(read.Pending && read.Parameters==themeBasis && Probe(target).GeometryFingerprint==themeGeometry &&
                                Current().Revision==heatRevision,"Theme rebuilt protected content or persisted a projection.");
                            cases.Add("theme-metrics-protected-latest-pending");
                        });
                        operations.Enqueue(()=>Require(_spatialRuntime.SetContentEditing(target,false) is null,"Cannot leave theme protection."));
                        operations.Enqueue(()=>
                        {
                            var read=_spatialRuntime.ReadContentThemeForSmoke(target);var surface=Probe(target);
                            Require(!read.Pending && read.Parameters.Preferences?.Scale==1.2 && Current().Revision==heatRevision &&
                                surface.GeometryFingerprint!=themeGeometry && surface.Handle==themeHandle,"Theme safe point did not apply latest metrics on the same surface.");
                            var invalid=read.Parameters with{Theme=read.Parameters.Theme with{Background="not-a-color"}};
                            Require(!_spatialRuntime.OfferContentThemeForSmoke(target,new(3,invalid)) &&
                                _spatialRuntime.ReadContentThemeForSmoke(target).Parameters==read.Parameters,"Invalid theme replaced last-good presentation.");
                            cases.Add("theme-safe-point-latest-last-good-no-native-surface");
                            Require(_spatialRuntime.OfferContentThemeForSmoke(target,new(4,themeBasis!)),"Theme basis restore refused.");
                        });
                        operations.Enqueue(()=>
                        {
                            Require(_spatialRuntime.ReadContentThemeForSmoke(target).Parameters==themeBasis && Current().Revision==heatRevision,
                                "Queued theme basis restore changed authored state or did not complete.");
                        });
                        operations.Enqueue(()=>
                        {
                            // Still in Using mode after the theme safe point. Offer then suspend
                            // in the SAME UI turn, before the queued drain can execute.
                            suspensionBasis=_spatialRuntime.ReadContentSuspensionForSmoke(target);
                            suspensionRevision=Current().Revision;
                            var doc=_spatialRuntime.ReadContentEditor(target)!.Document;
                            Require(_spatialRuntime.OfferContentHeatForSmoke(target,new(target,doc.ItemSetVersion,99,[])),"Pre-suspend neutral proposal refused.");
                            _spatialRuntime.SuspendContentInputForSmoke(target);
                            suspensionProbePending=true;
                        });
                        operations.Enqueue(()=>
                        {
                            var read=_spatialRuntime.ReadContentSuspensionForSmoke(target);
                            Require(read.InputReady && !read.HeatPending && read.ActiveHeatVersion==99 && Current().Revision==suspensionRevision,
                                "Shared recovery did not resume queued proposal safely: "+JsonSerializer.Serialize(read));
                            Probe(target);cases.Add("shared-recovery-resumes-proposal-on-safe-point");
                            Require(_spatialRuntime.SetContentEditing(target,true) is null,"Cannot resume editing after lifecycle recovery.");
                        });
                        foreach(string sample in new[]{"illustration","covers","landscape"})
                        {
                            string captured=sample;
                            operations.Enqueue(()=>
                            {
                                Mutation("reference-"+captured,()=>_spatialRuntime.ApplyContentSample(target,captured));
                                if(captured=="illustration")
                                {
                                    long revision=Current().Revision;
                                    Require(!_spatialRuntime.OfferContentHeatForSmoke(target,staleHeat! with{Version=4}) && Current().Revision==revision,
                                        "Heat proposal from a replaced item set was accepted.");
                                    cases.Add("heat-old-itemset-rejected-after-membership-replacement");
                                }
                            });
                        }
                        operations.Enqueue(()=>
                        {
                            var read=Probe(target); Require(read.ExtentWidth>read.ViewWidth,"Narrow manual reference did not expose horizontal extent.");
                            _spatialRuntime.ScrollContentForSmoke(target,double.MaxValue,0);
                            read=Probe(target); Require(read.ScrollX>0,"Horizontal offset was not clamped to the reachable right extent.");
                            _spatialRuntime.ScrollContentForSmoke(target,0,0); guideBasis=Probe(target);
                            cases.Add("horizontal-scroll-offset-nonnative-shift-wheel-equivalent");
                        });
                        foreach(var flags in new[]{(false,false),(true,false),(true,true),(false,true)})
                        {
                            var captured=flags;
                            operations.Enqueue(()=>
                            {
                                Mutation($"guides-{captured.Item1}-{captured.Item2}",()=>_spatialRuntime.ApplyContentEditor(target,new(ContentLayoutEditKind.Guides){ShowBaselineGrid=captured.Item1,ShowSpanLabels=captured.Item2}));
                                var read=Probe(target); Require(read.GeometryFingerprint==guideBasis!.GeometryFingerprint && read.HitFingerprint==guideBasis.HitFingerprint,"Guides changed geometry or item hit regions.");
                            });
                        }
                        operations.Enqueue(()=>Mutation("equal-reference",()=>_spatialRuntime.ApplyContentSample(target,"equal")));
                        operations.Enqueue(()=>Mutation("mode-manual",()=>_spatialRuntime.ApplyContentEditor(target,new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Manual})));
                        operations.Enqueue(()=>SyntheticInput("latest-release",true));
                        operations.Enqueue(()=>SyntheticInput("reverse-origin",false));
                        operations.Enqueue(()=>SyntheticInput("cancel-draft",false));
                        operations.Enqueue(()=>
                        {
                            var d=_spatialRuntime.ReadContentEditor(target)!.Document;
                            Mutation("resize-single-revision-target-only",()=>_spatialRuntime.ApplyContentEditor(target,new(ContentLayoutEditKind.Resize,d.Placements[^1].ItemId){ColumnSpan=3,RowSpan=3}));
                        });
                        operations.Enqueue(()=>
                        {
                            var d=_spatialRuntime.ReadContentEditor(target)!.Document;
                            Require(_spatialRuntime.SelectContentEditor(target,d.Placements[^1].ItemId) is null,"Selection failed.");
                            Mutation("move-single-revision-target-only",()=>_spatialRuntime.ApplyContentEditor(target,new(ContentLayoutEditKind.Move,d.Placements[^1].ItemId){Column=0,Row=40}));
                        });
                        operations.Enqueue(()=>Mutation("undo-after-commit",()=>_spatialRuntime.UndoContentEditor(target,false)));
                        operations.Enqueue(()=>Mutation("redo-after-commit",()=>_spatialRuntime.UndoContentEditor(target,true)));
                        operations.Enqueue(()=>SyntheticInput("edge-release",true));
                        operations.Enqueue(()=>
                        {
                            var before=Current(); var p=before.PostersById[target]; var doc=_spatialRuntime.ReadContentEditor(target)!.Document;
                            Mutation("fixed-card",()=>_spatialRuntime.ApplyContentEditor(target,new(ContentLayoutEditKind.Fixed,doc.Placements[^1].ItemId){Enabled=true}));
                            var stale=_stateCommands!.Execute(new ContentLayoutCommand("content-smoke-stale",StateCommandSource.Poster,before.Revision,target,
                                StateReadSet.ForPoster(target,p.EntityVersion),doc.DocumentVersion,doc.ItemSetVersion,doc with{DocumentVersion=doc.DocumentVersion+1,ShowSpanLabels=!doc.ShowSpanLabels}));
                            Require(!stale.Succeeded && stale.Status==StateCommandStatus.Conflict && Current().Revision==before.Revision+1,"Stale aggregate CAS mutated state.");
                            cases.Add("complete-aggregate-cas-stale-rejection");
                        });
                        operations.Enqueue(()=>Mutation("manual-exit-to-anchors",()=>_spatialRuntime.ApplyContentEditor(target,new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Anchored,ManualExit=ManualLayoutExit.ConvertToAnchors})));
                        operations.Enqueue(()=>{Require(_spatialRuntime.SetContentEditing(target,false) is null,"Cannot exit content editing."); Posture(true);});
                        operations.Enqueue(()=>Posture(false));
                    }
                    else if (scenario is "hundred" or "thousand")
                    {
                        int expected=scenario=="thousand"?1000:100;
                        Require(Probe(target).Items==expected,"Stress fixture count mismatch.");
                        operations.Enqueue(()=>
                        {
                            _spatialRuntime.ScrollContentForSmoke(target,0,33);
                            var read=Probe(target); Require(read.PartialClip,"Scrolling did not retain a partially clipped card.");
                            cases.Add("partial-card-clip-and-hit-agree");
                        });
                        operations.Enqueue(()=>
                        {
                            _spatialRuntime.ScrollContentForSmoke(target,0,double.MaxValue);
                            var read=Probe(target); Require(read.LastItemReachable && read.Controls<100,"Final item not reachable or 1000 controls were retained.");
                            cases.Add("scroll-bottom-reaches-last-item-with-bounded-controls");
                        });
                    }
                    else if(scenario=="empty") {Require(Probe(target).Items==0 && Probe(target).HitItems==0,"Empty layout kept item hits.");cases.Add("zero-items-no-fabricated-cards-or-hitboxes");}
                    initialized=true; return;
                }
                if(operations.TryDequeue(out var operation)){operation();return;}
                sampledControls=current.Posters.Sum(p=>Probe(p.Poster.PosterId).Controls);
                measureStart=_autoClock.ElapsedMilliseconds;cpuStart=process.TotalProcessorTime;
                idleSampling=true;timer.Interval=TimeSpan.FromSeconds(1);
            }
            catch(Exception error){Finish(false,error.ToString());}
            finally{ticking=false;}
        }
        timer.Tick+=Tick;timer.Start();
    }
}
