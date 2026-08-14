use std::{
    env, fs,
    path::{Path, PathBuf},
};

use serde::Serialize;
use serde_json::{json, Value};
use tauri::Manager;

use crate::{
    autostart_contract::AutostartCoordinator, install_paths::RuntimePathResolution,
    publish_autostart_snapshot,
};

pub const AUTOSTART_TEST_GATE_NAME: &str = "crlc-autostart-test-gate-v1";
const OPERATION_PREFIX: &str = "--crlc-autostart-test=";
const REPORT_PREFIX: &str = "--crlc-autostart-report=";

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum TestOperation {
    Inspect,
    Enable,
    Disable,
}

impl TestOperation {
    fn parse(value: &str) -> Option<Self> {
        match value {
            "inspect" => Some(Self::Inspect),
            "enable" => Some(Self::Enable),
            "disable" => Some(Self::Disable),
            _ => None,
        }
    }

    fn as_str(self) -> &'static str {
        match self {
            Self::Inspect => "inspect",
            Self::Enable => "enable",
            Self::Disable => "disable",
        }
    }
}

#[derive(Clone, Debug, Eq, PartialEq)]
struct TestRequest {
    operation: TestOperation,
    report_path: PathBuf,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct TestReport {
    gate_name: &'static str,
    operation: &'static str,
    handled_in_existing_instance: bool,
    succeeded: bool,
    payload: Value,
    error: String,
}

pub fn is_initial_forward_request() -> bool {
    let arguments = env::args().skip(1).collect::<Vec<_>>();
    parse_application_arguments(&arguments).is_some()
}

pub fn try_handle_forwarded(app: &tauri::AppHandle, forwarded_arguments: &[String]) -> bool {
    let application_arguments = forwarded_arguments.get(1..).unwrap_or_default();
    let Some(request) = parse_application_arguments(application_arguments) else {
        return false;
    };
    let report = execute_request(app, request.operation);
    if let Err(error) = write_report(&request.report_path, &report) {
        eprintln!("Desktop-Up autostart test report failed: {error}");
    }
    true
}

fn parse_application_arguments(arguments: &[String]) -> Option<TestRequest> {
    let [operation_argument, report_argument] = arguments else {
        return None;
    };
    let operation = operation_argument
        .strip_prefix(OPERATION_PREFIX)
        .and_then(TestOperation::parse)?;
    let report_path = report_argument.strip_prefix(REPORT_PREFIX)?;
    let request = TestRequest {
        operation,
        report_path: PathBuf::from(report_path),
    };
    report_path_allowed(&request.report_path).then_some(request)
}

fn execute_request(app: &tauri::AppHandle, operation: TestOperation) -> TestReport {
    let Some(paths) = app.try_state::<RuntimePathResolution>() else {
        return failed_report(operation, "runtime path state is unavailable");
    };
    let Some(coordinator) = app.try_state::<AutostartCoordinator>() else {
        return failed_report(operation, "autostart coordinator is unavailable");
    };

    match operation {
        TestOperation::Inspect => {
            let snapshot = coordinator.refresh(paths.inner());
            publish_autostart_snapshot(app, snapshot.clone());
            TestReport {
                gate_name: AUTOSTART_TEST_GATE_NAME,
                operation: operation.as_str(),
                handled_in_existing_instance: true,
                succeeded: snapshot.registration_eligible,
                payload: serde_json::to_value(snapshot)
                    .unwrap_or_else(|error| json!({ "serializationError": error.to_string() })),
                error: String::new(),
            }
        }
        TestOperation::Enable | TestOperation::Disable => {
            let enabled = operation == TestOperation::Enable;
            let result = coordinator.set_enabled(paths.inner(), enabled);
            publish_autostart_snapshot(app, result.after.clone());
            let succeeded = result.succeeded;
            TestReport {
                gate_name: AUTOSTART_TEST_GATE_NAME,
                operation: operation.as_str(),
                handled_in_existing_instance: true,
                succeeded,
                payload: serde_json::to_value(result)
                    .unwrap_or_else(|error| json!({ "serializationError": error.to_string() })),
                error: String::new(),
            }
        }
    }
}

fn failed_report(operation: TestOperation, error: &str) -> TestReport {
    TestReport {
        gate_name: AUTOSTART_TEST_GATE_NAME,
        operation: operation.as_str(),
        handled_in_existing_instance: true,
        succeeded: false,
        payload: Value::Null,
        error: error.to_string(),
    }
}

fn write_report(path: &Path, report: &TestReport) -> Result<(), String> {
    if !report_path_allowed(path) {
        return Err(
            "report path must be a JSON file inside the OS temporary directory".to_string(),
        );
    }
    let bytes = serde_json::to_vec_pretty(report).map_err(|error| error.to_string())?;
    fs::write(path, bytes).map_err(|error| error.to_string())
}

fn report_path_allowed(path: &Path) -> bool {
    if !path.is_absolute() || path.extension().and_then(|value| value.to_str()) != Some("json") {
        return false;
    }
    let normalized_report = normalized_path(path);
    let normalized_temp = normalized_path(&env::temp_dir());
    normalized_report.starts_with(&format!("{normalized_temp}/"))
}

fn normalized_path(path: &Path) -> String {
    let mut value = path.to_string_lossy().replace('\\', "/");
    if let Some(stripped) = value.strip_prefix("//?/") {
        value = stripped.to_string();
    }
    while value.ends_with('/') {
        value.pop();
    }
    value.make_ascii_lowercase();
    value
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn gate_requires_one_known_operation_and_one_safe_report() {
        let report = env::temp_dir().join("desktop-up-autostart-test-report.json");
        let valid = vec![
            format!("{OPERATION_PREFIX}enable"),
            format!("{REPORT_PREFIX}{}", report.display()),
        ];
        assert_eq!(
            parse_application_arguments(&valid),
            Some(TestRequest {
                operation: TestOperation::Enable,
                report_path: report
            })
        );
        assert!(parse_application_arguments(&valid[..1]).is_none());
        let unknown = vec![format!("{OPERATION_PREFIX}erase"), valid[1].clone()];
        assert!(parse_application_arguments(&unknown).is_none());
    }
}
