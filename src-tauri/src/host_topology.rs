use serde::Serialize;
use tauri::Manager;

#[cfg(test)]
use crate::host_contract::DisplayDescriptor;
use crate::host_contract::{
    LogicalSize, PhysicalRect, ResponsiveScalePolicy, HOST_CONTRACT_VERSION,
};

pub const DESKTOP_HOST_WINDOW_LABEL: &str = "poster";
pub const CONTROL_CENTER_WINDOW_LABEL: &str = "main";
pub const EXPECTED_DESKTOP_HOST_COUNT: usize = 1;

#[derive(Clone, Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct HostTopologySnapshot {
    pub contract_version: u32,
    pub desktop_host_window_label: String,
    pub expected_desktop_host_count: usize,
    pub live_desktop_host_count: usize,
    pub control_center_count: usize,
    pub total_webview_count: usize,
    pub invariant_satisfied: bool,
}

impl Default for HostTopologySnapshot {
    fn default() -> Self {
        Self {
            contract_version: HOST_CONTRACT_VERSION,
            desktop_host_window_label: DESKTOP_HOST_WINDOW_LABEL.to_string(),
            expected_desktop_host_count: EXPECTED_DESKTOP_HOST_COUNT,
            live_desktop_host_count: 0,
            control_center_count: 0,
            total_webview_count: 0,
            invariant_satisfied: false,
        }
    }
}

fn summarize_labels<'a>(labels: impl IntoIterator<Item = &'a str>) -> HostTopologySnapshot {
    let labels = labels.into_iter().collect::<Vec<_>>();
    let live_desktop_host_count = labels
        .iter()
        .filter(|label| **label == DESKTOP_HOST_WINDOW_LABEL)
        .count();
    let control_center_count = labels
        .iter()
        .filter(|label| **label == CONTROL_CENTER_WINDOW_LABEL)
        .count();
    HostTopologySnapshot {
        live_desktop_host_count,
        control_center_count,
        total_webview_count: labels.len(),
        invariant_satisfied: live_desktop_host_count == EXPECTED_DESKTOP_HOST_COUNT,
        ..HostTopologySnapshot::default()
    }
}

pub fn inspect_host_topology(app: &tauri::AppHandle) -> HostTopologySnapshot {
    let windows = app.webview_windows();
    summarize_labels(windows.keys().map(String::as_str))
}

pub fn desktop_host_window(app: &tauri::AppHandle) -> Result<tauri::WebviewWindow, String> {
    app.get_webview_window(DESKTOP_HOST_WINDOW_LABEL)
        .ok_or_else(|| "desktop host window is unavailable".to_string())
}

pub fn optional_desktop_host_window(app: &tauri::AppHandle) -> Option<tauri::WebviewWindow> {
    app.get_webview_window(DESKTOP_HOST_WINDOW_LABEL)
}

pub fn effective_logical_work_area(
    work_area: &PhysicalRect,
    device_scale_factor: f64,
) -> LogicalSize {
    let scale = if device_scale_factor.is_finite() && device_scale_factor > 0.0 {
        device_scale_factor
    } else {
        1.0
    };
    LogicalSize {
        width: work_area.width as f64 / scale,
        height: work_area.height as f64 / scale,
    }
}

pub fn responsive_scale_for_work_area(
    logical_work_area: &LogicalSize,
    policy: &ResponsiveScalePolicy,
) -> f64 {
    let width_ratio = logical_work_area.width / policy.reference_logical_size.width;
    let height_ratio = logical_work_area.height / policy.reference_logical_size.height;
    let raw = width_ratio.min(height_ratio);
    let snapped = if (raw - 1.0).abs() <= policy.baseline_snap_tolerance {
        1.0
    } else {
        raw
    };
    let bounded = if snapped.is_finite() {
        snapped.clamp(policy.minimum_scale, policy.maximum_scale)
    } else {
        1.0
    };
    (bounded * 1000.0).round() / 1000.0
}

