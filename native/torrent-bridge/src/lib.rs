use std::{
    ffi::{CStr, CString, c_char},
    io::SeekFrom,
    net::{Ipv4Addr, SocketAddr},
    panic::{AssertUnwindSafe, catch_unwind},
    path::PathBuf,
    ptr,
    sync::{Mutex, OnceLock, mpsc},
    thread::JoinHandle,
};

use axum::{
    Router,
    body::Body,
    extract::{Path, Query, State},
    http::{HeaderMap, StatusCode, header},
    response::Response,
    routing::get,
};
use enginefs::{
    BackendEngineFS,
    backend::{TorrentSource, TorrentHandle, priorities::PlaybackIntent, librqbit::{LibrqbitBackend, LibrqbitHandle}},
};
use serde::Deserialize;
use tokio::{io::{AsyncReadExt, AsyncSeekExt}, net::TcpListener, sync::oneshot};
use tokio_util::io::ReaderStream;

type XboxEngine = BackendEngineFS<LibrqbitBackend>;

#[derive(Clone)]
struct AppState {
    engine: std::sync::Arc<XboxEngine>,
}

struct RunningServer {
    port: u16,
    shutdown: Option<oneshot::Sender<()>>,
    thread: Option<JoinHandle<()>>,
}

static SERVER: OnceLock<Mutex<Option<RunningServer>>> = OnceLock::new();
static LAST_ERROR: OnceLock<Mutex<CString>> = OnceLock::new();

fn server_slot() -> &'static Mutex<Option<RunningServer>> {
    SERVER.get_or_init(|| Mutex::new(None))
}

fn error_slot() -> &'static Mutex<CString> {
    LAST_ERROR.get_or_init(|| Mutex::new(CString::new("").unwrap()))
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

#[derive(Deserialize)]
struct StreamQuery {
    magnet: Option<String>,
}

fn parse_range(headers: &HeaderMap, size: u64) -> Result<(u64, u64, bool), StatusCode> {
    let Some(value) = headers.get(header::RANGE) else {
        return Ok((0, size.saturating_sub(1), false));
    };
    let value = value.to_str().map_err(|_| StatusCode::RANGE_NOT_SATISFIABLE)?;
    let raw = value.strip_prefix("bytes=").ok_or(StatusCode::RANGE_NOT_SATISFIABLE)?;
    let (start, end) = raw.split_once('-').ok_or(StatusCode::RANGE_NOT_SATISFIABLE)?;
    let start: u64 = start.parse().map_err(|_| StatusCode::RANGE_NOT_SATISFIABLE)?;
    let end = if end.is_empty() { size.saturating_sub(1) } else {
        end.parse::<u64>().map_err(|_| StatusCode::RANGE_NOT_SATISFIABLE)?.min(size.saturating_sub(1))
    };
    if size == 0 || start >= size || end < start {
        return Err(StatusCode::RANGE_NOT_SATISFIABLE);
    }
    Ok((start, end, true))
}

async fn stream_file(
    State(state): State<AppState>,
    Path((hash, file_idx)): Path<(String, usize)>,
    Query(query): Query<StreamQuery>,
    headers: HeaderMap,
    method: axum::http::Method,
) -> Result<Response, StatusCode> {
    let engine = match state.engine.get_engine(&hash).await {
        Some(engine) => engine,
        None => {
            let magnet = query.magnet.ok_or(StatusCode::NOT_FOUND)?;
            let added = state.engine.add_torrent(TorrentSource::Url(magnet), None)
                .await.map_err(|_| StatusCode::BAD_GATEWAY)?;
            if !added.info_hash.eq_ignore_ascii_case(&hash) {
                return Err(StatusCode::BAD_REQUEST);
            }
            added
        }
    };

    let files = tokio::time::timeout(std::time::Duration::from_secs(60), async {
        loop {
            let files = engine.handle.get_files().await;
            if !files.is_empty() {
                break files;
            }
            tokio::time::sleep(std::time::Duration::from_millis(400)).await;
        }
    }).await.map_err(|_| StatusCode::GATEWAY_TIMEOUT)?;
    let file = files.get(file_idx).ok_or(StatusCode::NOT_FOUND)?;
    let size = file.length;
    let (start, end, partial) = parse_range(&headers, size)?;
    let length = if size == 0 { 0 } else { end - start + 1 };

    let mut response = Response::builder()
        .status(if partial { StatusCode::PARTIAL_CONTENT } else { StatusCode::OK })
        .header(header::ACCEPT_RANGES, "bytes")
        .header(header::CONTENT_LENGTH, length.to_string())
        .header(header::CONTENT_TYPE, "video/mp4");

    if partial {
        response = response.header(header::CONTENT_RANGE, format!("bytes {start}-{end}/{size}"));
    }

    if method == axum::http::Method::HEAD {
        return response.body(Body::empty()).map_err(|_| StatusCode::INTERNAL_SERVER_ERROR);
    }

    let intent = if start == 0 { PlaybackIntent::DirectInitial } else { PlaybackIntent::DirectSeek };
    let mut reader = engine.get_file_with_intent(file_idx, start, 128, intent)
        .await.ok_or(StatusCode::BAD_GATEWAY)?;
    reader.seek(SeekFrom::Start(start)).await.map_err(|_| StatusCode::INTERNAL_SERVER_ERROR)?;
    let bounded = reader.take(length);
    let body = Body::from_stream(ReaderStream::new(bounded));
    response.body(body).map_err(|_| StatusCode::INTERNAL_SERVER_ERROR)
}

