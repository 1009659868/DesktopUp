use std::{
    fs,
    path::{Path, PathBuf},
    sync::{mpsc::sync_channel, Arc, Mutex},
    thread,
    time::Duration,
};

use serde::{Deserialize, Serialize};

const PLACEMENT_VERSION: u32 = 1;
const PERSIST_DEBOUNCE_MS: u64 = 300;

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PosterPlacement {
    pub version: u32,
    pub monitor_device: Option<String>,
    pub anchor_x: f64,
    pub anchor_y: f64,
    pub fallback_x: i32,
    pub fallback_y: i32,
    pub width: Option<u32>,
}

impl PosterPlacement {
    pub fn new(
        monitor_device: Option<String>,
        anchor_x: f64,
        anchor_y: f64,
        fallback_x: i32,
        fallback_y: i32,
    ) -> Self {
        Self {
            version: PLACEMENT_VERSION,
            monitor_device,
            anchor_x: anchor_x.clamp(0.0, 1.0),
            anchor_y: anchor_y.clamp(0.0, 1.0),
            fallback_x,
            fallback_y,
            width: None,
        }
    }

    pub fn with_width(mut self, width: u32) -> Self {
        self.width = Some(width);
        self
    }

    fn is_supported(&self) -> bool {
        self.version == PLACEMENT_VERSION && self.anchor_x.is_finite() && self.anchor_y.is_finite()
    }
}

pub struct PlacementStore {
    path: PathBuf,
    current: Arc<Mutex<Option<PosterPlacement>>>,
    persist_signal: std::sync::mpsc::SyncSender<()>,
}

impl PlacementStore {
    pub fn open(path: PathBuf) -> Self {
        let current = Arc::new(Mutex::new(load_placement(&path)));
        let (persist_signal, receiver) = sync_channel(1);
        let worker_path = path.clone();
        let worker_current = Arc::clone(&current);
        let _ = thread::Builder::new()
            .name("desktop-up-placement-store".to_string())
            .spawn(move || {
                while receiver.recv().is_ok() {
                    thread::sleep(Duration::from_millis(PERSIST_DEBOUNCE_MS));
                    while receiver.try_recv().is_ok() {}
                    let snapshot = worker_current.lock().ok().and_then(|value| value.clone());
                    if let Some(snapshot) = snapshot {
                        if let Err(error) = write_placement(&worker_path, &snapshot) {
                            eprintln!("failed to persist poster placement: {error}");
                        }
                    }
                }
            });

        Self {
            path,
            current,
            persist_signal,
        }
    }

    pub fn snapshot(&self) -> Option<PosterPlacement> {
        self.current.lock().ok().and_then(|value| value.clone())
    }

    pub fn record(&self, placement: PosterPlacement) {
        if let Ok(mut current) = self.current.lock() {
            *current = Some(placement);
        }
        let _ = self.persist_signal.try_send(());
    }

    pub fn flush(&self) -> Result<(), String> {
        let Some(snapshot) = self.snapshot() else {
            return Ok(());
        };
        write_placement(&self.path, &snapshot).map_err(|error| error.to_string())
    }
}

fn load_placement(path: &Path) -> Option<PosterPlacement> {
    let bytes = fs::read(path).ok()?;
    let placement = serde_json::from_slice::<PosterPlacement>(&bytes).ok()?;
    placement.is_supported().then_some(placement)
}

fn write_placement(path: &Path, placement: &PosterPlacement) -> std::io::Result<()> {
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent)?;
    }
    let bytes = serde_json::to_vec_pretty(placement).map_err(std::io::Error::other)?;
    fs::write(path, bytes)
}

#[cfg(test)]
mod tests {
    use super::PosterPlacement;

    #[test]
    fn placement_clamps_normalized_anchors() {
        let placement = PosterPlacement::new(None, -2.0, 3.0, 10, 20);
        assert_eq!(placement.anchor_x, 0.0);
        assert_eq!(placement.anchor_y, 1.0);
    }

    #[test]
    fn unsupported_or_non_finite_placement_is_rejected() {
        let mut placement = PosterPlacement::new(None, 0.5, 0.5, 10, 20);
        placement.version = 99;
        assert!(!placement.is_supported());
        placement.version = 1;
        placement.anchor_x = f64::NAN;
        assert!(!placement.is_supported());
    }
}
