// 闸口 A 矩阵项 1 的自动化部分:控制通道显隐切换 10 轮(修订链自跟随)
// 用法:pipe_vis_test
use windows::core::w;
use windows::Win32::Foundation::{CloseHandle, HANDLE};
use windows::Win32::Storage::FileSystem::{
    CreateFileW, ReadFile, WriteFile, FILE_FLAGS_AND_ATTRIBUTES, FILE_GENERIC_READ,
    FILE_GENERIC_WRITE, FILE_SHARE_MODE, OPEN_EXISTING,
};

fn read_frame(h: HANDLE) -> Option<serde_json::Value> {
    let mut len_buf = [0u8; 4];
    let mut got = 0usize;
    while got < 4 {
        let mut n = 0u32;
        if unsafe { ReadFile(h, Some(&mut len_buf[got..]), Some(&mut n), None) }.is_err() || n == 0 {
            return None;
        }
        got += n as usize;
    }
    let len = u32::from_le_bytes(len_buf) as usize;
    let mut payload = vec![0u8; len];
    let mut got = 0usize;
    while got < len {
        let mut n = 0u32;
        if unsafe { ReadFile(h, Some(&mut payload[got..]), Some(&mut n), None) }.is_err() || n == 0 {
            return None;
        }
        got += n as usize;
    }
    serde_json::from_slice(&payload).ok()
}

fn write_frame(h: HANDLE, value: &serde_json::Value) -> bool {
    let body = serde_json::to_vec(value).unwrap();
    let mut frame = Vec::with_capacity(4 + body.len());
    frame.extend_from_slice(&(body.len() as u32).to_le_bytes());
    frame.extend_from_slice(&body);
    let mut written = 0u32;
    let ok = unsafe { WriteFile(h, Some(&frame), Some(&mut written), None).is_ok() };
    ok && written as usize == frame.len()
}

fn main() {
    let h = unsafe {
        CreateFileW(
            w!(r"\\.\pipe\desktopup-core-v1"),
            (FILE_GENERIC_READ | FILE_GENERIC_WRITE).0,
            FILE_SHARE_MODE(0),
            None,
            OPEN_EXISTING,
            FILE_FLAGS_AND_ATTRIBUTES(0),
            None,
        )
        .expect("connect control pipe — is desktopup-core running?")
    };

    let mut base_rev: u64 = 1;
    let mut failures = 0u32;
    for round in 1..=10u32 {
        let visible = round % 2 == 0; // false,true,... 交替,最后回到可见
        let req = serde_json::json!({"cmd":"set_visible","base_rev":base_rev,"visible":visible});
        if !write_frame(h, &req) {
            println!("round {round}: SEND FAILED");
            failures += 1;
            break;
        }
        match read_frame(h) {
            Some(reply) => {
                let ok = reply.get("ok").and_then(|v| v.as_bool()).unwrap_or(false);
                let rev = reply.get("revision").and_then(|v| v.as_u64()).unwrap_or(0);
                if ok && rev == base_rev + 1 {
                    base_rev = rev;
                    println!("round {round}: PASS visible={visible} rev={rev}");
                } else {
                    println!("round {round}: FAIL {reply}");
                    failures += 1;
                    break;
                }
            }
            None => {
                println!("round {round}: NO REPLY");
                failures += 1;
                break;
            }
        }
        std::thread::sleep(std::time::Duration::from_millis(150));
    }
    // 确保最终可见
    let _ = write_frame(h, &serde_json::json!({"cmd":"set_visible","base_rev":base_rev,"visible":true}));
    let _ = read_frame(h);
    unsafe {
        let _ = CloseHandle(h);
    }
    println!("visibility toggle: {} rounds, {} failures", if failures == 0 { 10 } else { 0 }, failures);
    std::process::exit(if failures == 0 { 0 } else { 1 });
}
