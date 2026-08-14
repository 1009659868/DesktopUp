use std::{
    ffi::OsString,
    sync::{
        atomic::{AtomicU64, Ordering},
        Mutex,
    },
};

use serde::Serialize;

use crate::{autostart_contract::AUTOSTART_ARGUMENT, performance_gate::PERFORMANCE_GATE_ARGUMENT};

pub const LAUNCH_INTENT_CONTRACT_VERSION: u32 = 1;
pub const LAUNCH_INTENT_CONTRACT_NAME: &str = "launch-intent-v1";
pub const INVALID_LAUNCH_EXIT_CODE: i32 = 64;

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum LaunchIntentSource {
    InitialInstance,
    SecondaryInstance,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum LaunchIntentKind {
    Interactive,
    Autostart,
    PerformanceGate,
    Invalid,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum LaunchIntentDisposition {
    StartRuntime,
    StartDiagnosticRuntime,
    ShowControlCenter,
    KeepExistingUi,
    Reject,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct LaunchIntentDecision {
    pub contract_version: u32,
    pub contract_name: String,
    pub source: LaunchIntentSource,
    pub kind: LaunchIntentKind,
    pub disposition: LaunchIntentDisposition,
    pub valid: bool,
    pub argument_count: usize,
    pub rejection_code: Option<String>,
}

impl LaunchIntentDecision {
    fn accepted(
        source: LaunchIntentSource,
        kind: LaunchIntentKind,
        disposition: LaunchIntentDisposition,
        argument_count: usize,
    ) -> Self {
        Self {
            contract_version: LAUNCH_INTENT_CONTRACT_VERSION,
            contract_name: LAUNCH_INTENT_CONTRACT_NAME.to_string(),
            source,
            kind,
            disposition,
            valid: true,
            argument_count,
            rejection_code: None,
        }
    }

    fn rejected(
        source: LaunchIntentSource,
        kind: LaunchIntentKind,
        argument_count: usize,
        rejection_code: &str,
    ) -> Self {
        Self {
            contract_version: LAUNCH_INTENT_CONTRACT_VERSION,
            contract_name: LAUNCH_INTENT_CONTRACT_NAME.to_string(),
            source,
            kind,
            disposition: LaunchIntentDisposition::Reject,
            valid: false,
            argument_count,
            rejection_code: Some(rejection_code.to_string()),
        }
    }
}

pub fn decide_initial_from_env() -> LaunchIntentDecision {
    let mut raw_arguments = std::env::args_os();
    let _executable = raw_arguments.next();
    decide_os_arguments(LaunchIntentSource::InitialInstance, raw_arguments)
}

fn decide_os_arguments(
    source: LaunchIntentSource,
    arguments: impl IntoIterator<Item = OsString>,
) -> LaunchIntentDecision {
    let mut normalized = Vec::new();
    for argument in arguments {
        match argument.into_string() {
            Ok(argument) => normalized.push(argument),
            Err(_) => {
                return LaunchIntentDecision::rejected(
                    source,
                    LaunchIntentKind::Invalid,
                    normalized.len() + 1,
                    "nonUnicodeArgument",
                )
            }
        }
    }
    decide_application_arguments(source, &normalized)
}

pub fn decide_initial_application_arguments(arguments: &[String]) -> LaunchIntentDecision {
    decide_application_arguments(LaunchIntentSource::InitialInstance, arguments)
}

pub fn decide_secondary_forwarded_arguments(
    forwarded_arguments: &[String],
) -> LaunchIntentDecision {
    let Some((_executable, application_arguments)) = forwarded_arguments.split_first() else {
        return LaunchIntentDecision::rejected(
            LaunchIntentSource::SecondaryInstance,
            LaunchIntentKind::Invalid,
            0,
            "missingForwardedExecutable",
        );
    };
    decide_application_arguments(LaunchIntentSource::SecondaryInstance, application_arguments)
}

fn decide_application_arguments(
    source: LaunchIntentSource,
    arguments: &[String],
) -> LaunchIntentDecision {
    let (kind, initial_disposition, secondary_disposition) = match arguments {
        [] => (
            LaunchIntentKind::Interactive,
            LaunchIntentDisposition::StartRuntime,
            LaunchIntentDisposition::ShowControlCenter,
        ),
        [argument] if argument == AUTOSTART_ARGUMENT => (
            LaunchIntentKind::Autostart,
            LaunchIntentDisposition::StartRuntime,
            LaunchIntentDisposition::KeepExistingUi,
        ),
        [argument] if argument == PERFORMANCE_GATE_ARGUMENT => (
            LaunchIntentKind::PerformanceGate,
            LaunchIntentDisposition::StartDiagnosticRuntime,
            LaunchIntentDisposition::Reject,
        ),
        _ => {
            return LaunchIntentDecision::rejected(
                source,
                LaunchIntentKind::Invalid,
                arguments.len(),
                "unsupportedArguments",
            )
        }
    };

    match (source, kind) {
        (LaunchIntentSource::SecondaryInstance, LaunchIntentKind::PerformanceGate) => {
            LaunchIntentDecision::rejected(
                source,
                kind,
                arguments.len(),
                "diagnosticIntentNotForwardable",
            )
        }
        (LaunchIntentSource::InitialInstance, _) => {
            LaunchIntentDecision::accepted(source, kind, initial_disposition, arguments.len())
        }
        (LaunchIntentSource::SecondaryInstance, _) => {
            LaunchIntentDecision::accepted(source, kind, secondary_disposition, arguments.len())
        }
    }
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct LaunchIntentDiagnostics {
    pub contract_version: u32,
    pub contract_name: String,
    pub initial: LaunchIntentDecision,
    pub secondary_event_count: u64,
    pub last_secondary: Option<LaunchIntentDecision>,
}

pub struct LaunchIntentCoordinator {
    initial: LaunchIntentDecision,
    secondary_event_count: AtomicU64,
    last_secondary: Mutex<Option<LaunchIntentDecision>>,
}

impl LaunchIntentCoordinator {
    pub fn new(initial: LaunchIntentDecision) -> Self {
        Self {
            initial,
            secondary_event_count: AtomicU64::new(0),
            last_secondary: Mutex::new(None),
        }
    }

    pub fn record_secondary(&self, decision: LaunchIntentDecision) {
        let mut last = self
            .last_secondary
            .lock()
            .unwrap_or_else(|poisoned| poisoned.into_inner());
        *last = Some(decision);
        self.secondary_event_count.fetch_add(1, Ordering::AcqRel);
    }

    pub fn diagnostics(&self) -> LaunchIntentDiagnostics {
        let last_secondary = self
            .last_secondary
            .lock()
            .unwrap_or_else(|poisoned| poisoned.into_inner())
            .clone();
        LaunchIntentDiagnostics {
            contract_version: LAUNCH_INTENT_CONTRACT_VERSION,
            contract_name: LAUNCH_INTENT_CONTRACT_NAME.to_string(),
            initial: self.initial.clone(),
            secondary_event_count: self.secondary_event_count.load(Ordering::Acquire),
            last_secondary,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn strings(values: &[&str]) -> Vec<String> {
        values.iter().map(|value| (*value).to_string()).collect()
    }

    #[test]
    fn initial_launch_matrix_is_exact() {
        let interactive = decide_initial_application_arguments(&[]);
        assert_eq!(interactive.kind, LaunchIntentKind::Interactive);
        assert_eq!(
            interactive.disposition,
            LaunchIntentDisposition::StartRuntime
        );
        assert!(interactive.valid);

        let autostart = decide_initial_application_arguments(&strings(&[AUTOSTART_ARGUMENT]));
        assert_eq!(autostart.kind, LaunchIntentKind::Autostart);
        assert_eq!(autostart.disposition, LaunchIntentDisposition::StartRuntime);
        assert!(autostart.valid);

        let performance =
            decide_initial_application_arguments(&strings(&[PERFORMANCE_GATE_ARGUMENT]));
        assert_eq!(performance.kind, LaunchIntentKind::PerformanceGate);
        assert_eq!(
            performance.disposition,
            LaunchIntentDisposition::StartDiagnosticRuntime
        );
        assert!(performance.valid);
    }

    #[test]
    fn unknown_duplicate_and_extra_arguments_are_rejected() {
        for arguments in [
            strings(&["--unknown"]),
            strings(&[AUTOSTART_ARGUMENT, AUTOSTART_ARGUMENT]),
            strings(&[AUTOSTART_ARGUMENT, "--extra"]),
            strings(&["--Autostart"]),
            strings(&[""]),
        ] {
            let decision = decide_initial_application_arguments(&arguments);
            assert_eq!(decision.kind, LaunchIntentKind::Invalid);
            assert_eq!(decision.disposition, LaunchIntentDisposition::Reject);
            assert!(!decision.valid);
            assert_eq!(
                decision.rejection_code.as_deref(),
                Some("unsupportedArguments")
            );
        }
    }

    #[test]
    fn secondary_instance_drops_only_the_forwarded_executable() {
        let interactive = decide_secondary_forwarded_arguments(&strings(&["DesktopUp.exe"]));
        assert_eq!(interactive.kind, LaunchIntentKind::Interactive);
        assert_eq!(
            interactive.disposition,
            LaunchIntentDisposition::ShowControlCenter
        );

        let autostart =
            decide_secondary_forwarded_arguments(&strings(&["DesktopUp.exe", AUTOSTART_ARGUMENT]));
        assert_eq!(autostart.kind, LaunchIntentKind::Autostart);
        assert_eq!(
            autostart.disposition,
            LaunchIntentDisposition::KeepExistingUi
        );

        let invalid = decide_secondary_forwarded_arguments(&strings(&[
            "DesktopUp.exe",
            AUTOSTART_ARGUMENT,
            "--extra",
        ]));
        assert_eq!(invalid.disposition, LaunchIntentDisposition::Reject);
        assert!(!invalid.valid);
    }

    #[test]
    fn diagnostic_intent_cannot_be_forwarded_to_an_existing_instance() {
        let decision = decide_secondary_forwarded_arguments(&strings(&[
            "DesktopUp.exe",
            PERFORMANCE_GATE_ARGUMENT,
        ]));
        assert_eq!(decision.kind, LaunchIntentKind::PerformanceGate);
        assert_eq!(decision.disposition, LaunchIntentDisposition::Reject);
        assert_eq!(
            decision.rejection_code.as_deref(),
            Some("diagnosticIntentNotForwardable")
        );
    }

    #[test]
    fn missing_forwarded_executable_is_rejected() {
        let decision = decide_secondary_forwarded_arguments(&[]);
        assert_eq!(decision.kind, LaunchIntentKind::Invalid);
        assert_eq!(decision.argument_count, 0);
        assert_eq!(
            decision.rejection_code.as_deref(),
            Some("missingForwardedExecutable")
        );
    }

    #[test]
    fn coordinator_records_one_monotonic_secondary_snapshot() {
        let coordinator = LaunchIntentCoordinator::new(decide_initial_application_arguments(&[]));
        assert_eq!(coordinator.diagnostics().secondary_event_count, 0);

        coordinator.record_secondary(decide_secondary_forwarded_arguments(&strings(&[
            "DesktopUp.exe",
            AUTOSTART_ARGUMENT,
        ])));
        let diagnostics = coordinator.diagnostics();
        assert_eq!(diagnostics.secondary_event_count, 1);
        assert_eq!(
            diagnostics.last_secondary.map(|decision| decision.kind),
            Some(LaunchIntentKind::Autostart)
        );
    }
}