fn app(state: AppState) -> Router {
    Router::new()
        .route("/heartbeat", get(|| async { "ok" }))
        .route("/stream/{hash}/{file_idx}", get(stream_file).head(stream_file))
        .with_state(state)
}

/// Starts the in-process librqbit torrent engine and HTTP byte-range server.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn streamio_torrent_bridge_start(
    config_dir: *const c_char,
    cache_dir: *const c_char,
    requested_port: u16,
    actual_port: *mut u16,
) -> i32 {
    let result = catch_unwind(AssertUnwindSafe(|| -> Result<u16, String> {
        if actual_port.is_null() { return Err("Output port pointer was null".into()); }
        let config_dir = unsafe { read_path(config_dir, "Config") }?;
        let cache_dir = unsafe { read_path(cache_dir, "Cache") }?;
        std::fs::create_dir_all(&config_dir).map_err(|e| e.to_string())?;
        std::fs::create_dir_all(&cache_dir).map_err(|e| e.to_string())?;

        let mut slot = server_slot().lock().map_err(|_| "Torrent server lock was poisoned".to_string())?;
        if let Some(server) = slot.as_ref() { return Ok(server.port); }

        let (ready_tx, ready_rx) = mpsc::sync_channel::<Result<u16, String>>(1);
        let (shutdown_tx, shutdown_rx) = oneshot::channel::<()>();
        let thread = std::thread::Builder::new().name("streamiox-torrent".into()).spawn(move || {
            let runtime = match tokio::runtime::Builder::new_multi_thread().worker_threads(2).enable_all().build() {
                Ok(runtime) => runtime,
                Err(error) => { let _ = ready_tx.send(Err(format!("Tokio runtime: {error}"))); return; }
            };
            runtime.block_on(async move {
                // config_dir is supplied by ApplicationData.Current.LocalFolder.
                // librqbit writes its DHT routing table to this explicit AppContainer path.
                let dht_config_path = config_dir.join("dht.json");
                let (backend, restored): (LibrqbitBackend, std::collections::HashMap<String, LibrqbitHandle>) =
                    match LibrqbitBackend::new(cache_dir.join("downloads"), dht_config_path).await {
                        Ok(value) => value,
                        Err(error) => { let _ = ready_tx.send(Err(format!("librqbit init: {error:#}"))); return; }
                    };
                let engine = std::sync::Arc::new(XboxEngine::new_with_backend(
                    backend, restored, cache_dir.join("piece-cache"), cache_dir.join("downloads")
                ));
                let address = SocketAddr::from((Ipv4Addr::LOCALHOST, requested_port));
                let listener = match TcpListener::bind(address).await {
                    Ok(listener) => listener,
                    Err(error) => { let _ = ready_tx.send(Err(format!("HTTP bind: {error}"))); return; }
                };
                let port = match listener.local_addr() {
                    Ok(addr) => addr.port(),
                    Err(error) => { let _ = ready_tx.send(Err(error.to_string())); return; }
                };
                let _ = ready_tx.send(Ok(port));
                let _ = axum::serve(listener, app(AppState { engine }))
                    .with_graceful_shutdown(async { let _ = shutdown_rx.await; })
                    .await;
            });
        }).map_err(|e| format!("Could not start torrent thread: {e}"))?;

        let port = ready_rx.recv().map_err(|e| e.to_string())?
            .map_err(|e| { let _ = thread.thread().id(); e })?;
        *slot = Some(RunningServer { port, shutdown: Some(shutdown_tx), thread: Some(thread) });
        Ok(port)
    }));

    match result {
        Ok(Ok(port)) => { unsafe { *actual_port = port }; set_error(""); 0 }
        Ok(Err(error)) => { set_error(error); -1 }
        Err(_) => { set_error("Torrent engine panicked while starting"); -2 }
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn streamio_torrent_bridge_stop() -> i32 {
    let result = catch_unwind(AssertUnwindSafe(|| {
        let server = match server_slot().lock() {
            Ok(mut slot) => slot.take(),
            Err(_) => { set_error("Torrent server lock was poisoned"); return -1; }
        };
        if let Some(mut server) = server {
            if let Some(shutdown) = server.shutdown.take() { let _ = shutdown.send(()); }
            if let Some(thread) = server.thread.take() {
                if thread.join().is_err() { set_error("Torrent HTTP server thread panicked"); return -1; }
            }
        }
        0
    }));
    match result {
        Ok(code) => code,
        Err(_) => { set_error("Torrent engine panicked while stopping"); -2 }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn streamio_torrent_bridge_last_error(buffer: *mut u8, capacity: usize) -> usize {
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
