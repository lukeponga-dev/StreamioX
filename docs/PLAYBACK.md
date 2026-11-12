# Playback and torrent bridge

## Playback pipeline

StreamioX accepts stream results from installed Stremio-compatible add-ons. A stream can represent a direct media URL or a torrent-backed source.

The intended high-level flow is:

1. The user opens a media details page.
2. `StreamService` queries installed add-ons that declare the `stream` resource and support the selected type/content ID.
3. The details/player flow presents the returned sources.
4. A direct source is passed into the playback layer with its supported headers.
5. A torrent source is converted by `TorrentBridgeService` into a loopback HTTP URL.
6. The shared Windows `MediaPlayer` / `MediaPlayerElement` consumes the selected URL.

The app uses a shared `PlaybackSessionService` to own the `MediaPlayer` instance. Its warm-up method touches the playback session during startup so the native media pipeline can be initialized before the first Play action.

## Direct HTTP playback

Direct HTTP sources are subject to the codecs, containers, protocols and authentication headers supported by the Xbox/Windows media stack and the configured FFmpeg integration. A valid URL does not guarantee that the device can decode the media.

The playback service filters unsafe or transport-controlled headers before forwarding provider headers. The implementation should continue to reject malformed header names/values and avoid overriding transport-managed headers such as Host, Range, Content-Length and Connection.

## Torrent playback

### Native component

`native/torrent-bridge` builds `streamio_torrent_bridge.dll`, a Rust library embedding the upstream EngineFS torrent engine with the librqbit backend and an Xbox-specific Axum HTTP adapter.

The adapter exposes a local route:

```text
http://127.0.0.1:<port>/stream/<infoHash>/<fileIdx>
```

The native server is responsible for torrent metadata, peer discovery, buffering, byte-range reads and seeking. The managed app does not implement the BitTorrent protocol itself.

### Managed startup

`AppServices.InitializeEngineAsync` creates an `rqbit_dht` directory in `ApplicationData.Current.LocalFolder` and starts the registered `ITorrentBridgeService`.

`TorrentBridgeService` then:

- Creates its DHT/configuration directory under app-local storage.
- Creates a torrent cache under `ApplicationData.Current.LocalCacheFolder`.
- Starts the native library on the requested loopback port (11470); the actual bound port is returned by the bridge.
- Exposes the local server address to the managed playback flow.
- Stops the native server through the exported stop function and clears cached local stream URLs.

The engine must receive explicit writable app-container paths. It must never search for a source repository, current project directory or developer-machine path at runtime.

### Stream URL construction

For torrent sources, the managed service normalizes the info hash from hexadecimal or Base32, or extracts it from a magnet URI. It preserves provider-supplied magnet parameters such as exact torrent sources (`xs=`) and web seeds (`ws=`), along with trackers.

The Stremio file index is preserved. A missing file index is represented as `-1`, allowing the native server to choose the best video file rather than blindly selecting file zero (which may be a sample, subtitle or metadata file).

### Native ABI

The current managed declarations call these exports:

- `streamio_torrent_bridge_start`
- `streamio_torrent_bridge_stop`
- `streamio_torrent_bridge_last_error`

The start call receives UTF-8, null-terminated configuration and cache paths, a requested port, and an output port. Keep these declarations synchronized with the Rust exports. The bridge README documents the native build command and output location.

## FFmpeg integration

The project references `FFmpegInteropX.UWP` and contains a managed FFmpeg playback service. However, the current Xbox torrent bridge README states that FFmpeg setup is disabled in the Xbox configuration; HLS transcoding and format conversion are not enabled by that native bridge. Treat the configured playback path as format-dependent and verify it against the actual build and runtime.

Do not assume that the presence of the NuGet package means every stream format or FFmpeg code path is active.

## Known limitations

- The native torrent bridge is currently configured for x64 UWP only. The managed project lists other architectures, but the bridge is not wired for x86 or ARM64.
- A successful Rust build does not prove that native DLL loading works inside the Xbox app container.
- A successful app build/package does not prove loopback networking, torrent peer connectivity, byte-range seeking or media playback works on console hardware.
- Direct playback is limited by Xbox/Windows codec and container support.
- FFmpeg-based HLS transcoding and format conversion are not currently enabled in the Xbox bridge configuration.
- The native bridge README explicitly states that the full path has not yet been runtime-verified on an Xbox console.

## Diagnostics checklist

When a stream fails, capture the failure stage rather than treating every issue as a player error:

1. **Provider discovery:** did any installed add-on declare a matching stream resource?
2. **Provider request:** did `/stream/{type}/{id}.json` return a response?
3. **Source validation:** is the URL or magnet URI valid, and are required headers present?
4. **Torrent startup:** did the native DLL load and return a listening port?
5. **Local route:** can the app reach the loopback HTTP endpoint?
6. **Media source:** did MediaPlayer accept the URL and any required headers?
7. **Decode:** does the selected codec/container work on the target device?
8. **Seeking:** do HTTP byte-range requests return the expected partial content?

Use the app's playback diagnostics output and preserve the relevant native/managed error messages. Avoid logging auth keys, passwords, or other credentials.
