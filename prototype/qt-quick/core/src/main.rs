// DesktopUp V2 — C-QTP 核心桩(Rust)
// 职责:启动/重启渲染侧车进程、stdio IPC(子进程 stdin 下行命令 / stdout 上行回复)、看门狗。
// 传输:4 字节 LE 长度前缀 + UTF-8 JSON。
use std::collections::HashMap;
use std::io::{Read, Write};
use std::process::{Child, ChildStdin, Command, Stdio};
use std::sync::atomic::{AtomicBool, AtomicIsize, Ordering};
use std::sync::mpsc::{channel, Receiver, Sender};
use std::sync::{Mutex, OnceLock};
use std::thread;
use std::time::Duration;

use windows::core::w;
use windows::Win32::Foundation::{HWND, LPARAM, LRESULT, POINT, WPARAM};
use windows::Win32::UI::Input::KeyboardAndMouse::{
    SendInput, INPUT, INPUT_0, INPUT_MOUSE, MOUSEINPUT, MOUSEEVENTF_ABSOLUTE, MOUSEEVENTF_MOVE,
    MOUSEEVENTF_VIRTUALDESK,
};
use windows::Win32::UI::WindowsAndMessaging::{
    CallNextHookEx, DispatchMessageW, GetCursorPos, GetMessageW, GetSystemMetrics,
    GetWindowLongPtrW, SetWindowLongPtrW, SetWindowsHookExW, TranslateMessage,
    UnhookWindowsHookEx, GWL_EXSTYLE, HHOOK, MSG, MSLLHOOKSTRUCT, SM_CXVIRTUALSCREEN,
    SM_CYVIRTUALSCREEN, SM_XVIRTUALSCREEN, SM_YVIRTUALSCREEN, WH_MOUSE_LL, WM_MOUSEMOVE,
    WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW, WS_EX_TRANSPARENT,
};

const MAX_RESTARTS: u32 = 3;

// 渲染器表面信息(由 hello 帧填充,钩子线程共享)
struct Surface {
    hwnd: isize,
    x: i32,
    y: i32,
    w: i32,
    h: i32,
    regions: Vec<(i32, i32, i32, i32)>, // (x,y,w,h) 窗口坐标
}
static SURFACE: OnceLock<Mutex<Option<Surface>>> = OnceLock::new();
// 渲染端窗口初始无 WS_EX_TRANSPARENT,故初始 false,与实际样式位一致
static PASSTHROUGH_ON: AtomicBool = AtomicBool::new(false);
static HOOK_STARTED: OnceLock<()> = OnceLock::new();
static PROBE_MOUSE: AtomicBool = AtomicBool::new(false);
static PROBE_STARTED: OnceLock<()> = OnceLock::new();
static VERIFY: AtomicBool = AtomicBool::new(false);

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

fn hit_any_region(s: &Surface, px: i32, py: i32) -> bool {
    let lx = px - s.x;
    let ly = py - s.y;
    s.regions
        .iter()
        .any(|(rx, ry, rw, rh)| lx >= *rx && lx < rx + rw && ly >= *ry && ly < ry + rh)
}

fn apply_passthrough(hwnd: isize, passthrough: bool) {
    if PASSTHROUGH_ON.swap(passthrough, Ordering::SeqCst) == passthrough {
        return;
    }
    static TOGGLES: std::sync::atomic::AtomicU32 = std::sync::atomic::AtomicU32::new(0);
    let n = TOGGLES.fetch_add(1, Ordering::Relaxed) + 1;
    if n % 20 == 1 {
        clog(&format!("passthrough -> {passthrough} (toggle #{n})"));
    }
    unsafe {
        let hwnd = HWND(hwnd as *mut core::ffi::c_void);
        let style = GetWindowLongPtrW(hwnd, GWL_EXSTYLE);
        let next = if passthrough {
            style | WS_EX_TRANSPARENT.0 as isize
        } else {
            style & !(WS_EX_TRANSPARENT.0 as isize)
        };
        // 首帧:固定 ToolWindow + NoActivate(不叠 layered,不叠 blur)
        let next = next | WS_EX_TOOLWINDOW.0 as isize | WS_EX_NOACTIVATE.0 as isize;
        let _ = SetWindowLongPtrW(hwnd, GWL_EXSTYLE, next);
    }
}

