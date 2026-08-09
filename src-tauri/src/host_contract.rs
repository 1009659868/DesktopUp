use serde::{Deserialize, Serialize};

pub const HOST_CONTRACT_VERSION: u32 = 1;
const MAX_SURFACE_LOGICAL_EXTENT: f64 = 8192.0;
const MAX_IDENTIFIER_LENGTH: usize = 96;

fn default_user_scale() -> f64 {
    1.0
}

#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct LogicalSize {
    pub width: f64,
    pub height: f64,
}

impl Default for LogicalSize {
    fn default() -> Self {
        Self {
            width: 1.0,
            height: 1.0,
        }
    }
}

#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PhysicalSize {
    pub width: u32,
    pub height: u32,
}

impl Default for PhysicalSize {
    fn default() -> Self {
        Self {
            width: 1,
            height: 1,
        }
    }
}

#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct LogicalRect {
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct InteractionRegion {
    pub region_id: String,
    pub rect: LogicalRect,
    pub corner_radius: f64,
}

#[derive(Clone, Debug, Default, Deserialize, PartialEq, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum HostPlacementIntent {
    #[default]
    Preserve,
    Anchor {
        display_id: Option<String>,
        normalized_x: f64,
        normalized_y: f64,
    },
}

#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct HostSurfaceRequest {
    pub contract_version: u32,
    pub revision: u64,
    pub surface_id: String,
    pub logical_size: LogicalSize,
    pub scale_factor: f64,
    #[serde(default = "default_user_scale")]
    pub user_scale: f64,
    #[serde(default)]
    pub placement_intent: HostPlacementIntent,
    pub interaction_region: Option<InteractionRegion>,
}

impl HostSurfaceRequest {
    pub fn validate(self) -> Result<ValidatedHostSurface, String> {
        if self.contract_version != HOST_CONTRACT_VERSION {
            return Err(format!(
                "unsupported host contract version {}; expected {}",
                self.contract_version, HOST_CONTRACT_VERSION
            ));
        }
        if self.revision == 0 {
            return Err("host surface revision must be greater than zero".to_string());
        }
        validate_identifier("surfaceId", &self.surface_id)?;
        validate_extent("logicalSize.width", self.logical_size.width)?;
        validate_extent("logicalSize.height", self.logical_size.height)?;
        if !self.scale_factor.is_finite() || !(0.5..=4.0).contains(&self.scale_factor) {
            return Err("scaleFactor must be finite and between 0.5 and 4.0".to_string());
        }
        if !self.user_scale.is_finite() || !(0.5..=2.0).contains(&self.user_scale) {
            return Err("userScale must be finite and between 0.5 and 2.0".to_string());
        }
        validate_placement_intent(&self.placement_intent)?;
        if let Some(region) = self.interaction_region.as_ref() {
            validate_region(region, &self.logical_size)?;
        }

        let effective_scale_factor = self.scale_factor * self.user_scale;
        let physical_size = physical_size(&self.logical_size, effective_scale_factor);
        Ok(ValidatedHostSurface {
            target_display_id: None,
            monitor_scale_factor: self.scale_factor,
            device_scale_factor: self.scale_factor,
            renderer_scale_factor: self.scale_factor,
            responsive_scale: 1.0,
            user_scale: self.user_scale,
            effective_scale_factor,
            request: self,
            physical_size,
        })
    }
}

fn validate_identifier(field: &str, value: &str) -> Result<(), String> {
    if value.is_empty() || value.len() > MAX_IDENTIFIER_LENGTH {
        return Err(format!(
            "{field} must contain between 1 and {MAX_IDENTIFIER_LENGTH} characters"
        ));
    }
    if !value
        .chars()
        .all(|character| character.is_ascii_alphanumeric() || matches!(character, '-' | '_' | '.'))
    {
        return Err(format!(
            "{field} may contain only ASCII letters, digits, dash, underscore, and dot"
        ));
    }
    Ok(())
}

fn validate_opaque_identifier(field: &str, value: &str) -> Result<(), String> {
    if value.is_empty()
        || value.len() > MAX_IDENTIFIER_LENGTH
        || value.chars().any(char::is_control)
    {
        return Err(format!(
            "{field} must contain between 1 and {MAX_IDENTIFIER_LENGTH} non-control characters"
        ));
    }
    Ok(())
}

fn validate_extent(field: &str, value: f64) -> Result<(), String> {
    if !value.is_finite() || !(1.0..=MAX_SURFACE_LOGICAL_EXTENT).contains(&value) {
        return Err(format!(
            "{field} must be finite and between 1 and {MAX_SURFACE_LOGICAL_EXTENT}"
        ));
    }
    Ok(())
}

