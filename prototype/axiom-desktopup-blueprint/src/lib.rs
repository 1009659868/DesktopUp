//! Compile-time DesktopUp V2 program blueprint fixture.
//!
//! This crate proves program topology and authority rules only. Physical
//! execution mechanisms, rendering, desktop hosting, persistence and process boundaries
//! remain adapter decisions.

#![forbid(unsafe_code)]

use axiom::prelude_all::{Blueprint, Chain, Diamond, PortCell, Slot, Static, drive};
#[cfg(test)]
use axiom::prelude_all::{Wire, assert_conforms, assert_wiring, blueprint_is_zero_sized};

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, PartialOrd, Ord)]
pub struct Revision(pub u64);

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct RequestId(pub u64);

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum PresentationMode {
    Expanded,
    Collapsed,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum CommandSource {
    Poster,
    ControlCenter,
    Tray,
    AiChangePlan,
    Lifecycle,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct PosterState {
    pub x: i32,
    pub y: i32,
    pub width: u32,
    pub height: u32,
    pub locked: bool,
    pub presentation: PresentationMode,
    pub theme_id: String,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct AcceptedSnapshot {
    pub revision: Revision,
    pub poster: PosterState,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum Mutation {
    SetPosition { x: i32, y: i32 },
    SetSize { width: u32, height: u32 },
    SetLocked(bool),
    SetPresentation(PresentationMode),
    SetTheme(String),
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum Command {
    Mutate(Mutation),
    OpenControlCenter,
    RequestExit,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct CommandEnvelope {
    pub request_id: RequestId,
    pub expected_revision: Option<Revision>,
    pub source: CommandSource,
    pub command: Command,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum StoreRequest {
    Load,
    Commit {
        request_id: RequestId,
        expected_revision: Revision,
        proposed: AcceptedSnapshot,
    },
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum StoreError {
    NotFound,
    Conflict {
        expected: Revision,
        actual: Revision,
    },
    Corrupt(String),
    Io(String),
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum StoreEvent {
    Loaded(AcceptedSnapshot),
    LoadFailed(StoreError),
    CommitAccepted {
        request_id: RequestId,
        snapshot: AcceptedSnapshot,
    },
    CommitRejected {
        request_id: RequestId,
        error: StoreError,
    },
}

#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct HostRuntimeObservation {
    pub attached: bool,
    pub visible: bool,
    pub surface_epoch: u64,
}

#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct RendererRuntimeObservation {
    pub ready: bool,
    pub last_frame_revision: Option<Revision>,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum RuntimeInput {
    Command(CommandEnvelope),
    Store(StoreEvent),
    Host(HostRuntimeObservation),
    Renderer(RendererRuntimeObservation),
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum EffectIntent {
    DispatchStore(StoreRequest),
    OpenControlCenter,
    ExitApplication,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum CoreError {
    StateUnavailable,
    ExpectedRevisionRequired,
    StaleCommand {
        expected: Revision,
        actual: Revision,
    },
    NonMonotonicStoreResult {
        current: Revision,
        received: Revision,
    },
    Store(StoreError),
}

#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct CoreState {
    /// The last snapshot accepted from StateStore. None means "not loaded";
    /// default construction must never invent business state.
    pub accepted: Option<AcceptedSnapshot>,
    pub host: HostRuntimeObservation,
    pub renderer: RendererRuntimeObservation,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct CoreEmission {
    pub accepted: Option<AcceptedSnapshot>,
    pub accepted_changed: bool,
    pub host: HostRuntimeObservation,
    pub renderer: RendererRuntimeObservation,
    pub effects: Vec<EffectIntent>,
    pub errors: Vec<CoreError>,
}

impl CoreEmission {
    fn from_state(state: &CoreState) -> Self {
        Self {
            accepted: state.accepted.clone(),
            accepted_changed: false,
            host: state.host.clone(),
            renderer: state.renderer.clone(),
            effects: Vec::new(),
            errors: Vec::new(),
        }
    }
}

pub struct RuntimeIngress;

impl PortCell for RuntimeIngress {
    type In = RuntimeInput;
    type Out = RuntimeInput;
    type State = ();

    fn step(_: &mut Self::State, input: Self::In) -> Self::Out {
        input
    }
}

pub struct ApplicationReducer;

impl ApplicationReducer {
    fn apply_mutation(snapshot: &AcceptedSnapshot, mutation: Mutation) -> AcceptedSnapshot {
        let mut proposed = snapshot.clone();
        proposed.revision = Revision(snapshot.revision.0 + 1);

        match mutation {
            Mutation::SetPosition { x, y } => {
                proposed.poster.x = x;
                proposed.poster.y = y;
            }
            Mutation::SetSize { width, height } => {
                proposed.poster.width = width;
                proposed.poster.height = height;
            }
            Mutation::SetLocked(locked) => proposed.poster.locked = locked,
            Mutation::SetPresentation(presentation) => {
                proposed.poster.presentation = presentation;
            }
            Mutation::SetTheme(theme_id) => proposed.poster.theme_id = theme_id,
        }

        proposed
    }

    fn accept_store_snapshot(state: &mut CoreState, snapshot: AcceptedSnapshot) -> CoreEmission {
        let mut emission = CoreEmission::from_state(state);

        if let Some(current) = state.accepted.as_ref() {
            if snapshot.revision <= current.revision {
                emission.errors.push(CoreError::NonMonotonicStoreResult {
                    current: current.revision,
                    received: snapshot.revision,
                });
                return emission;
            }
        }

        state.accepted = Some(snapshot);
        emission = CoreEmission::from_state(state);
        emission.accepted_changed = true;
        emission
    }

    fn handle_command(state: &CoreState, envelope: CommandEnvelope) -> CoreEmission {
        let mut emission = CoreEmission::from_state(state);

        match envelope.command {
            Command::OpenControlCenter => {
                emission.effects.push(EffectIntent::OpenControlCenter);
            }
            Command::RequestExit => {
                emission.effects.push(EffectIntent::ExitApplication);
            }
            Command::Mutate(mutation) => {
                let Some(current) = state.accepted.as_ref() else {
                    emission.errors.push(CoreError::StateUnavailable);
                    return emission;
                };
                let Some(expected) = envelope.expected_revision else {
                    emission.errors.push(CoreError::ExpectedRevisionRequired);
                    return emission;
                };
                if expected != current.revision {
                    emission.errors.push(CoreError::StaleCommand {
                        expected,
                        actual: current.revision,
                    });
                    return emission;
                }

                let proposed = Self::apply_mutation(current, mutation);
                emission
                    .effects
                    .push(EffectIntent::DispatchStore(StoreRequest::Commit {
                        request_id: envelope.request_id,
                        expected_revision: current.revision,
                        proposed,
                    }));
            }
        }

        emission
    }
}

impl PortCell for ApplicationReducer {
    type In = RuntimeInput;
    type Out = CoreEmission;
    type State = CoreState;

    fn step(state: &mut Self::State, input: Self::In) -> Self::Out {
        match input {
            RuntimeInput::Command(command) => Self::handle_command(state, command),
            RuntimeInput::Store(StoreEvent::Loaded(snapshot)) => {
                Self::accept_store_snapshot(state, snapshot)
            }
            RuntimeInput::Store(StoreEvent::CommitAccepted { snapshot, .. }) => {
                Self::accept_store_snapshot(state, snapshot)
            }
            RuntimeInput::Store(StoreEvent::LoadFailed(error))
            | RuntimeInput::Store(StoreEvent::CommitRejected { error, .. }) => {
                let mut emission = CoreEmission::from_state(state);
                emission.errors.push(CoreError::Store(error));
                emission
            }
            RuntimeInput::Host(observation) => {
                state.host = observation;
                CoreEmission::from_state(state)
            }
            RuntimeInput::Renderer(observation) => {
                state.renderer = observation;
                CoreEmission::from_state(state)
            }
        }
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct PosterProjection {
    pub revision: Revision,
    pub state: PosterState,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct ControlCenterProjection {
    pub revision: Revision,
    pub state: PosterState,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct TrayProjection {
    pub revision: Revision,
    pub locked: bool,
    pub presentation: PresentationMode,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DiagnosticProjection {
    pub revision: Revision,
    pub theme_id: String,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct AuxiliaryProjection {
    pub control_center: ControlCenterProjection,
    pub tray: TrayProjection,
    pub diagnostics: DiagnosticProjection,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct ProjectionBundle {
    pub poster: PosterProjection,
    pub control_center: ControlCenterProjection,
    pub tray: TrayProjection,
    pub diagnostics: DiagnosticProjection,
}

pub struct SnapshotIdentity;

impl PortCell for SnapshotIdentity {
    type In = AcceptedSnapshot;
    type Out = AcceptedSnapshot;
    type State = ();

    fn step(_: &mut Self::State, input: Self::In) -> Self::Out {
        input
    }
}

pub struct PosterProjector;

impl PortCell for PosterProjector {
    type In = AcceptedSnapshot;
    type Out = PosterProjection;
    type State = ();

    fn step(_: &mut Self::State, input: Self::In) -> Self::Out {
        PosterProjection {
            revision: input.revision,
            state: input.poster,
        }
    }
}

pub struct AuxiliaryProjector;

impl PortCell for AuxiliaryProjector {
    type In = AcceptedSnapshot;
    type Out = AuxiliaryProjection;
    type State = ();

    fn step(_: &mut Self::State, input: Self::In) -> Self::Out {
        AuxiliaryProjection {
            control_center: ControlCenterProjection {
                revision: input.revision,
                state: input.poster.clone(),
            },
            tray: TrayProjection {
                revision: input.revision,
                locked: input.poster.locked,
                presentation: input.poster.presentation,
            },
            diagnostics: DiagnosticProjection {
                revision: input.revision,
                theme_id: input.poster.theme_id,
            },
        }
    }
}

pub struct ProjectionJoin;

impl PortCell for ProjectionJoin {
    type In = (PosterProjection, AuxiliaryProjection);
    type Out = ProjectionBundle;
    type State = ();

    fn step(_: &mut Self::State, (poster, auxiliary): Self::In) -> Self::Out {
        ProjectionBundle {
            poster,
            control_center: auxiliary.control_center,
            tray: auxiliary.tray,
            diagnostics: auxiliary.diagnostics,
        }
    }
}

pub type ProjectionGraph =
    Diamond<SnapshotIdentity, PosterProjector, AuxiliaryProjector, ProjectionJoin>;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct ProgramEmission {
    pub accepted: Option<AcceptedSnapshot>,
    pub projections: Option<ProjectionBundle>,
    pub host: HostRuntimeObservation,
    pub renderer: RendererRuntimeObservation,
    pub effects: Vec<EffectIntent>,
    pub errors: Vec<CoreError>,
}

pub struct EmissionProjector;

impl PortCell for EmissionProjector {
    type In = CoreEmission;
    type Out = ProgramEmission;
    type State = <ProjectionGraph as PortCell>::State;

    fn step(state: &mut Self::State, input: Self::In) -> Self::Out {
        let projections = if input.accepted_changed {
            input
                .accepted
                .clone()
                .map(|snapshot| drive::<ProjectionGraph>(state, snapshot))
        } else {
            None
        };

        ProgramEmission {
            accepted: input.accepted,
            projections,
            host: input.host,
            renderer: input.renderer,
            effects: input.effects,
            errors: input.errors,
        }
    }
}

pub type DesktopUpProgramGraph =
    Chain<RuntimeIngress, Chain<ApplicationReducer, EmissionProjector>>;
pub type DesktopUpStaticGraph = Static<DesktopUpProgramGraph>;

pub const DESKTOPUP_PROGRAM_BLUEPRINT: Blueprint<DesktopUpProgramGraph> = Blueprint::define();

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DispatchAck;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Poll;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DesktopHostCommand;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DesktopHostEvent;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct RenderCommand;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct RenderEvent;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct SurfaceCommand;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct SurfaceEvent;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct ThemeRequest;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct ThemeResult;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct AiRequest;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct AiChangePlan;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DiagnosticEvent;

pub type StateStoreDispatchSlot = Slot<StoreRequest, DispatchAck>;
pub type StateStoreEventSlot = Slot<Poll, Vec<StoreEvent>>;
pub type DesktopHostDispatchSlot = Slot<DesktopHostCommand, DispatchAck>;
pub type DesktopHostEventSlot = Slot<Poll, Vec<DesktopHostEvent>>;
pub type RendererDispatchSlot = Slot<RenderCommand, DispatchAck>;
pub type RendererEventSlot = Slot<Poll, Vec<RenderEvent>>;
pub type ControlCenterDispatchSlot = Slot<SurfaceCommand, DispatchAck>;
pub type ControlCenterEventSlot = Slot<Poll, Vec<SurfaceEvent>>;
pub type TrayDispatchSlot = Slot<SurfaceCommand, DispatchAck>;
pub type TrayEventSlot = Slot<Poll, Vec<SurfaceEvent>>;
pub type ThemeSlot = Slot<ThemeRequest, ThemeResult>;
pub type AiSlot = Slot<AiRequest, AiChangePlan>;
pub type DiagnosticsSlot = Slot<DiagnosticEvent, DispatchAck>;

#[cfg(test)]
mod tests {
    use super::*;

    fn snapshot(revision: u64) -> AcceptedSnapshot {
        AcceptedSnapshot {
            revision: Revision(revision),
            poster: PosterState {
                x: 100,
                y: 120,
                width: 480,
                height: 720,
                locked: false,
                presentation: PresentationMode::Expanded,
                theme_id: "nocturne".into(),
            },
        }
    }

    fn load(state: &mut CoreState, revision: u64) {
        let emission = ApplicationReducer::step(
            state,
            RuntimeInput::Store(StoreEvent::Loaded(snapshot(revision))),
        );
        assert!(emission.accepted_changed);
    }

    #[test]
    fn blueprint_and_static_graph_are_zero_sized_and_wired() {
        assert!(blueprint_is_zero_sized::<DesktopUpProgramGraph>());
        assert_eq!(core::mem::size_of_val(&DESKTOPUP_PROGRAM_BLUEPRINT), 0);
        assert_eq!(core::mem::size_of::<DesktopUpStaticGraph>(), 0);
        assert_wiring::<RuntimeIngress, ApplicationReducer>();
        assert_wiring::<ApplicationReducer, EmissionProjector>();
        let _: Option<Wire<RuntimeIngress, ApplicationReducer>> = None;
    }

    #[test]
    fn default_state_does_not_fabricate_business_data() {
        assert_eq!(CoreState::default().accepted, None);
    }

    #[test]
    fn mutation_waits_for_authoritative_store_commit() {
        let mut state = CoreState::default();
        load(&mut state, 7);

        let emission = ApplicationReducer::step(
            &mut state,
            RuntimeInput::Command(CommandEnvelope {
                request_id: RequestId(12),
                expected_revision: Some(Revision(7)),
                source: CommandSource::Poster,
                command: Command::Mutate(Mutation::SetLocked(true)),
            }),
        );

        assert_eq!(state.accepted.as_ref().unwrap().revision, Revision(7));
        assert!(!state.accepted.as_ref().unwrap().poster.locked);
        assert!(!emission.accepted_changed);
        assert!(matches!(
            &emission.effects[..],
            [EffectIntent::DispatchStore(StoreRequest::Commit {
                expected_revision: Revision(7),
                proposed,
                ..
            })] if proposed.revision == Revision(8) && proposed.poster.locked
        ));

        let accepted = ApplicationReducer::step(
            &mut state,
            RuntimeInput::Store(StoreEvent::CommitAccepted {
                request_id: RequestId(12),
                snapshot: {
                    let mut committed = snapshot(8);
                    committed.poster.locked = true;
                    committed
                },
            }),
        );

        assert!(accepted.accepted_changed);
        assert_eq!(state.accepted.as_ref().unwrap().revision, Revision(8));
        assert!(state.accepted.as_ref().unwrap().poster.locked);
    }

    #[test]
    fn stale_commands_and_non_monotonic_store_results_are_rejected() {
        let mut state = CoreState::default();
        load(&mut state, 9);

        let stale = ApplicationReducer::step(
            &mut state,
            RuntimeInput::Command(CommandEnvelope {
                request_id: RequestId(13),
                expected_revision: Some(Revision(8)),
                source: CommandSource::ControlCenter,
                command: Command::Mutate(Mutation::SetPosition { x: 1, y: 2 }),
            }),
        );
        assert!(matches!(
            stale.errors.as_slice(),
            [CoreError::StaleCommand {
                expected: Revision(8),
                actual: Revision(9)
            }]
        ));
        assert!(stale.effects.is_empty());

        let old_result = ApplicationReducer::step(
            &mut state,
            RuntimeInput::Store(StoreEvent::CommitAccepted {
                request_id: RequestId(13),
                snapshot: snapshot(9),
            }),
        );
        assert!(matches!(
            old_result.errors.as_slice(),
            [CoreError::NonMonotonicStoreResult {
                current: Revision(9),
                received: Revision(9)
            }]
        ));
    }

    #[test]
    fn all_surface_projections_share_one_accepted_revision() {
        let mut state = <ProjectionGraph as PortCell>::State::default();
        let bundle = drive::<ProjectionGraph>(&mut state, snapshot(42));

        assert_eq!(bundle.poster.revision, Revision(42));
        assert_eq!(bundle.control_center.revision, Revision(42));
        assert_eq!(bundle.tray.revision, Revision(42));
        assert_eq!(bundle.diagnostics.revision, Revision(42));
    }

    struct MockStoreDispatch;

    impl PortCell for MockStoreDispatch {
        type In = StoreRequest;
        type Out = DispatchAck;
        type State = ();

        fn step(_: &mut Self::State, _: Self::In) -> Self::Out {
            DispatchAck
        }
    }

    struct MockStoreEvents;

    impl PortCell for MockStoreEvents {
        type In = Poll;
        type Out = Vec<StoreEvent>;
        type State = ();

        fn step(_: &mut Self::State, _: Self::In) -> Self::Out {
            Vec::new()
        }
    }

    #[test]
    fn asynchronous_store_boundary_uses_two_typed_slots() {
        assert_conforms::<StateStoreDispatchSlot, MockStoreDispatch>();
        assert_conforms::<StateStoreEventSlot, MockStoreEvents>();
    }
}