unsafe extern "system" fn mouse_hook(code: i32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    static EVENTS: std::sync::atomic::AtomicU32 = std::sync::atomic::AtomicU32::new(0);
    let n = EVENTS.fetch_add(1, Ordering::Relaxed) + 1;
    if n <= 8 {
        let pt = (lparam.0 as *const MSLLHOOKSTRUCT)
            .as_ref()
            .map(|d| (d.pt.x, d.pt.y))
            .unwrap_or((-1, -1));
        clog(&format!(
            "hook event #{n} code={code} wparam=0x{:X} pt=({},{})",
            wparam.0, pt.0, pt.1
        ));
    }
    if code >= 0 && wparam.0 as u32 == WM_MOUSEMOVE {
        if let Some(details) = (lparam.0 as *const MSLLHOOKSTRUCT).as_ref() {
            let point = details.pt;
            let guard = SURFACE.get_or_init(|| Mutex::new(None)).lock().ok();
            if let Some(guard) = guard {
                if let Some(s) = guard.as_ref() {
                    let inside = point.x >= s.x
                        && point.x < s.x + s.w
                        && point.y >= s.y
                        && point.y < s.y + s.h;
                    if inside {
                        apply_passthrough(s.hwnd, !hit_any_region(s, point.x, point.y));
                    } else {
                        apply_passthrough(s.hwnd, true);
                    }
                }
            }
        }
    }
    CallNextHookEx(None, code, wparam, lparam)
}

fn run_hook_thread() {
    unsafe {
        let hook = SetWindowsHookExW(WH_MOUSE_LL, Some(mouse_hook), None, 0);
        let Ok(hook) = hook else {
            clog("failed to install mouse hook");
            return;
        };
        clog("mouse hook installed (WH_MOUSE_LL)");
        let _ = HOOK_STARTED.set(());
        let mut message = MSG::default();
        while GetMessageW(&mut message, None, 0, 0).as_bool() {
            let _ = TranslateMessage(&message);
            DispatchMessageW(&message);
        }
        let _ = UnhookWindowsHookEx(hook);
    }
}

// SendInput 注入绝对移动(WH_MOUSE_LL 可观察注入事件;SetCursorPos 直接改光标不进输入流)
unsafe fn inject_move(x: i32, y: i32, vsx: i32, vsy: i32, vsw: i32, vsh: i32) {
    let dx = ((x - vsx) as i64 * 65535 / (vsw - 1) as i64) as i32;
    let dy = ((y - vsy) as i64 * 65535 / (vsh - 1) as i64) as i32;
    let mut input = INPUT {
        r#type: INPUT_MOUSE,
        Anonymous: INPUT_0 {
            mi: MOUSEINPUT {
                dx,
                dy,
                mouseData: 0,
                dwFlags: MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK,
                time: 0,
                dwExtraInfo: 0,
            },
        },
    };
    let sent = SendInput(
        std::slice::from_ref(&input),
        std::mem::size_of::<INPUT>() as i32,
    );
    clog(&format!(
        "inject: SendInput -> {sent} (last_error={})",
        windows::Win32::Foundation::GetLastError().0
    ));
}

