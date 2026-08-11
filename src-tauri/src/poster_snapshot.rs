use std::{
    fs,
    path::{Path, PathBuf},
    sync::Mutex,
};

use serde_json::Value;

use crate::poster_contract::{summarize_poster_contract_errors, validate_poster_contract_v1};

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum PosterSnapshotKind {
    FormalV1,
    LegacyA0,
}

pub struct PosterSnapshotStore {
    path: PathBuf,
    current: Mutex<Option<Value>>,
}

impl PosterSnapshotStore {
    pub fn open(path: PathBuf) -> Self {
        let current = Mutex::new(load_snapshot(&path));
        Self { path, current }
    }

    pub fn snapshot(&self) -> Option<Value> {
        self.current.lock().ok().and_then(|value| value.clone())
    }

    pub fn replace(&self, snapshot: Value) -> Result<(), String> {
        classify_supported_snapshot(&snapshot)?;
        let mut current = self.current.lock().map_err(|error| error.to_string())?;
        write_snapshot(&self.path, &snapshot).map_err(|error| error.to_string())?;
        *current = Some(snapshot);
        Ok(())
    }
}

fn classify_supported_snapshot(snapshot: &Value) -> Result<PosterSnapshotKind, String> {
    if snapshot.get("contractVersion").is_some() {
        validate_poster_contract_v1(snapshot)
            .map_err(|errors| summarize_poster_contract_errors(&errors))?;
        return Ok(PosterSnapshotKind::FormalV1);
    }

    snapshot
        .get("posters")
        .and_then(Value::as_array)
        .map(|_| PosterSnapshotKind::LegacyA0)
        .ok_or_else(|| "legacy poster snapshot must contain a posters array".to_string())
}

fn load_snapshot(path: &Path) -> Option<Value> {
    let bytes = fs::read(path).ok()?;
    let snapshot = serde_json::from_slice::<Value>(&bytes).ok()?;
    classify_supported_snapshot(&snapshot)
        .ok()
        .map(|_| snapshot)
}

fn write_snapshot(path: &Path, snapshot: &Value) -> std::io::Result<()> {
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent)?;
    }
    let bytes = serde_json::to_vec_pretty(snapshot).map_err(std::io::Error::other)?;
    fs::write(path, bytes)
}

#[cfg(test)]
mod tests {
    use super::{classify_supported_snapshot, PosterSnapshotKind};

    #[test]
    fn snapshot_requires_a_posters_array() {
        assert_eq!(
            classify_supported_snapshot(&serde_json::json!({ "posters": [] })),
            Ok(PosterSnapshotKind::LegacyA0)
        );
        assert!(classify_supported_snapshot(&serde_json::json!({ "posters": {} })).is_err());
        assert!(classify_supported_snapshot(&serde_json::json!({})).is_err());
    }

    #[test]
    fn formal_snapshots_cannot_fall_back_to_legacy_array_validation() {
        assert!(classify_supported_snapshot(&serde_json::json!({
            "contractVersion": 99,
            "posters": []
        }))
        .is_err());
    }

    #[test]
    fn valid_formal_snapshot_is_classified_without_legacy_fallback() {
        let snapshot = serde_json::json!({
            "contractVersion": 1,
            "revision": 0,
            "selectedPosterId": "poster-primary",
            "posters": [{
                "id": "poster-primary",
                "title": "DESKTOP COLLECTION",
                "subtitle": "",
                "monitorIdentity": "primary",
                "anchor": "top-right",
                "normalizedRect": { "x": 1.0, "y": 0.0 },
                "width": 490,
                "height": 392,
                "collapsedState": "expanded",
                "autoCollapseEnabled": false,
                "autoCollapseDelaySeconds": 3,
                "isLocked": false,
                "layoutKind": "grid",
                "layoutSpec": {},
                "templateKey": "transparent-default"
            }]
        });
        assert_eq!(
            classify_supported_snapshot(&snapshot),
            Ok(PosterSnapshotKind::FormalV1)
        );
    }
}