#[cfg(test)]
fn display_scale_components(display: &DisplayDescriptor, user_scale: f64) -> (f64, f64, f64, f64) {
    let device = display.renderer_scale_factor;
    let responsive = display.responsive_scale;
    let user = user_scale;
    (device, responsive, user, device * responsive * user)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn work_area(width: u32, height: u32, device_scale_factor: f64) -> (LogicalSize, f64) {
        let physical = PhysicalRect {
            x: 0,
            y: 0,
            width,
            height,
        };
        let logical = effective_logical_work_area(&physical, device_scale_factor);
        let responsive =
            responsive_scale_for_work_area(&logical, &ResponsiveScalePolicy::default());
        (logical, responsive)
    }

    #[test]
    fn runtime_topology_requires_exactly_one_desktop_host_webview() {
        let valid = summarize_labels(["main", "poster"]);
        assert_eq!(valid.live_desktop_host_count, 1);
        assert_eq!(valid.control_center_count, 1);
        assert_eq!(valid.total_webview_count, 2);
        assert!(valid.invariant_satisfied);

        assert!(!summarize_labels(["main"]).invariant_satisfied);
        assert!(!summarize_labels(["main", "poster", "poster"]).invariant_satisfied);
    }

    #[test]
    fn responsive_scale_matrix_is_bounded_and_monotonic() {
        let matrix = [
            work_area(1366, 768, 1.0).1,
            work_area(1920, 1080, 1.0).1,
            work_area(2560, 1440, 1.0).1,
            work_area(3840, 2160, 1.0).1,
        ];
        assert_eq!(matrix, [0.8, 1.0, 1.25, 1.25]);
        assert!(matrix.windows(2).all(|pair| pair[0] <= pair[1]));
    }

    #[test]
    fn equivalent_logical_work_areas_produce_the_same_responsive_scale() {
        let full_hd = work_area(1920, 1080, 1.0);
        let four_k_at_two_hundred_percent = work_area(3840, 2160, 2.0);
        assert_eq!(full_hd.0, four_k_at_two_hundred_percent.0);
        assert_eq!(full_hd.1, four_k_at_two_hundred_percent.1);

        let qhd = work_area(2560, 1440, 1.0);
        let four_k_at_one_hundred_fifty_percent = work_area(3840, 2160, 1.5);
        assert_eq!(qhd.0, four_k_at_one_hundred_fifty_percent.0);
        assert_eq!(qhd.1, four_k_at_one_hundred_fifty_percent.1);
    }

    #[test]
    fn ordinary_taskbar_work_area_snaps_to_the_full_hd_baseline() {
        let (_, responsive) = work_area(1920, 1040, 1.0);
        assert_eq!(responsive, 1.0);
    }

    #[test]
    fn renderer_scale_can_differ_from_native_monitor_dpi() {
        let physical = PhysicalRect {
            x: 0,
            y: 0,
            width: 2560,
            height: 1392,
        };
        let logical = effective_logical_work_area(&physical, 1.2);
        let responsive =
            responsive_scale_for_work_area(&logical, &ResponsiveScalePolicy::default());
        assert!((logical.width - 2133.333).abs() < 0.001);
        assert_eq!(logical.height, 1160.0);
        assert_eq!(responsive, 1.074);
        assert!((1.2 * responsive - 1.2888).abs() < 1e-9);
    }

    #[test]
    fn device_responsive_and_user_scale_remain_independent() {
        let logical = LogicalSize {
            width: 2560.0,
            height: 1440.0,
        };
        let display = DisplayDescriptor {
            display_id: "display".to_string(),
            is_primary: true,
            scale_factor: 1.5,
            renderer_scale_factor: 1.5,
            bounds: PhysicalRect::default(),
            work_area: PhysicalRect::default(),
            effective_logical_work_area: logical.clone(),
            responsive_scale: responsive_scale_for_work_area(
                &logical,
                &ResponsiveScalePolicy::default(),
            ),
        };
        assert_eq!(
            display_scale_components(&display, 1.0),
            (1.5, 1.25, 1.0, 1.875)
        );
        assert_eq!(
            display_scale_components(&display, 0.8),
            (1.5, 1.25, 0.8, 1.5)
        );
    }
}
