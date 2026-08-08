use std::{
    fs,
    path::{Path, PathBuf},
    sync::Mutex,
};

use serde_json::Value;

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
        if !is_valid_snapshot(&snapshot) {
            return Err("poster snapshot must contain a posters array".to_string());
        }
        let mut current = self.current.lock().map_err(|error| error.to_string())?;
        write_snapshot(&self.path, &snapshot).map_err(|error| error.to_string())?;
        *current = Some(snapshot);
        Ok(())
    }
}

fn is_valid_snapshot(snapshot: &Value) -> bool {
    snapshot.get("posters").and_then(Value::as_array).is_some()
}

fn load_snapshot(path: &Path) -> Option<Value> {
    let bytes = fs::read(path).ok()?;
    let snapshot = serde_json::from_slice::<Value>(&bytes).ok()?;
    is_valid_snapshot(&snapshot).then_some(snapshot)
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
    use super::is_valid_snapshot;

    #[test]
    fn snapshot_requires_a_posters_array() {
        assert!(is_valid_snapshot(&serde_json::json!({ "posters": [] })));
        assert!(!is_valid_snapshot(&serde_json::json!({ "posters": {} })));
        assert!(!is_valid_snapshot(&serde_json::json!({})));
    }
}
