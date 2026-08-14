use std::sync::Mutex;

use serde::Serialize;

pub const CONTROL_CENTER_LIFECYCLE_CONTRACT_VERSION: u32 = 1;

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum ControlCenterSurfaceState {
    Absent,
    Hidden,
    Visible,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ControlCenterLifecycleSnapshot {
    pub contract_version: u32,
    pub generation: u64,
    pub event_count: u64,
    pub surface_state: ControlCenterSurfaceState,
    pub ready: bool,
    pub pending_command_count: usize,
    pub last_trigger: String,
}

impl Default for ControlCenterLifecycleSnapshot {
    fn default() -> Self {
        Self {
            contract_version: CONTROL_CENTER_LIFECYCLE_CONTRACT_VERSION,
            generation: 0,
            event_count: 0,
            surface_state: ControlCenterSurfaceState::Absent,
            ready: false,
            pending_command_count: 0,
            last_trigger: "initialState".to_string(),
        }
    }
}

#[derive(Default)]
struct ControlCenterLifecycleState {
    snapshot: ControlCenterLifecycleSnapshot,
    pending_commands: Vec<String>,
}

#[derive(Default)]
pub struct ControlCenterLifecycle(Mutex<ControlCenterLifecycleState>);

impl ControlCenterLifecycle {
    pub fn snapshot(&self) -> Result<ControlCenterLifecycleSnapshot, String> {
        self.0
            .lock()
            .map(|state| state.snapshot.clone())
            .map_err(|error| error.to_string())
    }

    pub fn page_load_started(&self) -> Result<ControlCenterLifecycleSnapshot, String> {
        self.transition("pageLoadStarted", None, Some(false))
    }

    pub fn window_created(&self) -> Result<ControlCenterLifecycleSnapshot, String> {
        self.transition(
            "windowCreated",
            Some(ControlCenterSurfaceState::Hidden),
            Some(false),
        )
    }

    pub fn window_shown(&self) -> Result<ControlCenterLifecycleSnapshot, String> {
        self.transition(
            "windowShown",
            Some(ControlCenterSurfaceState::Visible),
            None,
        )
    }

    pub fn window_hidden(&self) -> Result<ControlCenterLifecycleSnapshot, String> {
        self.transition(
            "windowHidden",
            Some(ControlCenterSurfaceState::Hidden),
            None,
        )
    }

    pub fn window_destroyed(&self) -> Result<ControlCenterLifecycleSnapshot, String> {
        self.transition(
            "windowDestroyed",
            Some(ControlCenterSurfaceState::Absent),
            Some(false),
        )
    }

    pub fn queue_or_take_ready(&self, payload: String) -> Result<Option<String>, String> {
        let mut state = self.0.lock().map_err(|error| error.to_string())?;
        state.snapshot.event_count = state.snapshot.event_count.saturating_add(1);
        state.snapshot.last_trigger = if state.snapshot.ready {
            "commandDispatched"
        } else {
            "commandQueued"
        }
        .to_string();
        if state.snapshot.ready {
            return Ok(Some(payload));
        }
        state.pending_commands.push(payload);
        state.snapshot.pending_command_count = state.pending_commands.len();
        state.snapshot.generation = state.snapshot.generation.saturating_add(1);
        Ok(None)
    }

    pub fn mark_ready(&self) -> Result<Vec<String>, String> {
        let mut state = self.0.lock().map_err(|error| error.to_string())?;
        if state.snapshot.surface_state == ControlCenterSurfaceState::Absent {
            return Err("control center cannot become ready while absent".to_string());
        }
        state.snapshot.event_count = state.snapshot.event_count.saturating_add(1);
        let changed = !state.snapshot.ready || !state.pending_commands.is_empty();
        state.snapshot.ready = true;
        state.snapshot.last_trigger = "frontendReady".to_string();
        let pending = std::mem::take(&mut state.pending_commands);
        state.snapshot.pending_command_count = 0;
        if changed {
            state.snapshot.generation = state.snapshot.generation.saturating_add(1);
        }
        Ok(pending)
    }

    fn transition(
        &self,
        trigger: &str,
        surface_state: Option<ControlCenterSurfaceState>,
        ready: Option<bool>,
    ) -> Result<ControlCenterLifecycleSnapshot, String> {
        let mut state = self.0.lock().map_err(|error| error.to_string())?;
        let next_surface = surface_state.unwrap_or(state.snapshot.surface_state);
        let next_ready = ready.unwrap_or(state.snapshot.ready);
        let changed =
            next_surface != state.snapshot.surface_state || next_ready != state.snapshot.ready;
        state.snapshot.event_count = state.snapshot.event_count.saturating_add(1);
        if changed {
            state.snapshot.generation = state.snapshot.generation.saturating_add(1);
        }
        state.snapshot.surface_state = next_surface;
        state.snapshot.ready = next_ready;
        state.snapshot.pending_command_count = state.pending_commands.len();
        state.snapshot.last_trigger = trigger.to_string();
        Ok(state.snapshot.clone())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn ready_handshake_drains_pre_ready_commands_once_in_order() {
        let lifecycle = ControlCenterLifecycle::default();
        lifecycle.window_created().unwrap();
        lifecycle.window_shown().unwrap();
        assert_eq!(
            lifecycle
                .queue_or_take_ready("open-settings".into())
                .unwrap(),
            None
        );
        assert_eq!(
            lifecycle.queue_or_take_ready("open-agent".into()).unwrap(),
            None
        );
        assert_eq!(
            lifecycle.mark_ready().unwrap(),
            ["open-settings".to_string(), "open-agent".to_string()]
        );
        assert!(lifecycle.mark_ready().unwrap().is_empty());
        assert_eq!(
            lifecycle
                .queue_or_take_ready("open-settings".into())
                .unwrap(),
            Some("open-settings".to_string())
        );
    }

    #[test]
    fn reload_and_destroy_reset_readiness_without_losing_queued_commands() {
        let lifecycle = ControlCenterLifecycle::default();
        lifecycle.window_created().unwrap();
        lifecycle.mark_ready().unwrap();
        lifecycle.page_load_started().unwrap();
        assert_eq!(
            lifecycle.queue_or_take_ready("open-agent".into()).unwrap(),
            None
        );
        lifecycle.window_destroyed().unwrap();
        let absent = lifecycle.snapshot().unwrap();
        assert_eq!(absent.surface_state, ControlCenterSurfaceState::Absent);
        assert!(!absent.ready);
        assert_eq!(absent.pending_command_count, 1);

        lifecycle.window_created().unwrap();
        lifecycle.window_shown().unwrap();
        assert_eq!(lifecycle.mark_ready().unwrap(), ["open-agent".to_string()]);
    }

    #[test]
    fn duplicate_visibility_events_do_not_advance_generation() {
        let lifecycle = ControlCenterLifecycle::default();
        let first = lifecycle.window_created().unwrap();
        let second = lifecycle.window_hidden().unwrap();
        assert_eq!(first.generation, second.generation);
        assert!(second.event_count > first.event_count);
    }
}
