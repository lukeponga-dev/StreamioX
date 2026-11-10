use crate::backend::{
    BackendFileInfo, BackendMemoryDiagnostics, EngineStats, FileStreamTrait, Growler, PeerSearch,
    PieceReadiness, StatsFile, StatsOptions, SwarmCap, TorrentBackend, TorrentHandle,
    TorrentSource,
};
use anyhow::{Context, Result};
use librqbit::{ManagedTorrent, Session};
use std::collections::HashMap;
use std::net::SocketAddr;
use std::path::PathBuf;
use std::sync::Arc;
use std::time::Duration;
use tracing::{debug, warn};

pub struct LibrqbitBackend {
    pub session: Arc<Session>,
}

impl LibrqbitBackend {
    pub async fn new(
        download_dir: PathBuf,
        dht_config_path: PathBuf,
    ) -> Result<(Self, HashMap<String, LibrqbitHandle>)> {
        tokio::fs::create_dir_all(&download_dir).await?;
        debug!(path = ?download_dir, "Storing downloads");

        let session_opts = librqbit::SessionOptions {
            listen_port_range: Some(42000..42010),
            enable_upnp_port_forwarding: false,
            // Persist the DHT routing table inside the app's LocalFolder-backed
            // directory supplied by the native bridge. Never use OS/project defaults
            // in the Xbox AppContainer.
            disable_dht_persistence: false,
            dht_config: Some(librqbit::dht::PersistentDhtConfig {
                dump_interval: Some(Duration::from_secs(60)),
                config_filename: Some(dht_config_path),
            }),
            persistence: Some(librqbit::SessionPersistenceConfig::Json {
                folder: Some(download_dir.clone()),
            }),
            peer_opts: Some(librqbit::PeerConnectionOptions {
                connect_timeout: Some(Duration::from_secs(10)),
                read_write_timeout: Some(Duration::from_secs(30)),
                ..Default::default()
            }),
            ..Default::default()
        };

        let session = Session::new_with_opts(download_dir.clone(), session_opts).await?;
        // Restore from session
        let mut restored_handles = session.with_torrents(|iter| {
            let mut map = HashMap::new();
            for (_id, handle) in iter {
                let info_hash = handle.info_hash().as_string();
                map.insert(
                    info_hash.clone(),
                    LibrqbitHandle {
                        handle: handle.clone(),
                        info_hash,
                        session: session.clone(),
                    },
                );
            }
            map
        });

        // Restore from .cache directory
        let cache_dir = download_dir.join(".cache");
        if let Ok(mut entries) = tokio::fs::read_dir(&cache_dir).await {
            while let Ok(Some(entry)) = entries.next_entry().await {
                let path = entry.path();
                if path.extension().is_some_and(|ext| ext == "torrent") {
                    if let Some(stem) = path.file_stem().and_then(|s| s.to_str()) {
                        let info_hash = stem.to_string();
                        if !restored_handles.contains_key(&info_hash) {
                            if let Ok(bytes) = tokio::fs::read(&path).await {
                                let bytes = bytes::Bytes::from(bytes);
                                let add_torrent = librqbit::AddTorrent::from_bytes(bytes);
                                match session.add_torrent(add_torrent, None).await {
                                    Ok(response) => {
                                        if let librqbit::AddTorrentResponse::Added(_, handle)
                                        | librqbit::AddTorrentResponse::AlreadyManaged(
                                            _,
                                            handle,
                                        ) = response
                                        {
                                            restored_handles.insert(
                                                info_hash.clone(),
                                                LibrqbitHandle {
                                                    handle,
                                                    info_hash,
                                                    session: session.clone(),
                                                },
                                            );
                                        }
                                    }
                                    Err(e) => warn!(error = %e, "Failed to add torrent from cache"),
                                }
                            }
                        }
                    }
                }
            }
        }

        Ok((Self { session }, restored_handles))
    }
}

pub struct LibrqbitHandle {
    pub handle: Arc<ManagedTorrent>,
    pub info_hash: String,
    pub session: Arc<Session>,
}

