use serde::{Deserialize, Serialize};

pub const PERFORMANCE_GATE_CONTRACT: &str = "performance-gate-v3";
pub const PERFORMANCE_GATE_ARGUMENT: &str = "--cdrp-performance-gate";
pub const PERFORMANCE_SAMPLE_COUNT: usize = 6;
pub const PERFORMANCE_STABILIZATION_MIN_MS: u64 = 120_000;
pub const PERFORMANCE_WINDOW_MIN_MS: u64 = 9_500;
pub const CPU_DELTA_BUDGET_MS: u64 = 100;
pub const MAIN_PRIVATE_WORKING_SET_BUDGET_BYTES: u64 = 50 * 1024 * 1024;
pub const PROCESS_TREE_PRIVATE_WORKING_SET_BUDGET_BYTES: u64 = 256 * 1024 * 1024;
pub const ACTIVE_ANIMATED_FPS_LIMIT: f64 = 30.0;

const REQUIRED_MODES: [&str; 4] = ["activeStatic", "activeAnimated", "paused", "stopped"];

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PerformanceSample {
    pub mode: String,
    pub memory_bytes: u64,
    pub private_working_set_bytes: u64,
    pub private_commit_bytes: u64,
    pub process_count: u32,
    pub process_tree_working_set_bytes: u64,
    pub process_tree_private_working_set_bytes: u64,
    pub process_tree_private_commit_bytes: u64,
    pub process_tree_process_count: u32,
    pub process_tree_measurement_complete: bool,
    pub poster_fps: f64,
    pub cpu_ms: u64,
    pub pause_state: String,
    pub elapsed_ms: u64,
    pub sampled_at: String,
    pub build_profile: String,
    pub host_contract_version: u32,
}

#[derive(Clone, Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PerformanceBudget {
    pub sample_count: usize,
    pub stabilization_min_ms: u64,
    pub window_min_ms: u64,
    pub cpu_delta_budget_ms: u64,
    pub main_private_working_set_budget_bytes: u64,
    pub process_tree_private_working_set_budget_bytes: u64,
    pub active_animated_fps_limit: f64,
}

impl Default for PerformanceBudget {
    fn default() -> Self {
        Self {
            sample_count: PERFORMANCE_SAMPLE_COUNT,
            stabilization_min_ms: PERFORMANCE_STABILIZATION_MIN_MS,
            window_min_ms: PERFORMANCE_WINDOW_MIN_MS,
            cpu_delta_budget_ms: CPU_DELTA_BUDGET_MS,
            main_private_working_set_budget_bytes: MAIN_PRIVATE_WORKING_SET_BUDGET_BYTES,
            process_tree_private_working_set_budget_bytes:
                PROCESS_TREE_PRIVATE_WORKING_SET_BUDGET_BYTES,
            active_animated_fps_limit: ACTIVE_ANIMATED_FPS_LIMIT,
        }
    }
}

#[derive(Clone, Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PerformanceModeReport {
    pub mode: String,
    pub sample_count: usize,
    pub window_ms: u64,
    pub cpu_delta_ms: u64,
    pub peak_memory_bytes: u64,
    pub peak_private_working_set_bytes: u64,
    pub peak_private_commit_bytes: u64,
    pub peak_process_tree_working_set_bytes: u64,
    pub peak_process_tree_private_working_set_bytes: u64,
    pub peak_process_tree_private_commit_bytes: u64,
    pub max_process_tree_process_count: u32,
    pub process_tree_measurement_complete: bool,
    pub max_fps: f64,
    pub passed: bool,
    pub violations: Vec<String>,
}

#[derive(Clone, Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PerformanceGateReport {
    pub contract_version: String,
    pub expected_host_contract_version: u32,
    pub required_modes: Vec<String>,
    pub budget: PerformanceBudget,
    pub passed: bool,
    pub violations: Vec<String>,
    pub modes: Vec<PerformanceModeReport>,
}

