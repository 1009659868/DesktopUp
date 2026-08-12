use std::{fs, io::ErrorKind, path::PathBuf};

use serde::Serialize;
use serde_json::Value;

use crate::poster_contract::{summarize_poster_contract_errors, validate_poster_contract_v1};

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum PosterSnapshotKind {
    FormalV1,
    LegacyA0,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum PosterSnapshotLoadRejectionCode {
    ReadFailed,
    CorruptJson,
    InvalidSnapshot,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
#[serde(
    tag = "status",
    rename_all = "camelCase",
    rename_all_fields = "camelCase"
)]
pub enum PosterSnapshotLoadResult {
    NoData,
    Loaded {
        kind: PosterSnapshotKind,
        snapshot: Value,
    },
    Rejected {
        code: PosterSnapshotLoadRejectionCode,
        message: String,
    },
}

pub struct PosterSnapshotStore {
    path: PathBuf,
}

impl PosterSnapshotStore {
    pub fn open(path: PathBuf) -> Self {
        Self { path }
    }

    pub fn inspect(&self) -> PosterSnapshotLoadResult {
        inspect_snapshot(&self.path)
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

fn inspect_snapshot(path: &std::path::Path) -> PosterSnapshotLoadResult {
    let bytes = match fs::read(path) {
        Ok(bytes) => bytes,
        Err(error) if error.kind() == ErrorKind::NotFound => {
            return PosterSnapshotLoadResult::NoData;
        }
        Err(error) => {
            return PosterSnapshotLoadResult::Rejected {
                code: PosterSnapshotLoadRejectionCode::ReadFailed,
                message: format!("failed to read {}: {error}", path.display()),
            };
        }
    };
    let snapshot = match serde_json::from_slice::<Value>(&bytes) {
        Ok(snapshot) => snapshot,
        Err(error) => {
            return PosterSnapshotLoadResult::Rejected {
                code: PosterSnapshotLoadRejectionCode::CorruptJson,
                message: error.to_string(),
            };
        }
    };
    match classify_supported_snapshot(&snapshot) {
        Ok(kind) => PosterSnapshotLoadResult::Loaded { kind, snapshot },
        Err(message) => PosterSnapshotLoadResult::Rejected {
            code: PosterSnapshotLoadRejectionCode::InvalidSnapshot,
            message,
        },
    }
}

#[cfg(test)]
mod tests {
    use std::{
        fs,
        time::{SystemTime, UNIX_EPOCH},
    };

    use serde_json::json;

    use super::{
        classify_supported_snapshot, PosterSnapshotKind, PosterSnapshotLoadRejectionCode,
        PosterSnapshotLoadResult, PosterSnapshotStore,
    };

    fn valid_formal_snapshot() -> serde_json::Value {
        json!({
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
        })
    }

    fn temporary_path(label: &str) -> PathBuf {
        let unique = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("system clock")
            .as_nanos();
        std::env::temp_dir().join(format!(
            "desktop-up-{label}-{}-{unique}.json",
            std::process::id()
        ))
    }

    use std::path::PathBuf;

    #[test]
    fn snapshot_requires_a_posters_array() {
        assert_eq!(
            classify_supported_snapshot(&json!({ "posters": [] })),
            Ok(PosterSnapshotKind::LegacyA0)
        );
        assert!(classify_supported_snapshot(&json!({ "posters": {} })).is_err());
        assert!(classify_supported_snapshot(&json!({})).is_err());
    }

    #[test]
    fn formal_snapshots_cannot_fall_back_to_legacy_array_validation() {
        assert!(classify_supported_snapshot(&json!({
            "contractVersion": 99,
            "posters": []
        }))
        .is_err());
    }

    #[test]
    fn valid_formal_snapshot_is_classified_without_legacy_fallback() {
        assert_eq!(
            classify_supported_snapshot(&valid_formal_snapshot()),
            Ok(PosterSnapshotKind::FormalV1)
        );
    }

    #[test]
    fn read_only_inspection_distinguishes_absent_corrupt_legacy_and_formal_sources() {
        let path = temporary_path("legacy-inspection");
        let store = PosterSnapshotStore::open(path.clone());
        assert_eq!(store.inspect(), PosterSnapshotLoadResult::NoData);

        fs::write(&path, b"{").expect("corrupt source");
        assert!(matches!(
            store.inspect(),
            PosterSnapshotLoadResult::Rejected {
                code: PosterSnapshotLoadRejectionCode::CorruptJson,
                ..
            }
        ));

        fs::write(
            &path,
            serde_json::to_vec(&json!({ "posters": [{ "id": "legacy" }] })).expect("legacy JSON"),
        )
        .expect("legacy source");
        assert!(matches!(
            store.inspect(),
            PosterSnapshotLoadResult::Loaded {
                kind: PosterSnapshotKind::LegacyA0,
                ..
            }
        ));

        fs::write(
            &path,
            serde_json::to_vec(&valid_formal_snapshot()).expect("formal JSON"),
        )
        .expect("formal source");
        assert!(matches!(
            store.inspect(),
            PosterSnapshotLoadResult::Loaded {
                kind: PosterSnapshotKind::FormalV1,
                ..
            }
        ));
        fs::remove_file(path).expect("remove inspection source");
    }
}
