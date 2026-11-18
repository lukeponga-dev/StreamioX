# StreamioX Xbox torrent bridge

The bridge embeds Stremio's upstream EngineFS torrent engine with the librqbit backend in the StreamioX UWP process. A small Xbox-specific Axum HTTP adapter exposes the Stremio-compatible stream route and translates HTTP byte ranges into seekable reads from torrent files:

`http://127.0.0.1:<dynamic-port>/stream/<infoHash>/<fileIdx>`

The native server handles torrent metadata, peer discovery, download buffering, HTTP byte ranges and seeking. StreamioX passes the returned local HTTP URL to `MediaPlayerElement`.

## Build prerequisites

- Rust stable and nightly toolchains
- Rust source component for nightly
- Visual Studio C++ build tools and Windows SDK
- x64 UWP target support built from Rust source (the target does not ship prebuilt stable standard-library artifacts)

Install the nightly toolchain and source component:

```powershell
rustup toolchain install nightly --profile minimal --component rust-src
```

Build the Xbox x64 native library from the repository root:

```powershell
cargo +nightly build -Z build-std=std,panic_abort --manifest-path .\native\torrent-bridge\Cargo.toml --target x86_64-uwp-windows-msvc --release
```

The output is:

`native/torrent-bridge/target/x86_64-uwp-windows-msvc/release/streamio_torrent_bridge.dll`

The StreamioX x64 MSBuild target runs this command and packages the resulting DLL. The UWP manifest includes `internetClientServer` because the embedded engine listens on the loopback interface.

## Next native-engine task: implement real BEP 19 web-seed fetching

**Status: not implemented. Web-seed playback is not fixed.** The bridge currently parses HTTP/HTTPS `ws=` values and retains the original magnet URI at the backend boundary, but the selected `librqbit 8.1.1` backend does not consume BEP 19 web seeds or expose a web-seed configuration API. Passing the magnet through is preservation only; it is not web-seed downloading.

This is the next native-engine task. Implement BEP 19 support in the native torrent backend (or replace/extend the backend with a demonstrably compatible implementation) so `ws=` URLs become active HTTP/HTTPS piece sources. Keep the managed bridge contract and Xbox UWP constraints intact.

### Required implementation and acceptance criteria

- Parse and pass every valid HTTP/HTTPS `ws=` URL from a magnet into the engine's web-seed source list; do not silently discard hints when `xs=` is also present.
- Implement BEP 19 URL-to-file mapping for single-file and multi-file torrents, including correct percent-encoding and path handling.
- Fetch the required byte ranges from web seeds, validate response status, `Content-Range`/length and byte counts, and feed the bytes through the torrent engine's normal piece verification path. Never trust unverified web-seed bytes as complete pieces.
- Support multiple web seeds, cancellation, bounded timeouts, redirects under an explicit safe policy, and fallback to peers/trackers when a web seed is unavailable or returns an unusable response.
- Apply URL/network safety checks appropriate to untrusted magnet input; do not permit arbitrary local/private-network or otherwise unsafe destinations without an explicit product decision.
- Add deterministic native integration tests using a local HTTP test server and known torrent fixtures. Tests must prove that the backend actually requests the `ws=` URL (including the expected path/range), consumes valid bytes, verifies pieces, and falls back correctly when a seed fails. Parser-only assertions do not satisfy this gate.
- Verify the implementation builds for the existing `x86_64-uwp-windows-msvc` target and that the HTTP client/network configuration works in the Xbox AppContainer.

Do not mark this task complete until the native backend's real HTTP requests and verified piece ingestion are demonstrated by tests. A successful parser test, magnet passthrough, ordinary peer download, or compile alone is insufficient.

## Current limitations

- The embedded engine uses the `librqbit` backend. It is the same engine selected by the upstream open-source stream-server project, not Stremio's proprietary `server.js`.
- Direct MP4 and other formats supported by Xbox MediaPlayerElement can be streamed through HTTP range requests. Codec/container compatibility still depends on Xbox playback support.
- FFmpeg setup is disabled in the Xbox configuration. HLS transcoding and format conversion are not currently enabled.
- The native binary is built for x64 Xbox. x86 and ARM64 builds are not wired up yet.
- This is not runtime-verified on an Xbox console yet. A successful native build and app build do not prove that AppContainer networking, native DLL loading, torrent peer connectivity or MediaPlayerElement loopback playback work on hardware.
