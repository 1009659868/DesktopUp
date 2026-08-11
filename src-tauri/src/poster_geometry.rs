use std::fmt;

pub const POSTER_GEOMETRY_CONTRACT_VERSION: u32 = 1;
pub const SAFE_TOP: f64 = 24.0;
pub const SAFE_RIGHT: f64 = 24.0;
pub const SAFE_BOTTOM: f64 = 24.0;
pub const DEFAULT_WIDTH_RATIO: f64 = 0.22;
pub const MINIMUM_DEFAULT_WIDTH: u32 = 280;
pub const MINIMUM_WIDTH: u32 = 240;
pub const MAXIMUM_WIDTH_RATIO: f64 = 0.45;
pub const MINIMUM_EXPANDED_HEIGHT: u32 = 240;
pub const COLLAPSED_CONTENT_HEIGHT: u32 = 60;
pub const SURFACE_GUTTER: u32 = 10;
pub const COLLAPSED_HOST_HEIGHT: u32 = COLLAPSED_CONTENT_HEIGHT + SURFACE_GUTTER * 2;

#[derive(Clone, Debug, PartialEq)]
pub struct LogicalPoint {
    pub x: f64,
    pub y: f64,
}

#[derive(Clone, Debug, PartialEq)]
pub struct LogicalExtent {
    pub width: u32,
    pub height: u32,
}

#[derive(Clone, Debug, PartialEq)]
pub struct LogicalRect {
    pub x: f64,
    pub y: f64,
    pub width: u32,
    pub height: u32,
}

#[derive(Clone, Debug, PartialEq)]
pub struct HostEnvelope {
    pub offset: LogicalPoint,
    pub logical_size: LogicalExtent,
}

#[derive(Clone, Debug, PartialEq)]
pub struct ResolvedPosterGeometry {
    pub contract_version: u32,
    pub display_id: String,
    pub normalized_rect: LogicalPoint,
    pub poster_rect: LogicalRect,
    pub expanded_host: HostEnvelope,
    pub collapsed_host: HostEnvelope,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct PosterGeometryError {
    pub path: &'static str,
    pub code: &'static str,
    pub message: &'static str,
}

impl PosterGeometryError {
    fn new(path: &'static str, code: &'static str, message: &'static str) -> Self {
        Self {
            path,
            code,
            message,
        }
    }
}

impl fmt::Display for PosterGeometryError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "{} [{}]: {}", self.path, self.code, self.message)
    }
}

fn normalized_axis(position: f64, available_span: f64) -> f64 {
    if available_span <= 0.0 {
        0.0
    } else {
        (position / available_span).clamp(0.0, 1.0)
    }
}

pub fn resolve_default_poster_geometry(
    display_id: &str,
    is_primary: bool,
    work_width: f64,
    work_height: f64,
) -> Result<ResolvedPosterGeometry, Vec<PosterGeometryError>> {
    let mut errors = Vec::new();
    if display_id.trim().is_empty() {
        errors.push(PosterGeometryError::new(
            "$.displayId",
            "display_id_required",
            "the primary display must provide its opaque display id",
        ));
    }
    if !is_primary {
        errors.push(PosterGeometryError::new(
            "$.isPrimary",
            "primary_display_required",
            "the V1 default must be resolved from the primary display",
        ));
    }
    if !work_width.is_finite() || work_width <= 0.0 || work_width > u32::MAX as f64 {
        errors.push(PosterGeometryError::new(
            "$.width",
            "invalid_work_area_width",
            "work-area width must be positive, finite, and representable",
        ));
    }
    if !work_height.is_finite() || work_height <= 0.0 || work_height > u32::MAX as f64 {
        errors.push(PosterGeometryError::new(
            "$.height",
            "invalid_work_area_height",
            "work-area height must be positive, finite, and representable",
        ));
    }
    if !errors.is_empty() {
        return Err(errors);
    }

    let maximum_poster_width = (work_width * MAXIMUM_WIDTH_RATIO).floor() as u32;
    if maximum_poster_width < MINIMUM_WIDTH {
        errors.push(PosterGeometryError::new(
            "$.width",
            "work_area_too_narrow",
            "45% of the work area is smaller than the poster minimum width",
        ));
    }
    let expanded_height = (work_height - SAFE_TOP - SAFE_BOTTOM).floor();
    if expanded_height < MINIMUM_EXPANDED_HEIGHT as f64 {
        errors.push(PosterGeometryError::new(
            "$.height",
            "work_area_too_short",
            "the safe work-area height is smaller than the expanded minimum",
        ));
    }
    if !errors.is_empty() {
        return Err(errors);
    }

    let poster_width = (work_width * DEFAULT_WIDTH_RATIO)
        .round()
        .max(MINIMUM_DEFAULT_WIDTH as f64)
        .max(MINIMUM_WIDTH as f64)
        .min(maximum_poster_width as f64) as u32;
    let poster_height = expanded_height as u32;
    let poster_x = work_width - SAFE_RIGHT - poster_width as f64;
    let poster_y = SAFE_TOP;
    let host_offset_x = poster_x - SURFACE_GUTTER as f64;
    let host_offset_y = poster_y - SURFACE_GUTTER as f64;
    let expanded_host_width = poster_width + SURFACE_GUTTER * 2;
    let expanded_host_height = poster_height + SURFACE_GUTTER * 2;
    let normalized_x = normalized_axis(host_offset_x, work_width - expanded_host_width as f64);
    let normalized_y = normalized_axis(host_offset_y, work_height - expanded_host_height as f64);
    let host_offset = LogicalPoint {
        x: host_offset_x,
        y: host_offset_y,
    };

    Ok(ResolvedPosterGeometry {
        contract_version: POSTER_GEOMETRY_CONTRACT_VERSION,
        display_id: display_id.to_string(),
        normalized_rect: LogicalPoint {
            x: normalized_x,
            y: normalized_y,
        },
        poster_rect: LogicalRect {
            x: poster_x,
            y: poster_y,
            width: poster_width,
            height: poster_height,
        },
        expanded_host: HostEnvelope {
            offset: host_offset.clone(),
            logical_size: LogicalExtent {
                width: expanded_host_width,
                height: expanded_host_height,
            },
        },
        collapsed_host: HostEnvelope {
            offset: host_offset,
            logical_size: LogicalExtent {
                width: expanded_host_width,
                height: COLLAPSED_HOST_HEIGHT,
            },
        },
    })
}

