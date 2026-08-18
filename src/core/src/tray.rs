// 托盘(最小,C-V2P1-05):Shell_NotifyIconW + 右键菜单(重启海报/退出)
// 运行约束:由钩子线程的消息泵驱动(托盘回调投递到安装线程的消息队列)
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::OnceLock;

use windows::core::{w, PCWSTR};
use windows::Win32::Foundation::{HWND, POINT};
use windows::Win32::UI::Shell::{
    Shell_NotifyIconW, NIF_ICON, NIF_MESSAGE, NIF_TIP, NIM_ADD, NOTIFYICONDATAW,
};
use windows::Win32::UI::WindowsAndMessaging::{
    AppendMenuW, CreatePopupMenu, CreateWindowExW, DestroyMenu, GetCursorPos, LoadImageW, MSG,
    SetForegroundWindow, TrackPopupMenu, HICON, HWND_MESSAGE, IMAGE_ICON, LR_LOADFROMFILE,
    MF_STRING, TPM_BOTTOMALIGN, TPM_LEFTALIGN, TPM_RIGHTBUTTON, WM_COMMAND, WM_RBUTTONUP,
    WINDOW_EX_STYLE, WINDOW_STYLE,
};

pub static RESTART_REQUESTED: AtomicBool = AtomicBool::new(false);
pub static EXIT_REQUESTED: AtomicBool = AtomicBool::new(false);

const TRAY_CALLBACK: u32 = 0x8001; // WM_APP + 1
const CMD_RESTART: usize = 1;
const CMD_EXIT: usize = 2;

static TRAY_HWND: OnceLock<isize> = OnceLock::new();

/// 创建消息窗口 + 添加托盘图标;返回 (hwnd, 是否成功)
pub fn init() -> Result<(), String> {
    let hwnd = unsafe {
        CreateWindowExW(
            WINDOW_EX_STYLE(0),
            w!("STATIC"),
            w!("DesktopUpTray"),
            WINDOW_STYLE(0),
            0,
            0,
            0,
            0,
            Some(HWND_MESSAGE),
            None,
            None,
            None,
        )
    }
    .map_err(|e| format!("CreateWindowExW: {e}"))?;
    let _ = TRAY_HWND.set(hwnd.0 as isize);

    // 图标:exe 旁 assets/tray.ico
    let icon_path = std::env::current_exe()
        .ok()
        .and_then(|p| p.parent().map(|d| d.join("assets").join("tray.ico")))
        .ok_or("no exe path")?;
    let wide: Vec<u16> = icon_path
        .to_string_lossy()
        .encode_utf16()
        .chain(std::iter::once(0))
        .collect();
    let hicon = unsafe {
        LoadImageW(None, PCWSTR(wide.as_ptr()), IMAGE_ICON, 16, 16, LR_LOADFROMFILE)
            .map(|h| HICON(h.0))
            .map_err(|e| format!("LoadImageW: {e}"))?
    };

    let mut nid = NOTIFYICONDATAW::default();
    nid.cbSize = std::mem::size_of::<NOTIFYICONDATAW>() as u32;
    nid.hWnd = hwnd;
    nid.uID = 1;
    nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
    nid.uCallbackMessage = TRAY_CALLBACK;
    nid.hIcon = hicon;
    let tip: Vec<u16> = "DesktopUp V2".encode_utf16().chain(std::iter::once(0)).collect();
    nid.szTip[..tip.len()].copy_from_slice(&tip);
    if unsafe { Shell_NotifyIconW(NIM_ADD, &nid) }.as_bool() {
        Ok(())
    } else {
        Err("NIM_ADD failed".into())
    }
}

/// 消息泵钩子:处理托盘回调与菜单命令;返回 true 表示已消费(不再转派)
pub fn pump(msg: &MSG) -> bool {
    if msg.message == TRAY_CALLBACK {
        if msg.lParam.0 as u32 == WM_RBUTTONUP {
            show_menu();
        }
        return true;
    }
    if msg.message == WM_COMMAND {
        match msg.wParam.0 as usize & 0xFFFF {
            CMD_RESTART => RESTART_REQUESTED.store(true, Ordering::SeqCst),
            CMD_EXIT => EXIT_REQUESTED.store(true, Ordering::SeqCst),
            _ => {}
        }
        return true;
    }
    false
}

fn show_menu() {
    let Some(raw) = TRAY_HWND.get() else {
        return;
    };
    let hwnd = HWND(*raw as *mut core::ffi::c_void);
    unsafe {
        let Ok(menu) = CreatePopupMenu() else {
            return;
        };
        let _ = AppendMenuW(menu, MF_STRING, CMD_RESTART, w!("重启海报"));
        let _ = AppendMenuW(menu, MF_STRING, CMD_EXIT, w!("退出"));
        let mut pt = POINT::default();
        let _ = GetCursorPos(&mut pt);
        let _ = SetForegroundWindow(hwnd);
        let _ = TrackPopupMenu(
            menu,
            TPM_RIGHTBUTTON | TPM_BOTTOMALIGN | TPM_LEFTALIGN,
            pt.x,
            pt.y,
            Some(0),
            hwnd,
            None,
        );
        let _ = DestroyMenu(menu);
    }
}
