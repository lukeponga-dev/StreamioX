use std::{
    ffi::{CStr, CString, c_char},
    net::{Ipv4Addr, SocketAddr},
    panic::{AssertUnwindSafe, catch_unwind},
    path::PathBuf,
    ptr,
    sync::{Mutex, OnceLock},
};

use stream_server::{ServerConfig, ServerHandle};

static SERVER: OnceLock<Mutex<Option<ServerHandle>>> = OnceLock::new();
static LAST_ERROR: OnceLock<Mutex<CString>> = OnceLock::new();

fn server_slot() -> &'static Mutex<Option<ServerHandle>> {
    SERVER.get_or_init(|| Mutex::new(None))
}

fn error_slot() -> &'static Mutex<CString> {
    LAST_ERROR.get_or_init(|| Mutex::new(CString::new("").expect("empty CString")))
}

fn set_error(message: impl AsRef<str>) {
    let clean = message.as_ref().replace('\0', " ");
    if let Ok(mut slot) = error_slot().lock() {
        *slot = CString::new(clean).unwrap_or_else(|_| CString::new("Torrent bridge error").unwrap());
    }
}

unsafe fn read_path(value: *const c_char, name: &str) -> Result<PathBuf, String> {
    if value.is_null() {
        return Err(format!("{name} path was null"));
    }
    let value = unsafe { CStr::from_ptr(value) }
        .to_str()
        .map_err(|_| format!("{name} path was not valid UTF-8"))?;
    if value.trim().is_empty() {
        return Err(format!("{name} path was empty"));
    }
    Ok(PathBuf::from(value))
}

/// Starts the embedded Stremio-compatible torrent streaming server.
/// Returns 0 on success and writes the bound HTTP port to actual_port.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn streamio_torrent_bridge_start(
    config_dir: *const c_char,
    cache_dir: *const c_char,
    requested_port: u16,
    actual_port: *mut u16,
) -> i32 {
    let result = catch_unwind(AssertUnwindSafe(|| -> Result<u16, String> {
        if actual_port.is_null() {
            return Err("Output port pointer was null".to_string());
        }
        let config_dir = unsafe { read_path(config_dir, "Config") }?;
        let cache_dir = unsafe { read_path(cache_dir, "Cache") }?;

        let mut server = server_slot()
            .lock()
            .map_err(|_| "Torrent server lock was poisoned".to_string())?;

        if let Some(handle) = server.as_ref() {
            return Ok(handle.bound_http_addr().port());
        }

        let mut config = ServerConfig::embedded();
        config.http_addr = SocketAddr::from((Ipv4Addr::LOCALHOST, requested_port));
        config.config_dir = Some(config_dir);
        config.cache_dir = Some(cache_dir);
        config.init_logging = false;
        config.manage_process_globals = false;
        config.setup_ffmpeg = false;
        config.listen_for_ctrl_c = false;
        config.print_startup = false;
        config.exit_process_on_shutdown_timeout = false;
        config.enable_update_exit = false;
        config.enable_cache_cleaner = true;
        config.enable_memory_sampler = false;
        config.enable_ssdp_discovery = false;
        config.enable_background_update_checker = false;
        config.enable_local_addon_scan = false;

        let handle = stream_server::start(config)
            .map_err(|error| format!("Could not start torrent engine: {error:#}"))?;
        let port = handle.bound_http_addr().port();
        *server = Some(handle);
        Ok(port)
    }));

    match result {
        Ok(Ok(port)) => {
            unsafe { *actual_port = port };
            set_error("");
            0
        }
        Ok(Err(error)) => {
            set_error(error);
            -1
        }
        Err(_) => {
            set_error("Torrent engine panicked while starting");
            -2
        }
    }
}

/// Stops the embedded torrent server and waits for its shutdown.
#[unsafe(no_mangle)]
pub extern "C" fn streamio_torrent_bridge_stop() -> i32 {
    let result = catch_unwind(AssertUnwindSafe(|| {
        let handle = match server_slot().lock() {
            Ok(mut slot) => slot.take(),
            Err(_) => {
                set_error("Torrent server lock was poisoned");
                return -1;
            }
        };

        if let Some(handle) = handle {
            if let Err(error) = handle.shutdown() {
                set_error(format!("Could not signal torrent server shutdown: {error:#}"));
                return -1;
            }
            if let Err(error) = handle.join() {
                set_error(format!("Torrent server shutdown failed: {error:#}"));
                return -1;
            }
        }
        0
    }));

    match result {
        Ok(code) => code,
        Err(_) => {
            set_error("Torrent engine panicked while stopping");
            -2
        }
    }
}

/// Copies the latest error into a caller-owned UTF-8 buffer.
/// Returns the full message length excluding the null terminator.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn streamio_torrent_bridge_last_error(
    buffer: *mut u8,
    capacity: usize,
) -> usize {
    let message = match error_slot().lock() {
        Ok(value) => value.as_bytes().to_vec(),
        Err(_) => b"Could not read torrent bridge error".to_vec(),
    };

    if !buffer.is_null() && capacity > 0 {
        let copied = message.len().min(capacity - 1);
        unsafe {
            ptr::copy_nonoverlapping(message.as_ptr(), buffer, copied);
            *buffer.add(copied) = 0;
        }
    }
    message.len()
}
