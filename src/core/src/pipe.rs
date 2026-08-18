// 命名管道服务器(IPC_PROTOCOL_V1 §4):阻塞 ConnectNamedPipe + 每客户端独立线程
// 教训对照:禁用 PIPE_NOWAIT + 一次性 ConnectNamedPipe(断管死锁);本实现已在
// pipe_server_test 验证(2 客户端 × 2 回声往返 PASS)。
use std::sync::Arc;

use windows::core::PCWSTR;
use windows::Win32::Foundation::{CloseHandle, HANDLE};
use windows::Win32::Storage::FileSystem::{PIPE_ACCESS_DUPLEX, ReadFile, WriteFile};
use windows::Win32::System::Pipes::{
    ConnectNamedPipe, CreateNamedPipeW, PIPE_READMODE_BYTE, PIPE_TYPE_BYTE, PIPE_WAIT,
};

type Handler = Arc<dyn Fn(serde_json::Value) -> serde_json::Value + Send + Sync>;

/// 启动接受线程;handler 在客户端线程内执行,返回帧作为回复。
pub fn start(name: &'static str, handler: Handler) -> std::thread::JoinHandle<()> {
    std::thread::spawn(move || loop {
        let wide: Vec<u16> = name.encode_utf16().chain(std::iter::once(0)).collect();
        let h = unsafe {
            CreateNamedPipeW(
                PCWSTR(wide.as_ptr()),
                PIPE_ACCESS_DUPLEX,
                PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
                8,
                4096,
                4096,
                0,
                None,
            )
        };
        if h.is_invalid() {
            break;
        }
        if unsafe { ConnectNamedPipe(h, None) }.is_err() {
            unsafe {
                let _ = CloseHandle(h);
            }
            continue;
        }
        let raw = h.0 as isize;
        let handler = handler.clone();
        std::thread::spawn(move || {
            let h = HANDLE(raw as *mut core::ffi::c_void);
            loop {
                let Some(payload) = read_frame(h) else {
                    break;
                };
                let Ok(req) = serde_json::from_slice::<serde_json::Value>(&payload) else {
                    break;
                };
                let reply = handler(req);
                let Ok(body) = serde_json::to_vec(&reply) else {
                    break;
                };
                if !write_frame(h, &body) {
                    break;
                }
            }
            unsafe {
                let _ = CloseHandle(h);
            }
        });
    })
}

/// 读一帧([4 字节 LE 长度][载荷]);EOF/错误 → None
fn read_frame(h: HANDLE) -> Option<Vec<u8>> {
    let mut len_buf = [0u8; 4];
    let mut got = 0usize;
    while got < 4 {
        let mut n = 0u32;
        if unsafe { ReadFile(h, Some(&mut len_buf[got..]), Some(&mut n), None) }.is_err() || n == 0
        {
            return None;
        }
        got += n as usize;
    }
    let len = u32::from_le_bytes(len_buf) as usize;
    let mut payload = vec![0u8; len];
    let mut got = 0usize;
    while got < len {
        let mut n = 0u32;
        if unsafe { ReadFile(h, Some(&mut payload[got..]), Some(&mut n), None) }.is_err()
            || n == 0
        {
            return None;
        }
        got += n as usize;
    }
    Some(payload)
}

/// 写一帧;失败 → false
fn write_frame(h: HANDLE, payload: &[u8]) -> bool {
    let mut frame = Vec::with_capacity(4 + payload.len());
    frame.extend_from_slice(&(payload.len() as u32).to_le_bytes());
    frame.extend_from_slice(payload);
    let mut written = 0u32;
    let ok = unsafe { WriteFile(h, Some(&frame), Some(&mut written), None).is_ok() };
    ok && written as usize == frame.len()
}
