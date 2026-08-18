// C-V2P1-01 传输验证:命名管道服务器(修正版模式)
// 教训对照:PIPE_NOWAIT + 一次性 ConnectNamedPipe → 断管死锁(已弃用);
// 本实现 = 阻塞 ConnectNamedPipe 循环 + 每客户端独立读线程 + 多实例 + byte-mode 长度前缀帧。
// 用法:
//   pipe_server_test          → 服务器(处理 2 个客户端后退出)
//   pipe_server_test --client → 客户端(两次 echo 往返,成功退出码 0)
use windows::core::w;
use windows::Win32::Foundation::{CloseHandle, HANDLE};
use windows::Win32::Storage::FileSystem::{
    CreateFileW, ReadFile, WriteFile, FILE_FLAGS_AND_ATTRIBUTES, FILE_GENERIC_READ,
    FILE_GENERIC_WRITE, FILE_SHARE_MODE, OPEN_EXISTING, PIPE_ACCESS_DUPLEX,
};
use windows::Win32::System::Pipes::{
    ConnectNamedPipe, CreateNamedPipeW, PIPE_READMODE_BYTE, PIPE_TYPE_BYTE, PIPE_WAIT,
};

const PIPE_NAME_LITERAL: &str = r"\\.\pipe\desktopup-pipe-smoke";

fn send_frame(h: HANDLE, payload: &[u8]) -> bool {
    let mut frame = Vec::with_capacity(4 + payload.len());
    frame.extend_from_slice(&(payload.len() as u32).to_le_bytes());
    frame.extend_from_slice(payload);
    let mut written = 0u32;
    unsafe { WriteFile(h, Some(&frame), Some(&mut written), None).is_ok() && written as usize == frame.len() }
}

fn recv_frame(h: HANDLE, buf: &mut [u8; 4096]) -> Option<Vec<u8>> {
    let mut n = 0u32;
    if unsafe { ReadFile(h, Some(buf), Some(&mut n), None) }.is_err() || n == 0 {
        return None;
    }
    Some(buf[..n as usize].to_vec())
}

fn server() {
    let mut handled = 0u32;
    let mut sessions = Vec::new();
    while handled < 2 {
        let h = unsafe {
            CreateNamedPipeW(
                w!(r"\\.\pipe\desktopup-pipe-smoke"),
                PIPE_ACCESS_DUPLEX,
                PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
                4,
                4096,
                4096,
                0,
                None,
            )
        };
        println!("server: instance {}, waiting (blocking ConnectNamedPipe)...", handled + 1);
        unsafe {
            ConnectNamedPipe(h, None).expect("ConnectNamedPipe");
        }
        println!("server: client connected on instance {}", handled + 1);
        let raw = h.0 as isize;
        sessions.push(std::thread::spawn(move || {
            let h = HANDLE(raw as *mut core::ffi::c_void);
            let mut buf = [0u8; 4096];
            loop {
                let Some(frame) = recv_frame(h, &mut buf) else {
                    break;
                };
                // 帧 = [4 字节长度][载荷];解析后回显载荷(重打包为帧)
                if frame.len() >= 4 {
                    let plen = u32::from_le_bytes(frame[..4].try_into().unwrap()) as usize;
                    if frame.len() == 4 + plen {
                        println!("server: received {plen} bytes payload, echoing back");
                        if !send_frame(h, &frame[4..]) {
                            break;
                        }
                    }
                }
            }
            println!("server: client session closed");
            unsafe {
                let _ = CloseHandle(h);
            }
        }));
        handled += 1;
    }
    println!("server: all instances connected; waiting for echo sessions...");
    for t in sessions {
        let _ = t.join();
    }
    println!("server: smoke complete (2 clients handled)");
}

fn client() -> i32 {
    let h = unsafe {
        CreateFileW(
            w!(r"\\.\pipe\desktopup-pipe-smoke"),
            (FILE_GENERIC_READ | FILE_GENERIC_WRITE).0,
            FILE_SHARE_MODE(0),
            None,
            OPEN_EXISTING,
            FILE_FLAGS_AND_ATTRIBUTES(0),
            None,
        )
        .expect("CreateFileW connect to pipe")
    };
    println!("client: connected");
    let mut buf = [0u8; 4096];
    let mut ok = 0u32;
    for i in 1..=2u32 {
        let payload = format!("ping-{i}").into_bytes();
        if !send_frame(h, &payload) {
            eprintln!("client: send failed");
            break;
        }
        match recv_frame(h, &mut buf) {
            Some(reply) if reply.len() == 4 + payload.len() && reply[4..] == payload[..] => {
                println!("client: echo #{i} OK ({})", String::from_utf8_lossy(&payload));
                ok += 1;
            }
            other => {
                eprintln!("client: echo #{i} MISMATCH: {other:?}");
            }
        }
    }
    unsafe {
        let _ = CloseHandle(h);
    }
    if ok == 2 {
        println!("client: PASS");
        0
    } else {
        eprintln!("client: FAIL");
        1
    }
}

fn main() {
    let args: Vec<String> = std::env::args().collect();
    if args.iter().any(|a| a == "--client") {
        std::process::exit(client());
    }
    server();
}
