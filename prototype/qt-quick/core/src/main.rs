// DesktopUp V2 — C-QTP 核心桩(Rust)
// 职责:启动/重启渲染侧车进程、stdio IPC(子进程 stdin 下行命令 / stdout 上行回复)、看门狗。
// 传输:4 字节 LE 长度前缀 + UTF-8 JSON。
use std::collections::HashMap;
use std::io::{Read, Write};
use std::process::{Child, ChildStdin, Command, Stdio};
use std::sync::mpsc::{channel, Receiver, Sender};
use std::thread;
use std::time::Duration;

const MAX_RESTARTS: u32 = 3;

fn clog(line: &str) {
    use std::io::Write as _;
    if let Ok(mut f) = std::fs::OpenOptions::new()
        .create(true)
        .append(true)
        .open(r"G:\A_\Tools\DesktopUp_V2\prototype\qt-quick\core.log")
    {
        let _ = writeln!(f, "{line}");
    }
}

fn main() {
    let renderer = std::env::args().nth(1).unwrap_or_else(|| {
        r"G:\A_\Tools\DesktopUp_V2\prototype\qt-quick\renderer\build\poster-renderer.exe"
            .to_string()
    });

    clog(&format!("starting renderer: {renderer}"));
    let mut child = spawn_renderer(&renderer);
    let mut restarts = 0u32;

    loop {
        match child.as_mut().map(run_session) {
            None => {
                clog("failed to spawn renderer");
                return;
            }
            Some(Ok(SessionOutcome::RendererExited)) => {
                let code = child.as_mut().and_then(|c| c.try_wait().ok().flatten());
                clog(&format!("renderer exited {code:?}"));
                if restarts >= MAX_RESTARTS {
                    clog("restart limit reached; giving up");
                    return;
                }
                restarts += 1;
                clog(&format!("restarting renderer {restarts}/{MAX_RESTARTS}"));
                child = spawn_renderer(&renderer);
            }
            Some(Ok(SessionOutcome::Shutdown)) => {
                clog("shutdown requested; terminating renderer");
                if let Some(mut c) = child {
                    let _ = c.kill();
                    let _ = c.wait();
                }
                return;
            }
            Some(Err(message)) => {
                clog(&format!("session failed: {message}"));
                return;
            }
        }
    }
}

enum SessionOutcome {
    RendererExited,
    Shutdown,
}

enum Wire {
    Frame(serde_json::Value),
    Eof,
}

fn spawn_renderer(exe: &str) -> Option<Child> {
    Command::new(exe)
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .spawn()
        .ok()
}

fn run_session(child: &mut Child) -> Result<SessionOutcome, String> {
    let mut stdin = child.stdin.take().ok_or("no child stdin")?;
    let stdout = child.stdout.take().ok_or("no child stdout")?;

    // 上行:stdout 读线程,解析长度前缀帧
    let (tx, rx): (Sender<Wire>, Receiver<Wire>) = channel();
    thread::spawn(move || {
        let mut reader = stdout;
        let mut carry: Vec<u8> = Vec::new();
        let mut buf = [0u8; 64 * 1024];
        loop {
            let read = match reader.read(&mut buf) {
                Ok(0) | Err(_) => {
                    let _ = tx.send(Wire::Eof);
                    return;
                }
                Ok(n) => n,
            };
            carry.extend_from_slice(&buf[..read]);
            loop {
                if carry.len() < 4 {
                    break;
                }
                let len =
                    u32::from_le_bytes([carry[0], carry[1], carry[2], carry[3]]) as usize;
                if carry.len() < 4 + len {
                    break;
                }
                let payload = carry[4..4 + len].to_vec();
                carry.drain(..4 + len);
                if let Ok(value) = serde_json::from_slice::<serde_json::Value>(&payload) {
                    if tx.send(Wire::Frame(value)).is_err() {
                        return;
                    }
                }
            }
        }
    });

    clog("session started; waiting for renderer hello");
    let mut next_id: u64 = 1;
    let mut pending: HashMap<u64, serde_json::Value> = HashMap::new();
    let mut ticks: u64 = 0;
    let mut hellos = 0u32;

    loop {
        ticks += 1;
        if ticks % 100 == 0 {
            let id = next_id;
            next_id += 1;
            send_frame(&mut stdin, &serde_json::json!({"cmd": "get_health", "id": id}))?;
            pending.insert(id, serde_json::Value::Null);
            clog(&format!("sent get_health id={id}"));
        }
        if ticks % 300 == 150 {
            // 定期让渲染器自抓窗口帧(呈现自检素材)
            let id = next_id;
            next_id += 1;
            send_frame(&mut stdin, &serde_json::json!({"cmd": "capture", "id": id}))?;
            pending.insert(id, serde_json::Value::Null);
            clog(&format!("sent capture id={id}"));
        }

        loop {
            match rx.try_recv() {
                Ok(Wire::Eof) => return Ok(SessionOutcome::RendererExited),
                Ok(Wire::Frame(value)) => {
                    if let Some(id) = value.get("id").and_then(|v| v.as_u64()) {
                        if pending.remove(&id).is_some() {
                            clog(&format!("reply: {value}"));
                            continue;
                        }
                    }
                    match value.get("cmd").and_then(|v| v.as_str()) {
                        Some("hello") => {
                            hellos += 1;
                            clog(&format!("hello #{hellos} from renderer"));
                        }
                        Some("bye") => return Ok(SessionOutcome::RendererExited),
                        _ => clog(&format!("frame: {value}")),
                    }
                }
                Err(std::sync::mpsc::TryRecvError::Empty) => break,
                Err(std::sync::mpsc::TryRecvError::Disconnected) => {
                    return Ok(SessionOutcome::RendererExited)
                }
            }
        }

        thread::sleep(Duration::from_millis(30));
    }
}

fn send_frame(stdin: &mut ChildStdin, value: &serde_json::Value) -> Result<(), String> {
    let body = serde_json::to_vec(value).map_err(|e| e.to_string())?;
    let mut frame = Vec::with_capacity(4 + body.len());
    frame.extend_from_slice(&(body.len() as u32).to_le_bytes());
    frame.extend_from_slice(&body);
    stdin.write_all(&frame).map_err(|e| format!("write failed: {e}"))?;
    stdin.flush().map_err(|e| format!("flush failed: {e}"))?;
    Ok(())
}
