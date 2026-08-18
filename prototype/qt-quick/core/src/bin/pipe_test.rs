// 最小管道回环测试:server 建管(与核心桩相同标志)→ 客户端 CreateFile → 双向通信
use std::thread;
use std::time::Duration;

use windows::core::w;
use windows::Win32::Foundation::{CloseHandle, GetLastError, HANDLE, WAIT_OBJECT_0};
use windows::Win32::Storage::FileSystem::{
    CreateFileW, ReadFile, WriteFile, FILE_GENERIC_READ, FILE_GENERIC_WRITE, FILE_SHARE_MODE,
    FILE_FLAGS_AND_ATTRIBUTES, OPEN_EXISTING, PIPE_ACCESS_DUPLEX,
};
use windows::Win32::System::Pipes::{
    ConnectNamedPipe, CreateNamedPipeW, DisconnectNamedPipe, PIPE_READMODE_BYTE, PIPE_TYPE_BYTE,
};
use windows::Win32::System::Threading::{CreateEventW, SetEvent, WaitForSingleObject};

fn main() {
    let pipe = unsafe {
        CreateNamedPipeW(
            w!(r"\\.\pipe\desktopup-minipipe"),
            PIPE_ACCESS_DUPLEX,
            PIPE_TYPE_BYTE | PIPE_READMODE_BYTE,
            1,
            64 * 1024,
            64 * 1024,
            0,
            None,
        )
    };
    println!("server pipe: {pipe:?} invalid={}", pipe.is_invalid());
    if pipe.is_invalid() {
        panic!("CreateNamedPipeW failed err={}", unsafe { GetLastError() }.0);
    }

    let connected =
        unsafe { CreateEventW(None, true, false, None) }.expect("CreateEventW");
    {
        let pipe_raw = pipe.0 as isize;
        let connected_raw = connected.0 as isize;
        thread::spawn(move || unsafe {
            let pipe = HANDLE(pipe_raw as *mut core::ffi::c_void);
            let connected = HANDLE(connected_raw as *mut core::ffi::c_void);
            let r = ConnectNamedPipe(pipe, None);
            println!("ConnectNamedPipe -> {r:?} err={}", GetLastError().0);
            let _ = SetEvent(connected);
        });
    }

    // 先启动客户端,再等连接事件(阻塞 ConnectNamedPipe 会在客户端到达时返回)
    let client = thread::spawn(|| unsafe {
        let h = CreateFileW(
            w!(r"\\.\pipe\desktopup-minipipe"),
            (FILE_GENERIC_READ | FILE_GENERIC_WRITE).0,
            FILE_SHARE_MODE(0),
            None,
            OPEN_EXISTING,
            FILE_FLAGS_AND_ATTRIBUTES(0),
            None,
        );
        match h {
            Ok(h) => {
                println!("client connected {h:?}");
                let data = b"hello-from-client";
                let mut written = 0u32;
                let w = WriteFile(h, Some(data), Some(&mut written as *mut u32), None);
                println!("client write {w:?} written={written}");
                let mut buf = [0u8; 256];
                let mut read = 0u32;
                let r = ReadFile(h, Some(&mut buf), Some(&mut read as *mut u32), None);
                println!(
                    "client read {r:?} read={read} text={}",
                    String::from_utf8_lossy(&buf[..read as usize])
                );
                let _ = CloseHandle(h);
            }
            Err(e) => println!("client CreateFile failed: {e}"),
        }
    });

    // 等待连接事件(可能因 NOWAIT 提前触发)
    let wait = unsafe { WaitForSingleObject(connected, 3_000) };
    println!("wait = {wait:?} (WAIT_OBJECT_0={WAIT_OBJECT_0:?})");
    thread::sleep(Duration::from_millis(300)); // 给客户端连接时间

    // 客户端线程
    // 服务端读+写
    let mut buf = [0u8; 256];
    let mut read = 0u32;
    let r = unsafe { ReadFile(pipe, Some(&mut buf), Some(&mut read as *mut u32), None) };
    println!("server read {r:?} read={read} err={} text={}",
        unsafe { GetLastError() }.0,
        String::from_utf8_lossy(&buf[..read as usize]));

    let reply = b"hello-from-server";
    let mut written = 0u32;
    let w = unsafe { WriteFile(pipe, Some(reply), Some(&mut written as *mut u32), None) };
    println!("server write {w:?} written={written}");

    let _ = client.join();
    thread::sleep(Duration::from_millis(200));
    unsafe {
        let _ = DisconnectNamedPipe(pipe);
        let _ = CloseHandle(pipe);
    }
    println!("done");
}