#[async_trait::async_trait]
impl TorrentBackend for LibrqbitBackend {
    type Handle = LibrqbitHandle;

    async fn add_torrent(
        &self,
        source: TorrentSource,
        trackers: Vec<String>,
    ) -> Result<Self::Handle> {
        let (add_torrent, initial_peers) = match source {
            TorrentSource::Url(url) => {
                // Preserve BEP 9 magnet x.pe= initial peer hints. These are
                // especially useful for local diagnostics and private swarms
                // where DHT or public trackers are unavailable.
                let mut initial_peers = Vec::<SocketAddr>::new();
                if let Some((_, query)) = url.split_once('?') {
                    for (key, value) in url::form_urlencoded::parse(query.as_bytes()) {
                        if key == "x.pe" {
                            if let Ok(peer) = value.parse::<SocketAddr>() {
                                initial_peers.push(peer);
                            }
                        }
                    }
                }
                (librqbit::AddTorrent::Url(url.into()), initial_peers)
            }
            TorrentSource::Bytes(bytes) => (
                librqbit::AddTorrent::from_bytes(bytes::Bytes::from(bytes)),
                Vec::new(),
            ),
        };
        let response = self
            .session
            .add_torrent(
                add_torrent,
                Some(librqbit::AddTorrentOptions {
                    overwrite: true,
                    trackers: Some(trackers),
                    initial_peers: Some(initial_peers),
                    ..Default::default()
                }),
            )
            .await
            .context("Failed to add torrent to librqbit")?;

        let (_id, handle) = match response {
            librqbit::AddTorrentResponse::Added(id, handle)
            | librqbit::AddTorrentResponse::AlreadyManaged(id, handle) => (id, handle),
            _ => return Err(anyhow::anyhow!("Unexpected response from librqbit")),
        };

        Ok(LibrqbitHandle {
            handle,
            info_hash: "".to_string(), // Will be updated by Engine
            session: self.session.clone(),
        })
    }

    async fn get_torrent(&self, _info_hash: &str) -> Option<Self::Handle> {
        None
    }

    async fn remove_torrent(&self, _info_hash: &str) -> Result<()> {
        Ok(())
    }

    async fn list_torrents(&self) -> Vec<String> {
        Vec::new()
    }

    async fn memory_diagnostics(&self) -> BackendMemoryDiagnostics {
        BackendMemoryDiagnostics::default()
    }
}

#[async_trait::async_trait]
impl TorrentHandle for LibrqbitHandle {
    fn info_hash(&self) -> String {
        self.handle.info_hash().as_string()
    }

    fn name(&self) -> Option<String> {
        self.handle
            .metadata
            .load_full()
            .and_then(|m| m.info.name.as_ref().map(|n| n.to_string()))
    }

    async fn wait_until_initialized(&self) -> Result<()> {
        self.handle
            .wait_until_initialized()
            .await
            .context("librqbit torrent storage did not finish initialization")
    }

    async fn resume_torrent(&self) -> Result<()> {
        if self.handle.is_paused() {
            self.session
                .unpause(&self.handle)
                .await
                .context("Failed to resume paused librqbit torrent")?;
        }
        Ok(())
    }

    async fn pause_torrent(&self) -> Result<()> {
        if !self.handle.is_paused() {
            self.session
                .pause(&self.handle)
                .await
                .context("Failed to pause librqbit torrent")?;
        }
        Ok(())
    }