fn validate_placement_intent(intent: &HostPlacementIntent) -> Result<(), String> {
    let HostPlacementIntent::Anchor {
        display_id,
        normalized_x,
        normalized_y,
    } = intent
    else {
        return Ok(());
    };
    if let Some(display_id) = display_id {
        validate_opaque_identifier("placementIntent.displayId", display_id)?;
    }
    for (field, value) in [
        ("placementIntent.normalizedX", *normalized_x),
        ("placementIntent.normalizedY", *normalized_y),
    ] {
        if !value.is_finite() || !(0.0..=1.0).contains(&value) {
            return Err(format!("{field} must be finite and between 0 and 1"));
        }
    }
    Ok(())
}

fn validate_region(region: &InteractionRegion, surface: &LogicalSize) -> Result<(), String> {
    validate_identifier("interactionRegion.regionId", &region.region_id)?;
    let values = [
        ("interactionRegion.rect.x", region.rect.x),
        ("interactionRegion.rect.y", region.rect.y),
        ("interactionRegion.rect.width", region.rect.width),
        ("interactionRegion.rect.height", region.rect.height),
        ("interactionRegion.cornerRadius", region.corner_radius),
    ];
    if values.iter().any(|(_, value)| !value.is_finite()) {
        return Err("interaction region values must be finite".to_string());
    }
    if region.rect.x < 0.0 || region.rect.y < 0.0 {
        return Err("interaction region origin must be inside the host surface".to_string());
    }
    if region.rect.width <= 0.0 || region.rect.height <= 0.0 {
        return Err("interaction region width and height must be greater than zero".to_string());
    }
    let right = region.rect.x + region.rect.width;
    let bottom = region.rect.y + region.rect.height;
    if right > surface.width + f64::EPSILON || bottom > surface.height + f64::EPSILON {
        return Err("interaction region must fit inside logicalSize".to_string());
    }
    let max_radius = region.rect.width.min(region.rect.height) / 2.0;
    if region.corner_radius < 0.0 || region.corner_radius > max_radius {
        return Err("interaction region cornerRadius exceeds its rectangle".to_string());
    }
    Ok(())
}

fn physical_extent(logical: f64, scale: f64) -> u32 {
    (logical * scale).round().clamp(1.0, u32::MAX as f64) as u32
}

fn physical_size(logical: &LogicalSize, scale: f64) -> PhysicalSize {
    PhysicalSize {
        width: physical_extent(logical.width, scale),
        height: physical_extent(logical.height, scale),
    }
}

#[derive(Clone, Debug)]
pub struct ValidatedHostSurface {
    pub request: HostSurfaceRequest,
    pub physical_size: PhysicalSize,
    pub target_display_id: Option<String>,
    pub monitor_scale_factor: f64,
    pub device_scale_factor: f64,
    pub renderer_scale_factor: f64,
    pub responsive_scale: f64,
    pub user_scale: f64,
    pub effective_scale_factor: f64,
}

impl ValidatedHostSurface {
    pub fn resolve_display_scale(
        mut self,
        target_display_id: Option<String>,
        monitor_scale_factor: f64,
        renderer_scale_factor: f64,
        responsive_scale: f64,
    ) -> Result<Self, String> {
        if !monitor_scale_factor.is_finite() || !(0.5..=4.0).contains(&monitor_scale_factor) {
            return Err(
                "resolved monitorScaleFactor must be finite and between 0.5 and 4.0".to_string(),
            );
        }
        if !renderer_scale_factor.is_finite() || !(0.5..=4.0).contains(&renderer_scale_factor) {
            return Err(
                "resolved rendererScaleFactor must be finite and between 0.5 and 4.0".to_string(),
            );
        }
        if !responsive_scale.is_finite() || !(0.5..=2.0).contains(&responsive_scale) {
            return Err(
                "resolved responsiveScale must be finite and between 0.5 and 2.0".to_string(),
            );
        }
        let effective_scale_factor =
            renderer_scale_factor * responsive_scale * self.request.user_scale;
        if !effective_scale_factor.is_finite() || !(0.125..=16.0).contains(&effective_scale_factor)
        {
            return Err(
                "resolved effectiveScaleFactor is outside the supported envelope".to_string(),
            );
        }
        self.target_display_id = target_display_id;
        self.monitor_scale_factor = monitor_scale_factor;
        self.device_scale_factor = renderer_scale_factor;
        self.renderer_scale_factor = renderer_scale_factor;
        self.responsive_scale = responsive_scale;
        self.user_scale = self.request.user_scale;
        self.effective_scale_factor = effective_scale_factor;
        self.physical_size = physical_size(&self.request.logical_size, effective_scale_factor);
        Ok(self)
    }
}

