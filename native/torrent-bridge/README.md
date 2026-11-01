# StreamioX Xbox torrent bridge

The bridge embeds the upstream Rust stream-server library in the StreamioX UWP process. It starts a loopback-only HTTP listener and exposes the upstream Stremio-compatible stream route:

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

## Current limitations

- The embedded engine uses the `librqbit` backend. It is the same engine selected by the upstream open-source stream-server project, not Stremio's proprietary `server.js`.
- Direct MP4 and other formats supported by Xbox MediaPlayerElement can be streamed through HTTP range requests. Codec/container compatibility still depends on Xbox playback support.
- FFmpeg setup is disabled in the Xbox configuration. HLS transcoding and format conversion are not currently enabled.
- The native binary is built for x64 Xbox. x86 and ARM64 builds are not wired up yet.
- This is not runtime-verified on an Xbox console yet. A successful native build and app build do not prove that AppContainer networking, native DLL loading, torrent peer connectivity or MediaPlayerElement loopback playback work on hardware.
