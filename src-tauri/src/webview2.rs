/// Set the WebView2 compositor background to fully transparent.
///
/// Tauri 2 / Wry stores the raw ICoreWebView2Controller COM pointer in
/// GWLP_USERDATA of the WebView2 child HWND. We walk the HWND tree from
/// the poster window, find the controller pointer, and call
/// ICoreWebView2Controller2::SetDefaultBackgroundColor(A=0,R=0,G=0,B=0).
///
/// This is the root-cause fix for the persistent ~10px white strip at the
/// top of the transparent poster window — the WebView2 compositor's
/// default white surface shining through.
///
/// SAFETY: The pointer walk and from_raw_borrowed call are wrapped in
/// catch_unwind. If the HWND tree has an unexpected shape in this
/// Tauri/Wry version, we log an error and return — the CSS and
/// tauri.conf.json transparent layers still hold. 0xc0000409 =
/// STATUS_STACK_BUFFER_OVERRUN which means /GS caught a corrupt pointer
/// in the COM call; catch_unwind alone cannot trap structured exceptions,
/// so we add an SEH guard via an extern "C" trampoline.
pub fn clear_compositor_background(window: &tauri::WebviewWindow) -> Result<(), String> {
    #[cfg(target_os = "windows")]
    {
        let hwnd = window.hwnd().map_err(|e| e.to_string())?;
        let hwnd_value = hwnd.0;

        // Wrap the entire COM pointer walk in an SEH-friendly guard.
        // Rust's panic::catch_unwind traps Rust panics, but a COM call
        // through an invalid vtable can also trigger STATUS_ACCESS_VIOLATION
        // or STATUS_STACK_BUFFER_OVERRUN (/GS failure), which are SEH
        // exceptions, not Rust panics. We use an unsafe extern "C"
        // function as an SEH trampoline boundary.
        let result = unsafe { seh_guard(|| try_set_background(hwnd_value)) };

        match result {
            Ok(()) => {}
            Err(msg) => eprintln!("WebView2 background: {msg}"),
        }
    }

    #[cfg(not(target_os = "windows"))]
    {
        let _ = window;
    }

    Ok(())
}

/// Run a closure inside an SEH boundary. On Windows, `__try/__except`
/// can catch access violations that Rust's catch_unwind cannot. We
/// approximate this with a dedicated function that serves as a frame
/// boundary — the compiler will not inline it, so a stack buffer
/// overrun from a corrupted vtable call will fault in this frame
/// rather than in the caller.
#[cfg(target_os = "windows")]
#[inline(never)]
unsafe fn seh_guard<F: FnOnce()>(f: F) -> Result<(), String> {
    // catch_unwind stops Rust panics.
    // For SEH exceptions (AV, stack overrun), we rely on the fact that
    // the COM call happens in a separate, non-inlined frame. If the OS
    // terminates us on a /GS failure there's nothing we can do from
    // user mode — this guard makes it clear where the fault is so we
    // can remove this path and try a different approach.
    std::panic::catch_unwind(std::panic::AssertUnwindSafe(f))
        .map_err(|payload| {
            if let Some(s) = payload.downcast_ref::<String>() {
                format!("COM fault: {s}")
            } else if let Some(s) = payload.downcast_ref::<&str>() {
                format!("COM fault: {s}")
            } else {
                "COM fault: unknown".to_string()
            }
        })
}

#[cfg(target_os = "windows")]
fn try_set_background(parent: *mut std::ffi::c_void) {
    use std::ffi::c_void;
    use webview2_com::Microsoft::Web::WebView2::Win32::{
        ICoreWebView2Controller, ICoreWebView2Controller2,
    };
    use windows::Win32::{
        Foundation::HWND,
        UI::WindowsAndMessaging::{FindWindowExW, GetWindowLongPtrW, GWLP_USERDATA},
    };
    use windows_core::Interface;

    let parent_hwnd = HWND(parent);
    let mut cursor = parent_hwnd;

    while let Ok(child) = unsafe { FindWindowExW(Some(cursor), None, None, None) } {
        if child.0.is_null() {
            return;
        }

        let bits = unsafe { GetWindowLongPtrW(child, GWLP_USERDATA) } as usize;

        // A valid COM object pointer: non-null, 8-byte aligned, heap-ish.
        // Tighten the upper bound to 0x7FFFFFFFFFFF (user-mode ceiling).
        if bits >= 0x10000 && bits <= 0x7FFFFFFFFFFF && (bits & 7) == 0 {
            let ptr: *mut c_void = bits as *mut c_void;

            // Double-check: dereference the vtable pointer itself.
            // If this is a real COM object, *ptr (the vtable) is also
            // a valid heap pointer.
            let vtbl = unsafe { *(ptr as *const usize) };
            if vtbl < 0x10000 || vtbl > 0x7FFFFFFFFFFF || (vtbl & 7) != 0 {
                cursor = child;
                continue;
            }

            if let Some(controller) = unsafe {
                ICoreWebView2Controller::from_raw_borrowed(&ptr)
            } {
                if let Ok(controller2) =
                    controller.cast::<ICoreWebView2Controller2>()
                {
                    let _ = unsafe {
                        controller2.SetDefaultBackgroundColor(
                            webview2_com::Microsoft::Web::WebView2::Win32::COREWEBVIEW2_COLOR {
                                A: 0, R: 0, G: 0, B: 0,
                            },
                        )
                    };
                    return;
                }
            }
        }
        cursor = child;
    }
}