#[derive(Clone, Debug, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct HostSurfaceSnapshot {
    pub contract_version: u32,
    pub revision: u64,
    pub surface_id: String,
    pub geometry_owner: String,
    pub logical_size: LogicalSize,
    pub physical_size: PhysicalSize,
    pub scale_factor: f64,
    pub target_display_id: Option<String>,
    pub monitor_scale_factor: f64,
    pub device_scale_factor: f64,
    pub renderer_scale_factor: f64,
    pub responsive_scale: f64,
    pub user_scale: f64,
    pub effective_scale_factor: f64,
    pub placement_intent: HostPlacementIntent,
    pub interaction_region: Option<InteractionRegion>,
}

impl Default for HostSurfaceSnapshot {
    fn default() -> Self {
        Self {
            contract_version: HOST_CONTRACT_VERSION,
            revision: 0,
            surface_id: "poster-host".to_string(),
            geometry_owner: "nativeHost".to_string(),
            logical_size: LogicalSize::default(),
            physical_size: PhysicalSize::default(),
            scale_factor: 1.0,
            target_display_id: None,
            monitor_scale_factor: 1.0,
            device_scale_factor: 1.0,
            renderer_scale_factor: 1.0,
            responsive_scale: 1.0,
            user_scale: 1.0,
            effective_scale_factor: 1.0,
            placement_intent: HostPlacementIntent::default(),
            interaction_region: None,
        }
    }
}

impl From<ValidatedHostSurface> for HostSurfaceSnapshot {
    fn from(validated: ValidatedHostSurface) -> Self {
        Self {
            contract_version: HOST_CONTRACT_VERSION,
            revision: validated.request.revision,
            surface_id: validated.request.surface_id,
            geometry_owner: "nativeHost".to_string(),
            logical_size: validated.request.logical_size,
            physical_size: validated.physical_size,
            scale_factor: validated.effective_scale_factor,
            target_display_id: validated.target_display_id,
            monitor_scale_factor: validated.monitor_scale_factor,
            device_scale_factor: validated.device_scale_factor,
            renderer_scale_factor: validated.renderer_scale_factor,
            responsive_scale: validated.responsive_scale,
            user_scale: validated.user_scale,
            effective_scale_factor: validated.effective_scale_factor,
            placement_intent: validated.request.placement_intent,
            interaction_region: validated.request.interaction_region,
        }
    }
}

#[derive(Clone, Debug, Default, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PhysicalRect {
    pub x: i32,
    pub y: i32,
    pub width: u32,
    pub height: u32,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct DisplayDescriptor {
    pub display_id: String,
    pub is_primary: bool,
    pub scale_factor: f64,
    pub renderer_scale_factor: f64,
    pub bounds: PhysicalRect,
    pub work_area: PhysicalRect,
    pub effective_logical_work_area: LogicalSize,
    pub responsive_scale: f64,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ResponsiveScalePolicy {
    pub reference_logical_size: LogicalSize,
    pub minimum_scale: f64,
    pub maximum_scale: f64,
    pub baseline_snap_tolerance: f64,
}

impl Default for ResponsiveScalePolicy {
    fn default() -> Self {
        Self {
            reference_logical_size: LogicalSize {
                width: 1920.0,
                height: 1080.0,
            },
            minimum_scale: 0.8,
            maximum_scale: 1.25,
            baseline_snap_tolerance: 0.05,
        }
    }
}

#[derive(Clone, Debug, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct DisplayTopology {
    pub contract_version: u32,
    pub coordinate_space: String,
    pub virtual_bounds: PhysicalRect,
    pub scale_policy: ResponsiveScalePolicy,
    pub displays: Vec<DisplayDescriptor>,
}

impl Default for DisplayTopology {
    fn default() -> Self {
        Self {
            contract_version: HOST_CONTRACT_VERSION,
            coordinate_space: "physicalPixels".to_string(),
            virtual_bounds: PhysicalRect::default(),
            scale_policy: ResponsiveScalePolicy::default(),
            displays: Vec::new(),
        }
    }
}