// --probe-mouse:程序化光标探测,验证钩子切换与穿透位真实生效(不等人工)
fn run_mouse_probe() {
    // 空白点/命中点(窗口局部坐标,来自 QML 构图):标题栏 y∈[16,74];卡片行0 y∈[168,264]
    let steps: [(&str, i32, i32); 5] = [
        ("blank", 225, 120),
        ("titlebar", 225, 45),
        ("blank", 225, 120),
        ("card", 225, 210),
        ("blank", 225, 120),
    ];
    thread::spawn(move || {
        let (hwnd, ox, oy) = {
            let mut got = None;
            for _ in 0..100 {
                if let Ok(guard) = SURFACE.get_or_init(|| Mutex::new(None)).lock() {
                    if let Some(s) = guard.as_ref() {
                        got = Some((s.hwnd, s.x, s.y));
                        break;
                    }
                }
                thread::sleep(Duration::from_millis(50));
            }
            match got {
                Some(v) => v,
                None => {
                    clog("probe: surface never registered");
                    return;
                }
            }
        };
        // 等钩子装好再注入,否则事件白白穿过
        for _ in 0..100 {
            if HOOK_STARTED.get().is_some() {
                break;
            }
            thread::sleep(Duration::from_millis(50));
        }
        if HOOK_STARTED.get().is_none() {
            clog("probe: hook never installed; aborting");
            return;
        }
        let start = unsafe {
            let mut p = POINT::default();
            if GetCursorPos(&mut p).is_ok() {
                Some((p.x, p.y))
            } else {
                None
            }
        };
        let (vsx, vsy, vsw, vsh) = unsafe {
            (
                GetSystemMetrics(SM_XVIRTUALSCREEN),
                GetSystemMetrics(SM_YVIRTUALSCREEN),
                GetSystemMetrics(SM_CXVIRTUALSCREEN),
                GetSystemMetrics(SM_CYVIRTUALSCREEN),
            )
        };
        clog(&format!(
            "probe: begin origin=({ox},{oy}) virtual=({vsx},{vsy},{vsw},{vsh})"
        ));
        for (kind, lx, ly) in steps {
            let sx = ox + lx;
            let sy = oy + ly;
            unsafe {
                inject_move(sx, sy, vsx, vsy, vsw, vsh);
            }
            thread::sleep(Duration::from_millis(300));
            let bit = unsafe {
                let style = GetWindowLongPtrW(HWND(hwnd as *mut core::ffi::c_void), GWL_EXSTYLE);
                style & WS_EX_TRANSPARENT.0 as isize != 0
            };
            clog(&format!(
                "probe step kind={kind} local=({lx},{ly}) transparent_bit={bit}"
            ));
        }
        if let Some((sx, sy)) = start {
            unsafe {
                inject_move(sx, sy, vsx, vsy, vsw, vsh);
            }
            clog(&format!("probe: cursor restored to ({sx},{sy})"));
        }
        // 阶段 B:直接施加验证(SendInput 被沙箱 UIPI 阻断时,样式切换机制仍可证明)
        let read_bit = |h: isize| unsafe {
            GetWindowLongPtrW(HWND(h as *mut core::ffi::c_void), GWL_EXSTYLE)
                & WS_EX_TRANSPARENT.0 as isize
                != 0
        };
        apply_passthrough(hwnd, true);
        thread::sleep(Duration::from_millis(200));
        clog(&format!("probe direct-apply on  -> bit={}", read_bit(hwnd)));
        apply_passthrough(hwnd, false);
        thread::sleep(Duration::from_millis(200));
        clog(&format!("probe direct-apply off -> bit={}", read_bit(hwnd)));
    });
}