pub fn evaluate_performance_samples(
    samples: &[PerformanceSample],
    expected_host_contract_version: u32,
) -> PerformanceGateReport {
    let mut global_violations = Vec::new();
    if samples
        .iter()
        .any(|sample| sample.build_profile != "release")
    {
        global_violations.push("non-release-sample".to_string());
    }
    if samples
        .iter()
        .any(|sample| sample.host_contract_version != expected_host_contract_version)
    {
        global_violations.push("host-contract-version-mismatch".to_string());
    }

    let mut modes = Vec::new();
    for mode in REQUIRED_MODES {
        let mut mode_samples: Vec<&PerformanceSample> = samples
            .iter()
            .filter(|sample| sample.mode == mode)
            .collect();
        mode_samples.sort_by_key(|sample| sample.elapsed_ms);

        let sample_count = mode_samples.len();
        let window_ms = mode_samples
            .first()
            .zip(mode_samples.last())
            .map(|(first, last)| last.elapsed_ms.saturating_sub(first.elapsed_ms))
            .unwrap_or(0);
        let cpu_delta_ms = mode_samples
            .first()
            .zip(mode_samples.last())
            .map(|(first, last)| last.cpu_ms.saturating_sub(first.cpu_ms))
            .unwrap_or(0);
        let peak_memory_bytes = mode_samples
            .iter()
            .map(|sample| sample.memory_bytes)
            .max()
            .unwrap_or(0);
        let peak_private_working_set_bytes = mode_samples
            .iter()
            .map(|sample| sample.private_working_set_bytes)
            .max()
            .unwrap_or(0);
        let peak_private_commit_bytes = mode_samples
            .iter()
            .map(|sample| sample.private_commit_bytes)
            .max()
            .unwrap_or(0);
        let max_fps = mode_samples
            .iter()
            .fold(0.0_f64, |maximum, sample| maximum.max(sample.poster_fps));
        let peak_process_tree_working_set_bytes = mode_samples
            .iter()
            .map(|sample| sample.process_tree_working_set_bytes)
            .max()
            .unwrap_or(0);
        let peak_process_tree_private_working_set_bytes = mode_samples
            .iter()
            .map(|sample| sample.process_tree_private_working_set_bytes)
            .max()
            .unwrap_or(0);
        let peak_process_tree_private_commit_bytes = mode_samples
            .iter()
            .map(|sample| sample.process_tree_private_commit_bytes)
            .max()
            .unwrap_or(0);
        let max_process_tree_process_count = mode_samples
            .iter()
            .map(|sample| sample.process_tree_process_count)
            .max()
            .unwrap_or(0);
        let process_tree_measurement_complete = !mode_samples.is_empty()
            && mode_samples
                .iter()
                .all(|sample| sample.process_tree_measurement_complete);

        let mut violations = Vec::new();
        if sample_count < PERFORMANCE_SAMPLE_COUNT {
            violations.push("insufficient-samples".to_string());
        }
        if window_ms < PERFORMANCE_WINDOW_MIN_MS {
            violations.push("window-too-short".to_string());
        }
        if mode_samples
            .first()
            .is_none_or(|sample| sample.elapsed_ms < PERFORMANCE_STABILIZATION_MIN_MS)
        {
            violations.push("stabilization-window-not-reached".to_string());
        }
        if mode_samples.iter().any(|sample| sample.pause_state != mode) {
            violations.push("pause-state-mismatch".to_string());
        }
        if peak_private_working_set_bytes > MAIN_PRIVATE_WORKING_SET_BUDGET_BYTES {
            violations.push("main-private-working-set-budget-exceeded".to_string());
        }
        if peak_process_tree_private_working_set_bytes
            > PROCESS_TREE_PRIVATE_WORKING_SET_BUDGET_BYTES
        {
            violations.push("process-tree-private-working-set-budget-exceeded".to_string());
        }
        if !process_tree_measurement_complete {
            violations.push("process-tree-measurement-incomplete".to_string());
        }

        match mode {
            "activeStatic" | "paused" => {
                if cpu_delta_ms > CPU_DELTA_BUDGET_MS {
                    violations.push("cpu-delta-budget-exceeded".to_string());
                }
                if max_fps > 0.0 {
                    violations.push("unexpected-render-frames".to_string());
                }
            }
            "activeAnimated" => {
                if max_fps > ACTIVE_ANIMATED_FPS_LIMIT {
                    violations.push("animated-fps-budget-exceeded".to_string());
                }
            }
            "stopped" => {
                if max_fps > 0.0 {
                    violations.push("unexpected-render-frames".to_string());
                }
            }
            _ => {}
        }

        for violation in &violations {
            global_violations.push(format!("{mode}:{violation}"));
        }
        modes.push(PerformanceModeReport {
            mode: mode.to_string(),
            sample_count,
            window_ms,
            cpu_delta_ms,
            peak_memory_bytes,
            peak_private_working_set_bytes,
            peak_private_commit_bytes,
            max_fps,
            passed: violations.is_empty(),
            peak_process_tree_working_set_bytes,
            peak_process_tree_private_working_set_bytes,
            peak_process_tree_private_commit_bytes,
            max_process_tree_process_count,
            process_tree_measurement_complete,
            violations,
        });
    }

    PerformanceGateReport {
        contract_version: PERFORMANCE_GATE_CONTRACT.to_string(),
        expected_host_contract_version,
        required_modes: REQUIRED_MODES
            .iter()
            .map(|mode| (*mode).to_string())
            .collect(),
        budget: PerformanceBudget::default(),
        passed: global_violations.is_empty(),
        violations: global_violations,
        modes,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn samples_for(
        mode: &str,
        cpu_delta_ms: u64,
        memory_bytes: u64,
        fps: f64,
    ) -> Vec<PerformanceSample> {
        (0..PERFORMANCE_SAMPLE_COUNT)
            .map(|index| PerformanceSample {
                mode: mode.to_string(),
                memory_bytes,
                private_working_set_bytes: MAIN_PRIVATE_WORKING_SET_BUDGET_BYTES,
                private_commit_bytes: 64 * 1024 * 1024,
                process_count: 1,
                process_tree_working_set_bytes: 512 * 1024 * 1024,
                process_tree_private_working_set_bytes:
                    PROCESS_TREE_PRIVATE_WORKING_SET_BUDGET_BYTES,
                process_tree_private_commit_bytes: 320 * 1024 * 1024,
                process_tree_process_count: 6,
                process_tree_measurement_complete: true,
                poster_fps: fps,
                cpu_ms: 1_000
                    + (cpu_delta_ms * index as u64 / (PERFORMANCE_SAMPLE_COUNT as u64 - 1)),
                pause_state: mode.to_string(),
                elapsed_ms: PERFORMANCE_STABILIZATION_MIN_MS + index as u64 * 2_000,
                sampled_at: format!("sample-{index}"),
                build_profile: "release".to_string(),
                host_contract_version: 1,
            })
            .collect()
    }

    fn passing_samples() -> Vec<PerformanceSample> {
        let diagnostic_total = 512 * 1024 * 1024;
        let mut samples = samples_for("activeStatic", 90, diagnostic_total, 0.0);
        samples.extend(samples_for("activeAnimated", 400, diagnostic_total, 30.0));
        samples.extend(samples_for("paused", 100, diagnostic_total, 0.0));
        samples.extend(samples_for("stopped", 0, diagnostic_total, 0.0));
        samples
    }

    #[test]
    fn release_samples_at_the_budget_boundary_pass() {
        let report = evaluate_performance_samples(&passing_samples(), 1);
        assert!(report.passed, "{:?}", report.violations);
        assert_eq!(report.contract_version, PERFORMANCE_GATE_CONTRACT);
        assert_eq!(report.modes.len(), 4);
    }

    #[test]
    fn short_windows_and_budget_overruns_fail_with_mode_evidence() {
        let mut samples = passing_samples();
        samples.retain(|sample| {
            !(sample.mode == "paused"
                && sample.elapsed_ms == PERFORMANCE_STABILIZATION_MIN_MS + 10_000)
        });
        for sample in samples
            .iter_mut()
            .filter(|sample| sample.mode == "activeStatic")
        {
            sample.private_working_set_bytes = MAIN_PRIVATE_WORKING_SET_BUDGET_BYTES + 1;
            sample.process_tree_private_working_set_bytes =
                PROCESS_TREE_PRIVATE_WORKING_SET_BUDGET_BYTES + 1;
        }
        for sample in samples
            .iter_mut()
            .filter(|sample| sample.mode == "activeAnimated")
        {
            sample.poster_fps = 60.0;
        }

        let report = evaluate_performance_samples(&samples, 1);
        assert!(!report.passed);
        assert!(report
            .violations
            .contains(&"paused:insufficient-samples".to_string()));
        assert!(report
            .violations
            .contains(&"paused:window-too-short".to_string()));
        assert!(report
            .violations
            .contains(&"activeStatic:main-private-working-set-budget-exceeded".to_string()));
        assert!(report.violations.contains(
            &"activeStatic:process-tree-private-working-set-budget-exceeded".to_string()
        ));
        assert!(report
            .violations
            .contains(&"activeAnimated:animated-fps-budget-exceeded".to_string()));
    }

    #[test]
    fn samples_before_the_stabilization_window_cannot_certify_release() {
        let mut samples = passing_samples();
        for sample in &mut samples {
            sample.elapsed_ms -= PERFORMANCE_STABILIZATION_MIN_MS;
        }
        let report = evaluate_performance_samples(&samples, 1);
        assert!(!report.passed);
        assert!(report
            .violations
            .contains(&"activeStatic:stabilization-window-not-reached".to_string()));
    }

    #[test]
    fn debug_or_mismatched_contract_samples_cannot_certify_release() {
        let mut samples = passing_samples();
        samples[0].build_profile = "debug".to_string();
        samples[1].host_contract_version = 2;
        let report = evaluate_performance_samples(&samples, 1);
        assert!(report
            .violations
            .contains(&"non-release-sample".to_string()));
        assert!(report
            .violations
            .contains(&"host-contract-version-mismatch".to_string()));
    }

    #[test]
    fn a_static_product_does_not_invent_animated_frames() {
        let mut samples = passing_samples();
        for sample in samples
            .iter_mut()
            .filter(|sample| sample.mode == "activeAnimated")
        {
            sample.poster_fps = 0.0;
        }
        let report = evaluate_performance_samples(&samples, 1);
        assert!(report.passed, "{:?}", report.violations);
    }

    #[test]
    fn incomplete_process_tree_measurements_cannot_certify_release() {
        let mut samples = passing_samples();
        for sample in samples.iter_mut().filter(|sample| sample.mode == "stopped") {
            sample.process_tree_measurement_complete = false;
        }
        let report = evaluate_performance_samples(&samples, 1);
        assert!(report
            .violations
            .contains(&"stopped:process-tree-measurement-incomplete".to_string()));
    }
}