#[cfg(test)]
mod tests {
    use super::{
        resolve_default_poster_geometry, COLLAPSED_CONTENT_HEIGHT, COLLAPSED_HOST_HEIGHT,
        MINIMUM_EXPANDED_HEIGHT, MINIMUM_WIDTH, SAFE_BOTTOM, SAFE_RIGHT, SAFE_TOP, SURFACE_GUTTER,
    };
    use serde_json::Value;

    fn close(actual: f64, expected: f64) -> bool {
        (actual - expected).abs() <= 1e-12
    }

    #[test]
    fn accepted_geometry_policy_values_are_frozen() {
        assert_eq!((SAFE_TOP, SAFE_RIGHT, SAFE_BOTTOM), (24.0, 24.0, 24.0));
        assert_eq!(MINIMUM_WIDTH, 240);
        assert_eq!(MINIMUM_EXPANDED_HEIGHT, 240);
        assert_eq!(COLLAPSED_CONTENT_HEIGHT, 60);
        assert_eq!(SURFACE_GUTTER, 10);
        assert_eq!(COLLAPSED_HOST_HEIGHT, 80);
    }

    #[test]
    fn rust_matches_the_shared_poster_geometry_v1_matrix() {
        let matrix: Value =
            serde_json::from_str(include_str!("../../tests/fixtures/poster-geometry-v1.json"))
                .expect("shared geometry matrix should be valid JSON");

        for sample in matrix
            .as_array()
            .expect("geometry matrix should be an array")
        {
            let name = sample["name"].as_str().expect("sample should have a name");
            let work_area = &sample["workArea"];
            let result = resolve_default_poster_geometry(
                work_area["displayId"].as_str().unwrap_or_default(),
                work_area["isPrimary"].as_bool().unwrap_or_default(),
                work_area["width"].as_f64().unwrap_or_default(),
                work_area["height"].as_f64().unwrap_or_default(),
            );
            let expected_valid = sample["valid"].as_bool().expect("sample decision");
            assert_eq!(result.is_ok(), expected_valid, "{name}");
            if !expected_valid {
                let expected_code = sample["errorCode"].as_str().expect("sample error code");
                assert!(
                    result
                        .expect_err("invalid sample should fail")
                        .iter()
                        .any(|error| error.code == expected_code),
                    "{name}"
                );
                continue;
            }

            let geometry = result.expect("valid sample should resolve");
            let expected = &sample["expected"];
            assert_eq!(geometry.poster_rect.width, expected["posterRect"]["width"]);
            assert_eq!(
                geometry.poster_rect.height,
                expected["posterRect"]["height"]
            );
            assert!(close(
                geometry.poster_rect.x,
                expected["posterRect"]["x"].as_f64().unwrap()
            ));
            assert!(close(
                geometry.poster_rect.y,
                expected["posterRect"]["y"].as_f64().unwrap()
            ));
            assert_eq!(
                geometry.expanded_host.logical_size.width,
                expected["expandedHost"]["logicalSize"]["width"]
            );
            assert_eq!(
                geometry.expanded_host.logical_size.height,
                expected["expandedHost"]["logicalSize"]["height"]
            );
            assert_eq!(
                geometry.collapsed_host.logical_size.height,
                expected["collapsedHost"]["logicalSize"]["height"]
            );
            assert_eq!(
                geometry.expanded_host.offset, geometry.collapsed_host.offset,
                "{name}"
            );
            assert!(close(
                geometry.normalized_rect.x,
                expected["normalizedRect"]["x"].as_f64().unwrap()
            ));
            assert!(close(
                geometry.normalized_rect.y,
                expected["normalizedRect"]["y"].as_f64().unwrap()
            ));
        }
    }
}