fn main() {
    let args: Vec<String> = std::env::args().collect();
    let renderer = args
        .iter()
        .skip(1)
        .find(|a| !a.starts_with("--"))
        .cloned()
        .unwrap_or_else(|| {
            r"G:\A_\Tools\DesktopUp_V2\prototype\qt-quick\renderer\build\poster-renderer.exe"
                .to_string()
        });
    PROBE_MOUSE.store(
        args.iter().any(|a| a == "--probe-mouse"),
        Ordering::Relaxed,
    );
    VERIFY.store(args.iter().any(|a| a == "--verify"), Ordering::Relaxed);

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
    let mut pending: HashMap<u64, std::time::Instant> = HashMap::new();
    let mut ticks: u64 = 0;
    let mut hellos = 0u32;

    loop {
        ticks += 1;
        if ticks % 100 == 0 {
            let id = next_id;
            next_id += 1;
            send_frame(&mut stdin, &serde_json::json!({"cmd": "get_health", "id": id}))?;
            pending.insert(id, std::time::Instant::now());
            clog(&format!("sent get_health id={id}"));
        }
        if ticks % 300 == 150 {
            // 定期让渲染器自抓窗口帧(呈现自检素材)
            let id = next_id;
            next_id += 1;
            send_frame(&mut stdin, &serde_json::json!({"cmd": "capture", "id": id}))?;
            pending.insert(id, std::time::Instant::now());
            clog(&format!("sent capture id={id}"));
        }
        // 几何往返探针:移动 → 健康轮询验证 → 还原
        if ticks == 400 {
            let id = next_id;
            next_id += 1;
            send_frame(
                &mut stdin,
                &serde_json::json!({"cmd":"set_geometry","id":id,"x":1710,"y":210,"w":450,"h":703}),
            )?;
            pending.insert(id, std::time::Instant::now());
            clog("probe: set_geometry -> 1710,210");
        }
        if ticks == 600 {
            let id = next_id;
            next_id += 1;
            send_frame(
                &mut stdin,
                &serde_json::json!({"cmd":"set_geometry","id":id,"x":1700,"y":200,"w":450,"h":703}),
            )?;
            pending.insert(id, std::time::Instant::now());
            clog("probe: set_geometry -> restore 1700,200");
        }
        // --verify 验证序列:显隐 10 轮 / 主题热切换计时 / 隐藏态负向自检
        if VERIFY.load(Ordering::Relaxed) {
            for round in 0..10u64 {
                if ticks == 400 + round * 60 {
                    let vis = round % 2 == 0;
                    let id = next_id;
                    next_id += 1;
                    send_frame(
                        &mut stdin,
                        &serde_json::json!({"cmd":"set_visible","id":id,"visible":vis}),
                    )?;
                    pending.insert(id, std::time::Instant::now());
                    clog(&format!("verify: set_visible -> {vis} (round {})", round + 1));
                }
            }
            if ticks == 1100 {
                let id = next_id;
                next_id += 1;
                send_frame(
                    &mut stdin,
                    &serde_json::json!({"cmd":"set_params","id":id,"accent":"#ff8800","opacity":0.7}),
                )?;
                pending.insert(id, std::time::Instant::now());
                clog("verify: set_params hot-swap -> #ff8800/0.7");
            }
            if ticks == 1130 {
                let id = next_id;
                next_id += 1;
                send_frame(
                    &mut stdin,
                    &serde_json::json!({"cmd":"set_params","id":id,"accent":"#72e4ff","opacity":0.82}),
                )?;
                pending.insert(id, std::time::Instant::now());
                clog("verify: set_params restore");
            }
            if ticks == 1200 {
                let id = next_id;
                next_id += 1;
                send_frame(
                    &mut stdin,
                    &serde_json::json!({"cmd":"set_visible","id":id,"visible":false}),
                )?;
                pending.insert(id, std::time::Instant::now());
                clog("verify: hide window (negative self-check setup)");
            }
            if ticks == 1260 {
                let id = next_id;
                next_id += 1;
                send_frame(&mut stdin, &serde_json::json!({"cmd": "capture", "id": id}))?;
                pending.insert(id, std::time::Instant::now());
                clog("verify: capture while hidden (expect FAIL/timeout)");
            }
            if ticks == 1350 {
                let id = next_id;
                next_id += 1;
                send_frame(
                    &mut stdin,
                    &serde_json::json!({"cmd":"set_visible","id":id,"visible":true}),
                )?;
                pending.insert(id, std::time::Instant::now());
                clog("verify: restore visible");
            }
        }
        // 超时清理:5s 未回复的挂起命令视为超时(负向自检证据之一)
        let now = std::time::Instant::now();
        let timed_out: Vec<u64> = pending
            .iter()
            .filter(|(_, t)| now.duration_since(**t) > Duration::from_secs(5))
            .map(|(id, _)| *id)
            .collect();
        for id in timed_out {
            pending.remove(&id);
            clog(&format!("pending id={id} TIMED OUT (no reply)"));
        }

        loop {
            match rx.try_recv() {
                Ok(Wire::Eof) => return Ok(SessionOutcome::RendererExited),
                Ok(Wire::Frame(value)) => {
                    if let Some(id) = value.get("id").and_then(|v| v.as_u64()) {
                        if let Some(t0) = pending.remove(&id) {
                            clog(&format!("reply: {value} ({}ms)", t0.elapsed().as_millis()));
                            // 呈现自检裁决:capture 回复带品红标记计数
                            if let Some(m) = value.get("magenta").and_then(|v| v.as_f64()) {
                                if m > 0.0 {
                                    clog(&format!("PRESENTATION CHECK PASS: magenta={m}"));
                                } else {
                                    clog("PRESENTATION CHECK FAIL: magenta=0");
                                }
                            }
                            // 几何以渲染端回复为准(拖动/命令后刷新命中坐标系)
                            if let Some(geo) = value.get("geometry").and_then(|v| v.as_str()) {
                                let parts: Vec<i32> =
                                    geo.split(',').filter_map(|s| s.parse().ok()).collect();
                                if parts.len() == 4 {
                                    if let Ok(mut guard) =
                                        SURFACE.get_or_init(|| Mutex::new(None)).lock()
                                    {
                                        if let Some(s) = guard.as_mut() {
                                            s.x = parts[0];
                                            s.y = parts[1];
                                            s.w = parts[2];
                                            s.h = parts[3];
                                        }
                                    }
                                }
                            }
                            continue;
                        }
                    }
                    match value.get("cmd").and_then(|v| v.as_str()) {
                        Some("hello") => {
                            hellos += 1;
                            clog(&format!("hello #{hellos} from renderer: {value}"));
                            register_surface(&value);
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

fn register_surface(value: &serde_json::Value) {
    let Some(hwnd) = value
        .get("hwnd")
        .and_then(|v| v.as_f64())
        .map(|f| f as u64)
    else {
        clog("hello missing hwnd");
        return;
    };
    let num = |obj: &serde_json::Value, key: &str| -> i32 {
        obj.get(key)
            .and_then(|v| v.as_f64())
            .map(|f| f as i32)
            .unwrap_or(0)
    };
    let rect = value.get("rect");
    let x = rect.map(|r| num(r, "x")).unwrap_or(0);
    let y = rect.map(|r| num(r, "y")).unwrap_or(0);
    let w = rect.map(|r| num(r, "w")).unwrap_or(0);
    let h = rect.map(|r| num(r, "h")).unwrap_or(0);
    let mut regions = Vec::new();
    if let Some(list) = value.get("regions").and_then(|v| v.as_array()) {
        for r in list {
            regions.push((num(r, "x"), num(r, "y"), num(r, "w"), num(r, "h")));
        }
    }
    clog(&format!("parsed {} regions", regions.len()));
    let parsed_len = regions.len();
    if let Ok(mut guard) = SURFACE.get_or_init(|| Mutex::new(None)).lock() {
        *guard = Some(Surface { hwnd: hwnd as isize, x, y, w, h, regions });
        let stored_len = guard.as_ref().map(|s| s.regions.len()).unwrap_or(0);
        clog(&format!("store inside lock: parsed={parsed_len} stored={stored_len}"));
    }
    HOOK_STARTED.get_or_init(|| {
        thread::spawn(run_hook_thread);
    });
    if PROBE_MOUSE.load(Ordering::Relaxed) {
        PROBE_STARTED.get_or_init(run_mouse_probe);
    }
    clog(&format!("surface registered hwnd=0x{hwnd:X} rect={x},{y},{w},{h} regions={}", {
        let guard = SURFACE.get_or_init(|| Mutex::new(None)).lock().ok();
        guard.as_ref().map(|g| g.as_ref().map(|s| s.regions.len()).unwrap_or(0)).unwrap_or(0)
    }));
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
