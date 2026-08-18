// DesktopUp V2 — C-QTP 核心桩(Rust)
// 职责:启动/重启渲染侧车进程、命名管道 IPC、看门狗、呈现自检。
use std::io::{Read, Write};
use std::process::{Child, Command};
use std::sync::mpsc::{channel, Sender};
use std::thread;
use std::time::{Duration, Instant};

use windows::core::{s, w, PCWSTR};
use windows::Win32::Foundation::{CloseHandle, HANDLE, INVALID_HANDLE_VALUE};
use windows::Win32::Storage::FileSystem::{ReadFile, WriteFile};
use windows::Win32::System::Pipes::{ConnectNamedPipe, CreateNamedPipe, DisconnectNamedPipe};
use windows::Win32::System::Threading::{CreateEventW, SetEvent, WaitForSingleObject, WAIT_OBJECT_0};

const PIPE_NAME: &str = r"\\.\pipe\desktopup-qtp";
const MAX_RESTARTS: u32 = 3;

fn main() {
    let renderer = std::env::args()
        .nth(1)
        .unwrap_or_else(|| r"G:\A_\Tools\DesktopUp_V2\prototype\qt-quick\renderer\build\poster-renderer.exe".to_string());

    println!("[core] starting renderer: {renderer}");
    let mut child = spawn_renderer(&renderer);
    let mut restarts = 0u32;

    loop {
        match run_server_session() {
            Ok(SessionOutcome::RendererExited) => {
                if let Some(exit) = child.as_mut().and_then(|c| c.try_wait().ok().flatten()) {
                    println!("[core] renderer exited: {exit:?}");
                }
                if restarts >= MAX_RESTARTS {
                    println!("[core] renderer restart limit reached; giving up");
                    return;
                }
                restarts += 1;
                println!("[core] restarting renderer ({restarts}/{MAX_RESTARTS})");
                child = spawn_renderer(&renderer);
            }
            Ok(SessionOutcome::Shutdown) => {
                println!("[core] shutdown requested");
                let _ = child.as_mut().map(|c| c.kill());
                let _ = child.as_mut().map(|c| c.wait());
                return;
            }
            Err(message) => {
                eprintln!("[core] pipe session failed: {message}");
                return;
            }
        }
    }
}

enum SessionOutcome {
    RendererExited,
    Shutdown,
}

fn spawn_renderer(exe: &str) -> Option<Child> {
    Command::new(exe).arg(PIPE_NAME).spawn().ok()
}

// ---- named pipe session ----

fn run_server_session() -> Result<SessionOutcome, String> {
    let pipe = unsafe {
        CreateNamedPipeW(
            w!(PIPE_NAME),
            windows::Win32::System::Pipes::PIPE_ACCESS_DUPLEX,
            windows::Win32::System::Pipes::PIPE_TYPE_MESSAGE
                | windows::Win32::System::Pipes::PIPE_READMODE_MESSAGE
                | windows::Win32::System::Pipes::PIPE_WAIT,
            1,
            64 * 1024,
            64 * 1024,
            0,
            None,
        )
    }
    .map_err(|e| format!("CreateNamedPipe failed: {e}"))?;

    let quit = unsafe { CreateEventW(None, true, false, None) }
        .map_err(|e| format!("CreateEvent failed: {e}"))?;

    let pipe2 = pipe;
    let quit2 = quit;
    thread::spawn(move || unsafe {
        let wait = ConnectNamedPipe(pipe2, None);
        if !wait.as_bool() {
            SetEvent(quit2).unwrap_or(());
        }
    });

    println!("[core] waiting for renderer to connect...");
    let deadline = Instant::now() + Duration::from_secs(15);
    loop {
        let wait = unsafe { WaitForSingleObject(quit, 50) };
        if wait == WAIT_OBJECT_0 {
            break;
        }
        if Instant::now() > deadline {
            return Err("renderer did not connect within 15s".into());
        }
    }

    println!("[core] renderer connected");
    let mut next_id: u64 = 1;
    let (tx, rx) = channel::<String>();
    start_reader(pipe, tx);

    let mut iter = 0u64;
    loop {
        // periodic health check driven by the core
        iter += 1;
        if iter % 40 == 0 {
            let health = health_probe(pipe)?;
            println!("[core] health: {health}");
        }

        if let Ok(cmd) = rx.try_recv() {
            let request: serde_json::Value =
                serde_json::from_str(&cmd).unwrap_or(serde_json::Value::Null);
            match request.get("cmd").and_then(|v| v.as_str()) {
                Some("shutdown") | Some("bye") => {
                    let _ = send_json(pipe, &serde_json::json!({"ok": true, "id": request.get("id")}));
                    return Ok(SessionOutcome::Shutdown);
                }
                Some("get_health") => {
                    let health = health_probe(pipe)?;
                    let _ = send_json(
                        pipe,
                        &serde_json::json!({"ok": true, "health": health, "id": request.get("id")}),
                    );
                }
                _ => {
                    let id = next_id;
                    next_id += 1;
                    if send_json(pipe, &serde_json::json!({"cmd": "ping", "id": id})).is_err() {
                        return Ok(SessionOutcome::RendererExited);
                    }
                }
            }
        }

        let wait = unsafe { WaitForSingleObject(quit, 30) };
        if wait == WAIT_OBJECT_0 {
            // pipe connected; check for broken pipe by testing a write
        }
        thread::sleep(Duration::from_millis(30));
    }
}

fn start_reader(pipe: HANDLE, tx: Sender<String>) {
    thread::spawn(move || {
        let mut buf = vec![0u8; 64 * 1024];
        loop {
            let mut read = 0u32;
            let ok = unsafe {
                ReadFile(
                    pipe,
                    Some(buf.as_mut_slice()),
                    Some(&mut read),
                    None,
                )
            };
            if ok.is_err() || read == 0 {
                break;
            }
            if let Ok(text) = String::from_utf8(buf[..read as usize].to_vec()) {
                let _ = tx.send(text);
            }
        }
    });
}

fn send_json(pipe: HANDLE, value: &serde_json::Value) -> Result<(), String> {
    let mut bytes = serde_json::to_vec(value).map_err(|e| e.to_string())?;
    let len = (bytes.len() as u32).to_le_bytes();
    bytes.splice(0..0, len.iter().copied());
    let mut written = 0u32;
    unsafe { WriteFile(pipe, Some(&bytes), Some(&mut written), None) }
        .map_err(|e| format!("WriteFile failed: {e}"))?;
    Ok(())
}

// ---- presentation health: sample the on-screen health marker ----

fn health_probe(pipe: HANDLE) -> Result<serde_json::Value, String> {
    // 1) ask the renderer for its window rect and marker position
    send_json(
        pipe,
        &serde_json::json!({"cmd": "get_geometry"}),
    )?;
    // response is async; for the skeleton we use a fixed known geometry below.
    // 2) screen-capture the marker region and compare with the expected color.
    let expected = "HEALTH MARKER SAMPLE";
    let _ = expected;
    Ok(serde_json::json!({"renderer": "alive", "screen_match": null}))
}

// keep unused imports quiet in the skeleton
#[allow(unused)]
fn unused_guards() {
    let _ = s!("unused");
    let _ = PCWSTR::null();
    let _ = INVALID_HANDLE_VALUE;
    let _ = DisconnectNamedPipe;
    let _ = CloseHandle;
}
