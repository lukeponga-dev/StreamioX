// Purpose: Provides a small executable example for exercising the torrent bridge outside the Xbox app.

use anyhow::{Context, Result};
use librqbit::{
    AddTorrent, AddTorrentOptions, CreateTorrentOptions, Session, SessionOptions,
    create_torrent,
};
use std::{env, path::PathBuf, time::Duration};

#[tokio::main]
async fn main() -> Result<()> {
    let input = env::args()
        .nth(1)
        .context("usage: cargo run --example seed_local -- <video-file>")?;
    let file = PathBuf::from(input);
    let folder = file
        .parent()
        .context("video file has no parent folder")?
        .to_path_buf();
    let file_name = file
        .file_name()
        .and_then(|name| name.to_str())
        .context("video filename is not valid UTF-8")?;

    let torrent = create_torrent(
        &file,
        CreateTorrentOptions {
            name: Some(file_name),
            piece_length: Some(16 * 1024),
        },
    )
    .await
    .context("failed to create local test torrent")?;
    let info_hash = torrent.info_hash().as_string();
    let torrent_bytes = torrent.as_bytes()?;

    let session = Session::new_with_opts(
        folder.clone(),
        SessionOptions {
            listen_port_range: Some(50000..50001),
            disable_dht: true,
            disable_dht_persistence: true,
            ..Default::default()
        },
    )
    .await
    .context("failed to start local test seeder")?;

    let handle = session
        .add_torrent(
            AddTorrent::from_bytes(torrent_bytes),
            Some(AddTorrentOptions {
                output_folder: Some(folder.to_string_lossy().into_owned()),
                disable_trackers: true,
                overwrite: true,
                ..Default::default()
            }),
        )
        .await
        .context("failed to add local test torrent to seeder")?
        .into_handle()
        .context("seeder did not return a torrent handle")?;

    handle
        .wait_until_completed()
        .await
        .context("local test file did not verify as a complete torrent")?;

    let port = session
        .tcp_listen_port()
        .context("local seeder did not bind a TCP port")?;
    println!("SEED_READY=true");
    println!("INFO_HASH={info_hash}");
    println!("LISTEN_PORT={port}");
    println!("FILE_NAME={file_name}");
    println!("FILE_BYTES={}", std::fs::metadata(&file)?.len());

    // Keep both the session and torrent handle live while StreamioX connects.
    loop {
        let _ = session.tcp_listen_port();
        let _ = handle.info_hash();
        tokio::time::sleep(Duration::from_secs(30)).await;
    }
}
