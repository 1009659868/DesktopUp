use serde::Serialize;

pub const RUNTIME_LIFECYCLE_CONTRACT_VERSION: u32 = 1;

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum RuntimePhase {
    Starting,
    Running,
    Exiting,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum RuntimeMode {
    ActiveStatic,
    ActiveAnimated,
    Paused,
    Stopped,
}

impl RuntimeMode {
    pub fn as_str(self) -> &'static str {
        match self {
            Self::ActiveStatic => "activeStatic",
            Self::ActiveAnimated => "activeAnimated",
            Self::Paused => "paused",
            Self::Stopped => "stopped",
        }
    }
}

#[derive(Clone, Debug)]
pub struct RuntimeLifecycleInput {
    pub manually_paused: bool,
    pub automatically_paused: bool,
    pub poster_visible: bool,
    pub host_surface_ready: bool,
    pub exit_requested: bool,
    pub pause_reasons: Vec<String>,
    pub performance_mode: String,
    pub poster_fps: f64,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RuntimeLifecycleSnapshot {
    pub contract_version: u32,
    pub generation: u64,
    pub event_count: u64,
    pub phase: RuntimePhase,
    pub mode: RuntimeMode,
    pub manually_paused: bool,
    pub automatically_paused: bool,
    pub effective_paused: bool,
    pub poster_visible: bool,
    pub host_surface_ready: bool,
    pub pause_reasons: Vec<String>,
    pub last_trigger: String,
}

impl Default for RuntimeLifecycleSnapshot {
    fn default() -> Self {
        Self {
            contract_version: RUNTIME_LIFECYCLE_CONTRACT_VERSION,
            generation: 0,
            event_count: 0,
            phase: RuntimePhase::Starting,
            mode: RuntimeMode::Stopped,
            manually_paused: false,
            automatically_paused: false,
            effective_paused: false,
            poster_visible: true,
            host_surface_ready: false,
            pause_reasons: Vec::new(),
            last_trigger: "initialState".to_string(),
        }
    }
}

impl RuntimeLifecycleSnapshot {
    pub fn reduce(&mut self, input: RuntimeLifecycleInput, trigger: &str) -> bool {
        let effective_paused = input.manually_paused || input.automatically_paused;
        let phase = if self.phase == RuntimePhase::Exiting || input.exit_requested {
            RuntimePhase::Exiting
        } else if input.host_surface_ready {
            RuntimePhase::Running
        } else {
            RuntimePhase::Starting
        };
        let mode = if phase == RuntimePhase::Exiting
            || !input.poster_visible
            || !input.host_surface_ready
            || input.performance_mode == "stopped"
        {
            RuntimeMode::Stopped
        } else if effective_paused || input.performance_mode == "paused" {
            RuntimeMode::Paused
        } else {
            match input.performance_mode.as_str() {
                "activeAnimated" => RuntimeMode::ActiveAnimated,
                "activeStatic" => RuntimeMode::ActiveStatic,
                _ if input.poster_fps > 0.0 => RuntimeMode::ActiveAnimated,
                _ => RuntimeMode::ActiveStatic,
            }
        };

        let changed = self.phase != phase
            || self.mode != mode
            || self.manually_paused != input.manually_paused
            || self.automatically_paused != input.automatically_paused
            || self.effective_paused != effective_paused
            || self.poster_visible != input.poster_visible
            || self.host_surface_ready != input.host_surface_ready
            || self.pause_reasons != input.pause_reasons;

        self.event_count = self.event_count.saturating_add(1);
        if changed {
            self.generation = self.generation.saturating_add(1);
        }
        self.phase = phase;
        self.mode = mode;
        self.manually_paused = input.manually_paused;
        self.automatically_paused = input.automatically_paused;
        self.effective_paused = effective_paused;
        self.poster_visible = input.poster_visible;
        self.host_surface_ready = input.host_surface_ready;
        self.pause_reasons = input.pause_reasons;
        self.last_trigger = trigger.to_string();
        changed
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn input() -> RuntimeLifecycleInput {
        RuntimeLifecycleInput {
            manually_paused: false,
            automatically_paused: false,
            poster_visible: true,
            host_surface_ready: true,
            exit_requested: false,
            pause_reasons: Vec::new(),
            performance_mode: "auto".to_string(),
            poster_fps: 0.0,
        }
    }

    #[test]
    fn lifecycle_combination_table_is_deterministic() {
        let cases = [
            (false, false, true, true, RuntimeMode::ActiveStatic),
            (true, false, true, true, RuntimeMode::Paused),
            (false, true, true, true, RuntimeMode::Paused),
            (true, true, true, true, RuntimeMode::Paused),
            (false, false, false, true, RuntimeMode::Stopped),
            (false, false, true, false, RuntimeMode::Stopped),
        ];

        for (manual, automatic, visible, ready, expected) in cases {
            let mut snapshot = RuntimeLifecycleSnapshot::default();
            let mut next = input();
            next.manually_paused = manual;
            next.automatically_paused = automatic;
            next.poster_visible = visible;
            next.host_surface_ready = ready;
            snapshot.reduce(next, "combinationTable");
            assert_eq!(snapshot.mode, expected);
        }
    }

    #[test]
    fn duplicate_events_increment_event_count_without_changing_generation() {
        let mut snapshot = RuntimeLifecycleSnapshot::default();
        assert!(snapshot.reduce(input(), "hostSurfaceReady"));
        let generation = snapshot.generation;
        let event_count = snapshot.event_count;

        assert!(!snapshot.reduce(input(), "hostSurfaceReady"));
        assert_eq!(snapshot.generation, generation);
        assert_eq!(snapshot.event_count, event_count + 1);
    }

    #[test]
    fn pause_policy_overrides_requested_animation() {
        let mut snapshot = RuntimeLifecycleSnapshot::default();
        let mut next = input();
        next.manually_paused = true;
        next.performance_mode = "activeAnimated".to_string();
        next.poster_fps = 30.0;
        snapshot.reduce(next, "manualPause");

        assert_eq!(snapshot.mode, RuntimeMode::Paused);
        assert!(snapshot.effective_paused);
    }

    #[test]
    fn automatic_pause_reasons_are_preserved_in_stable_order() {
        let mut snapshot = RuntimeLifecycleSnapshot::default();
        let mut next = input();
        next.automatically_paused = true;
        next.pause_reasons = vec![
            "displayOff".to_string(),
            "sessionLocked".to_string(),
            "fullscreenForeground".to_string(),
        ];
        snapshot.reduce(next, "runtimeVisibility");

        assert_eq!(snapshot.mode, RuntimeMode::Paused);
        assert_eq!(
            snapshot.pause_reasons,
            ["displayOff", "sessionLocked", "fullscreenForeground"]
        );
    }

    #[test]
    fn visibility_and_surface_readiness_control_stopped_state() {
        let mut snapshot = RuntimeLifecycleSnapshot::default();
        snapshot.reduce(input(), "hostSurfaceReady");
        assert_eq!(snapshot.phase, RuntimePhase::Running);
        assert_eq!(snapshot.mode, RuntimeMode::ActiveStatic);

        let mut hidden = input();
        hidden.poster_visible = false;
        snapshot.reduce(hidden, "posterVisibility");
        assert_eq!(snapshot.mode, RuntimeMode::Stopped);

        snapshot.reduce(input(), "posterVisibility");
        assert_eq!(snapshot.mode, RuntimeMode::ActiveStatic);

        let mut loading = input();
        loading.host_surface_ready = false;
        snapshot.reduce(loading, "hostSurfaceStarted");
        assert_eq!(snapshot.phase, RuntimePhase::Starting);
        assert_eq!(snapshot.mode, RuntimeMode::Stopped);
    }

    #[test]
    fn exit_is_sticky_and_cannot_be_reversed_by_late_events() {
        let mut snapshot = RuntimeLifecycleSnapshot::default();
        let mut exiting = input();
        exiting.exit_requested = true;
        snapshot.reduce(exiting, "trayExit");
        let exit_generation = snapshot.generation;

        snapshot.reduce(input(), "lateVisibility");
        assert_eq!(snapshot.phase, RuntimePhase::Exiting);
        assert_eq!(snapshot.mode, RuntimeMode::Stopped);
        assert_eq!(snapshot.generation, exit_generation);
    }

    #[test]
    fn explicit_modes_apply_only_after_safety_policy() {
        let mut snapshot = RuntimeLifecycleSnapshot::default();
        let mut animated = input();
        animated.performance_mode = "activeAnimated".to_string();
        snapshot.reduce(animated, "performanceMode");
        assert_eq!(snapshot.mode, RuntimeMode::ActiveAnimated);

        let mut stopped = input();
        stopped.performance_mode = "stopped".to_string();
        snapshot.reduce(stopped, "performanceMode");
        assert_eq!(snapshot.mode, RuntimeMode::Stopped);
    }
}
