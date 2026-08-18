// 状态权威与修订号(B-FND 契约):写命令必须带 base_rev,冲突即拒绝(防回弹)
use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct Params {
    pub accent: String,
    pub opacity: f64,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct SurfaceState {
    pub revision: u64,
    pub geometry: (i32, i32, i32, i32),
    pub visible: bool,
    pub params: Params,
}

impl Default for SurfaceState {
    fn default() -> Self {
        Self {
            revision: 0,
            geometry: (1700, 200, 450, 703),
            visible: true,
            params: Params {
                accent: "#72e4ff".into(),
                opacity: 0.82,
            },
        }
    }
}

#[derive(Debug, Clone, PartialEq, Serialize)]
pub struct RevConflict {
    pub current: u64,
}

#[derive(Debug, Clone, Deserialize)]
#[serde(tag = "cmd", rename_all = "snake_case")]
pub enum Command {
    SetGeometry { x: i32, y: i32, w: i32, h: i32 },
    SetVisible { visible: bool },
    SetParams { accent: String, opacity: f64 },
}

impl SurfaceState {
    /// 执行写命令;base_rev 与当前修订不一致 → Err(RevConflict),状态不变。
    pub fn apply(&mut self, cmd: &Command, base_rev: u64) -> Result<(), RevConflict> {
        if base_rev != self.revision {
            return Err(RevConflict {
                current: self.revision,
            });
        }
        match cmd {
            Command::SetGeometry { x, y, w, h } => self.geometry = (*x, *y, *w, *h),
            Command::SetVisible { visible } => self.visible = *visible,
            Command::SetParams { accent, opacity } => {
                self.params.accent = accent.clone();
                self.params.opacity = *opacity;
            }
        }
        self.revision += 1;
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn cmd_geo(x: i32, y: i32) -> Command {
        Command::SetGeometry {
            x,
            y,
            w: 450,
            h: 703,
        }
    }

    #[test]
    fn apply_with_correct_base_rev_updates_and_increments() {
        let mut s = SurfaceState::default();
        s.apply(&cmd_geo(100, 100), 0).unwrap();
        assert_eq!(s.geometry, (100, 100, 450, 703));
        assert_eq!(s.revision, 1);
    }

    #[test]
    fn stale_base_rev_is_rejected_without_state_change() {
        let mut s = SurfaceState::default();
        s.apply(&cmd_geo(100, 100), 0).unwrap();
        let err = s.apply(&cmd_geo(999, 999), 0).unwrap_err();
        assert_eq!(err.current, 1);
        assert_eq!(s.geometry, (100, 100, 450, 703));
        assert_eq!(s.revision, 1);
    }

    #[test]
    fn chained_applies_advance_revision_monotonically() {
        let mut s = SurfaceState::default();
        s.apply(&cmd_geo(1, 1), 0).unwrap();
        s.apply(&Command::SetVisible { visible: false }, 1).unwrap();
        s.apply(
            &Command::SetParams {
                accent: "#ff8800".into(),
                opacity: 0.7,
            },
            2,
        )
        .unwrap();
        assert_eq!(s.revision, 3);
        assert!(!s.visible);
        assert_eq!(s.params.accent, "#ff8800");
    }

    #[test]
    fn future_base_rev_is_also_rejected() {
        let mut s = SurfaceState::default();
        assert!(s.apply(&cmd_geo(5, 5), 7).is_err());
        assert_eq!(s.revision, 0);
    }
}