    async fn stats(&self) -> EngineStats {
        let stats = self.handle.stats();
        let (download_speed, upload_speed) = if let Some(ref live) = stats.live {
            (
                live.download_speed.mbps * 1_048_576.0 / 8.0,
                live.upload_speed.mbps * 1_048_576.0 / 8.0,
            )
        } else {
            (0.0, 0.0)
        };

        let (downloaded, uploaded) = if let Some(ref live) = stats.live {
            (live.snapshot.fetched_bytes, live.snapshot.uploaded_bytes)
        } else {
            (0, 0)
        };

        let peers = stats
            .live
            .as_ref()
            .map(|l| l.snapshot.peer_stats.live as u64)
            .unwrap_or(0);

        let mut files = Vec::new();
        let mut total_size = 0u64;
        let mut offset = 0u64;
        if let Some(m) = self.handle.metadata.load_full() {
            if let Ok(iter) = m.info.iter_file_details() {
                for f in iter {
                    let filename = f.filename.to_string().unwrap_or_default();
                    files.push(StatsFile {
                        name: filename.clone(),
                        path: filename,
                        length: f.len,
                        offset,
                        downloaded: 0, // TODO: Implement per-file progress for librqbit if needed
                        progress: 0.0,
                    });
                    total_size += f.len;
                    offset += f.len;
                }
            }
        }

        EngineStats {
            name: self.name().unwrap_or_else(|| "Unknown".to_string()),
            info_hash: self.info_hash(),
            files,
            sources: vec![],
            opts: StatsOptions {
                dht: true,
                tracker: true,
                path: "".to_string(),
                growler: Growler {
                    flood: 0,
                    pulse: None,
                },
                peer_search: PeerSearch {
                    max: 100,
                    min: 10,
                    sources: vec![],
                },
                swarm_cap: SwarmCap {
                    max_speed: None,
                    min_peers: None,
                },
                connections: None,
                handshake_timeout: None,
                timeout: None,
                r#virtual: false,
            },
            download_speed,
            upload_speed,
            downloaded,
            uploaded,
            peers,
            unchoked: peers,
            queued: 0,
            unique: peers,
            connection_tries: 0,
            peer_search_running: true,
            stream_len: total_size,
            stream_name: "".to_string(),
            stream_progress: if total_size > 0 {
                downloaded as f64 / total_size as f64
            } else {
                0.0
            },
            swarm_connections: peers,
            swarm_paused: false,
            swarm_size: peers,
            is_finished: total_size > 0 && downloaded >= total_size,
            has_metadata: total_size > 0,
        }
    }

    async fn add_trackers(&self, _trackers: Vec<String>) -> Result<()> {
        Ok(())
    }

    async fn get_file_reader(
        &self,
        file_idx: usize,
        _start_offset: u64,
        _priority: u8,
        _bitrate: Option<u64>,
        _intent: crate::backend::priorities::PlaybackIntent,
    ) -> Result<Box<dyn FileStreamTrait>> {
        let stream = self
            .handle
            .clone()
            .stream(file_idx)
            .context("Failed to stream from librqbit")?;
        Ok(Box::new(stream))
    }

    async fn get_files(&self) -> Vec<BackendFileInfo> {
        let mut files = Vec::new();
        if let Some(m) = self.handle.metadata.load_full() {
            if let Ok(iter) = m.info.iter_file_details() {
                for f in iter {
                    files.push(BackendFileInfo {
                        name: f.filename.to_string().unwrap_or_default(),
                        length: f.len,
                    });
                }
            }
        }
        files
    }

    async fn get_file_path(&self, _file_idx: usize) -> Option<String> {
        // librqbit doesn't expose local file paths easily
        // Return None to fall back to HTTP URL probing
        None
    }

    async fn prepare_file_for_streaming(&self, _file_idx: usize) -> Result<()> {
        Ok(())
    }

    async fn keep_file_downloading(&self, _file_idx: usize) -> Result<()> {
        Ok(())
    }

    async fn clear_file_streaming(&self, _file_idx: usize) -> Result<()> {
        Ok(())
    }

    async fn wait_for_piece_ready(
        &self,
        _file_idx: usize,
        _offset: u64,
        _timeout: Duration,
        _intent: crate::backend::priorities::PlaybackIntent,
    ) -> Result<PieceReadiness> {
        Ok(PieceReadiness {
            ready: true,
            piece: -1,
            ready_pieces: 1,
            target_pieces: 1,
            elapsed_ms: 0,
            peers: 0,
            download_rate: 0,
            reason: "librqbit-reader".to_string(),
        })
    }
}

impl Clone for LibrqbitHandle {
    fn clone(&self) -> Self {
        Self {
            handle: self.handle.clone(),
            info_hash: self.info_hash.clone(),
            session: self.session.clone(),
        }
    }
}
