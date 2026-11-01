# Upstream stream-server source

This directory contains the MIT-licensed Rust streaming engine from:

- Upstream: https://github.com/stremio-native/stream-server
- Upstream revision: `f585ab6`
- Upstream server crate version: `0.1.8`
- License: see [LICENSE](LICENSE)

StreamioX uses the upstream server library and its EngineFS torrent implementation. The Xbox bridge selects the pure-Rust `librqbit` backend; it does not enable the optional C++ libtorrent backend.

## Xbox-specific changes

The vendored source is adapted for a UWP-hosted library:

- Desktop tray and settings-GUI dependencies are excluded from the Xbox build.
- The tray module is replaced by the upstream no-op stats shim for the UWP target.
- The Windows thread-count diagnostic is disabled on UWP.
- The startup yt-dlp prewarm is disabled on UWP.
- The server crate's EngineFS dependency disables its default libtorrent feature so the selected `librqbit` backend is the only torrent backend.
- Windows executable resource generation is skipped for the UWP target.

The stream server's MIT license and copyright notice are retained. Keep this file and the LICENSE file with future updates to the vendored source.
