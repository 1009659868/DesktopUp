// 结构化诊断:JSON lines 循环日志(B-FND 契约)
// 行格式:{"ts":<epoch秒>,"level":..,"event":..,"session":.., ...调用方字段平铺}
use std::fs::{self, OpenOptions};
use std::io::Write as _;
use std::path::{Path, PathBuf};
use std::sync::Mutex;

pub struct Diag {
    out: Mutex<std::fs::File>,
    session: String,
}

impl Diag {
    pub fn open(dir: &Path) -> Self {
        fs::create_dir_all(dir).ok();
        let secs = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .map(|d| d.as_secs())
            .unwrap_or(0);
        let session = format!("{}-{secs}", std::process::id());
        let path = dir.join(format!("core-{secs}.jsonl"));
        let file = OpenOptions::new()
            .create(true)
            .append(true)
            .open(path)
            .unwrap_or_else(|_| panic!("cannot open diag log at {}", dir.display()));
        Self {
            out: Mutex::new(file),
            session,
        }
    }

    pub fn session(&self) -> &str {
        &self.session
    }

    pub fn log(&self, level: &str, event: &str, fields: serde_json::Value) {
        let mut obj = match fields {
            serde_json::Value::Object(m) => m,
            _ => serde_json::Map::new(),
        };
        obj.insert(
            "ts".into(),
            serde_json::Value::from(
                std::time::SystemTime::now()
                    .duration_since(std::time::UNIX_EPOCH)
                    .map(|d| d.as_secs())
                    .unwrap_or(0),
            ),
        );
        obj.insert("level".into(), level.into());
        obj.insert("event".into(), event.into());
        obj.insert("session".into(), self.session.clone().into());
        let line = serde_json::Value::Object(obj).to_string();
        if let Ok(mut f) = self.out.lock() {
            let _ = writeln!(f, "{line}");
        }
    }

    #[allow(dead_code)]
    pub fn path(&self) -> PathBuf {
        PathBuf::new()
    }
}
