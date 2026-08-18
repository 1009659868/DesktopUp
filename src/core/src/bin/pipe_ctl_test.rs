// 控制通道冒烟:经命名管道对运行中的核心发送 apply 命令,验证 base_rev 防回弹
// 序列与预期:
//   move-ok         set_geometry 1710,210 base_rev=1 → ok, revision=2
//   stale-rejected  同命令 base_rev=1             → rev_conflict, current=2
//   restore-ok      set_geometry 1700,200 base_rev=2 → ok, revision=3
//   params-ok       set_params #ff8800/0.7 base_rev=3 → ok, revision=4
//   params-restore  set_params 还原 base_rev=4      → ok, revision=5
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
        .expect("connect to control pipe — is desktopup-core running?")
    };

    let cases: Vec<(&str, serde_json::Value, &str)> = vec![
        (
            "move-ok",
            serde_json::json!({"cmd":"set_geometry","base_rev":1,"x":1710,"y":210,"w":450,"h":703}),
            "{\"ok\":true,\"revision\":2}",
        ),
        (
            "stale-rejected",
            serde_json::json!({"cmd":"set_geometry","base_rev":1,"x":999,"y":999,"w":450,"h":703}),
            "\"error\":\"rev_conflict\"",
        ),
        (
            "restore-ok",
            serde_json::json!({"cmd":"set_geometry","base_rev":2,"x":1700,"y":200,"w":450,"h":703}),
            "{\"ok\":true,\"revision\":3}",
        ),
        (
            "params-ok",
            serde_json::json!({"cmd":"set_params","base_rev":3,"accent":"#ff8800","opacity":0.7}),
            "{\"ok\":true,\"revision\":4}",
        ),
        (
            "params-restore",
            serde_json::json!({"cmd":"set_params","base_rev":4,"accent":"#72e4ff","opacity":0.82}),
            "{\"ok\":true,\"revision\":5}",
        ),
    ];

    let mut failures = 0u32;
    for (label, req, expect) in &cases {
        if !write_frame(h, req) {
            println!("{label}: SEND FAILED");
            failures += 1;
            continue;
        }
        match read_frame(h) {
            Some(reply) => {
                let text = reply.to_string();
                let ok = match *label {
                    "stale-rejected" => {
                        text.contains("\"error\":\"rev_conflict\"") && text.contains("\"current\":2")
                    }
                    _ => text == *expect,
                };
                println!("{label}: {} — {}", if ok { "PASS" } else { "FAIL" }, text);
                if !ok {
                    failures += 1;
                }
            }
            None => {
                println!("{label}: NO REPLY");
                failures += 1;
            }
        }
    }
    unsafe {
        let _ = CloseHandle(h);
    }
    std::process::exit(if failures == 0 { 0 } else { 1 });
}