pub fn virtual_bounds(displays: &[DisplayDescriptor]) -> PhysicalRect {
    let Some(first) = displays.first() else {
        return PhysicalRect::default();
    };
    let mut left = first.bounds.x as i64;
    let mut top = first.bounds.y as i64;
    let mut right = left + first.bounds.width as i64;
    let mut bottom = top + first.bounds.height as i64;
    for display in displays.iter().skip(1) {
        left = left.min(display.bounds.x as i64);
        top = top.min(display.bounds.y as i64);
        right = right.max(display.bounds.x as i64 + display.bounds.width as i64);
        bottom = bottom.max(display.bounds.y as i64 + display.bounds.height as i64);
    }
    PhysicalRect {
        x: left.clamp(i32::MIN as i64, i32::MAX as i64) as i32,
        y: top.clamp(i32::MIN as i64, i32::MAX as i64) as i32,
        width: (right - left).clamp(0, u32::MAX as i64) as u32,
        height: (bottom - top).clamp(0, u32::MAX as i64) as u32,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn valid_request() -> HostSurfaceRequest {
        HostSurfaceRequest {
            contract_version: HOST_CONTRACT_VERSION,
            revision: 1,
            surface_id: "poster-host".to_string(),
            logical_size: LogicalSize {
                width: 510.0,
                height: 412.0,
            },
            scale_factor: 1.25,
            user_scale: 1.0,
            placement_intent: HostPlacementIntent::Preserve,
            interaction_region: Some(InteractionRegion {
                region_id: "poster-primary".to_string(),
                rect: LogicalRect {
                    x: 10.0,
                    y: 10.0,
                    width: 490.0,
                    height: 392.0,
                },
                corner_radius: 18.0,
            }),
        }
    }

    #[test]
    fn request_serializes_with_versioned_camel_case_contract() {
        let value = serde_json::to_value(valid_request()).unwrap();
        assert_eq!(value["contractVersion"], HOST_CONTRACT_VERSION);
        assert_eq!(value["surfaceId"], "poster-host");
        assert_eq!(value["placementIntent"]["kind"], "preserve");
        assert_eq!(value["interactionRegion"]["regionId"], "poster-primary");
    }

    #[test]
    fn request_validation_rejects_unknown_versions_and_invalid_regions() {
        let mut request = valid_request();
        request.contract_version = 2;
        assert!(request.validate().unwrap_err().contains("unsupported"));

        let mut request = valid_request();
        request.interaction_region.as_mut().unwrap().rect.width = 900.0;
        assert!(request.validate().unwrap_err().contains("fit inside"));

        let mut request = valid_request();
        request.scale_factor = f64::NAN;
        assert!(request.validate().unwrap_err().contains("scaleFactor"));
    }

    #[test]
    fn windows_display_names_round_trip_as_opaque_identifiers() {
        let mut request = valid_request();
        request.placement_intent = HostPlacementIntent::Anchor {
            display_id: Some(r"\\.\DISPLAY1".to_string()),
            normalized_x: 1.0,
            normalized_y: 0.0,
        };
        assert!(request.validate().is_ok());
    }

    #[test]
    fn validated_request_produces_native_owned_physical_snapshot() {
        let snapshot = HostSurfaceSnapshot::from(valid_request().validate().unwrap());
        assert_eq!(snapshot.geometry_owner, "nativeHost");
        assert_eq!(snapshot.physical_size.width, 638);
        assert_eq!(snapshot.physical_size.height, 515);
        assert_eq!(snapshot.revision, 1);
    }

    #[test]
    fn native_display_resolution_combines_device_responsive_and_user_scale_once() {
        let mut request = valid_request();
        request.user_scale = 0.8;
        let validated = request
            .validate()
            .unwrap()
            .resolve_display_scale(Some("primary".to_string()), 1.0, 1.5, 1.25)
            .unwrap();
        let snapshot = HostSurfaceSnapshot::from(validated);
        assert_eq!(snapshot.target_display_id.as_deref(), Some("primary"));
        assert_eq!(snapshot.monitor_scale_factor, 1.0);
        assert_eq!(snapshot.device_scale_factor, 1.5);
        assert_eq!(snapshot.renderer_scale_factor, 1.5);
        assert_eq!(snapshot.responsive_scale, 1.25);
        assert_eq!(snapshot.user_scale, 0.8);
        assert_eq!(snapshot.effective_scale_factor, 1.5);
        assert_eq!(snapshot.physical_size.width, 765);
        assert_eq!(snapshot.physical_size.height, 618);
    }

    #[test]
    fn virtual_bounds_include_negative_and_secondary_display_coordinates() {
        let displays = vec![
            DisplayDescriptor {
                display_id: "primary".to_string(),
                is_primary: true,
                scale_factor: 1.0,
                renderer_scale_factor: 1.0,
                bounds: PhysicalRect {
                    x: 0,
                    y: 0,
                    width: 1920,
                    height: 1080,
                },
                work_area: PhysicalRect::default(),
                effective_logical_work_area: LogicalSize {
                    width: 1920.0,
                    height: 1080.0,
                },
                responsive_scale: 1.0,
            },
            DisplayDescriptor {
                display_id: "left".to_string(),
                is_primary: false,
                scale_factor: 1.25,
                renderer_scale_factor: 1.25,
                bounds: PhysicalRect {
                    x: -2560,
                    y: -200,
                    width: 2560,
                    height: 1440,
                },
                work_area: PhysicalRect::default(),
                effective_logical_work_area: LogicalSize {
                    width: 2048.0,
                    height: 1152.0,
                },
                responsive_scale: 1.067,
            },
        ];
        assert_eq!(
            virtual_bounds(&displays),
            PhysicalRect {
                x: -2560,
                y: -200,
                width: 4480,
                height: 1440,
            }
        );
    }
}
