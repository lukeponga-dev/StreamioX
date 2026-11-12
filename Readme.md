# StreamioX

**A controller-first Stremio-compatible media client for Xbox One and Xbox Series X|S.**

StreamioX is a native Windows/UWP application built with C# and XAML. It is designed around the Xbox controller and the 10-foot television experience: browse media, inspect details, manage add-ons and account settings, find streams, and play compatible sources.

> **Project status: engineering prototype.** The project has build and packaging configuration, Stremio-compatible service integrations, and an embedded torrent bridge. Do not interpret that as Xbox Store validation or confirmed end-to-end operation on every console. See [known limitations](docs/PLAYBACK.md#known-limitations).

## Features and project areas

- **Xbox interface:** Home, Discover, Search, Library, Add-ons, Details, Player, Settings, and Diagnostics routes.
- **Controller-first interaction:** focus-aware XAML controls, D-pad navigation, and a consistent shell.
- **Stremio-compatible services:** account authentication, add-on manifests, catalogs, metadata, and stream resources.
- **Local persistence:** installed add-ons and account/session information are stored using Windows app storage and the credential vault.
- **Playback:** shared Windows `MediaPlayer`, FFmpegInteropX integration, and an optional native torrent-to-local-HTTP bridge.
- **Diagnostics:** playback diagnostics are written through the app's diagnostics service.

## Repository layout

| Path | Purpose |
|---|---|
| `StreamioX.App/` | UWP/XAML application, pages, view models, services, models, themes |
| `StreamioX.App/Views/` | Main shell and feature pages |
| `StreamioX.App/ViewModels/` | MVVM presentation logic |
| `StreamioX.App/Services/` | Stremio, account, add-on, catalog, stream, playback and torrent services |
| `StreamioX.App/Models/` | API and application data models; source-generated JSON metadata |
| `StreamioX.App/Navigation/` | Navigation routes and navigation service |
| `native/torrent-bridge/` | Rust UWP native library and local HTTP torrent adapter |
| `native/stream-server/` | Separate upstream stream-server research checkout; not the app's primary bridge project |
| `docs/` | Architecture, development, playback, privacy and planning documents |

## Architecture at a glance

The application is organized around a native C# client rather than a web wrapper:

1. XAML pages bind to view models.
2. View models call interfaces registered in `AppServices`.
3. Services use `StremioClient` for HTTP and source-generated JSON serialization.
4. Add-on manifests describe the catalog, metadata and stream resources each provider supports.
5. Playback resolves a selected stream to either a direct URL or a local URL served by the torrent bridge.

Read [Architecture](docs/ARCHITECTURE.md) for the service boundaries and request flows.

## Requirements

The current project configuration targets:

- Windows SDK / target framework: `net10.0-windows10.0.26100.0`
- Minimum target platform: `10.0.19041.0`
- UWP project tooling and the matching .NET SDK installed in Visual Studio
- Visual Studio with the Windows application development workload and C++ build tools
- Rust stable and nightly toolchains, plus `rust-src`, for the x64 torrent bridge

The project enables NativeAOT and UWP in its project file. Compatibility depends on the installed toolchain; verify the actual build on the development machine rather than assuming every SDK combination supports it.

## Build (Windows)

Open a Visual Studio Developer PowerShell in the repository root.

Install the Rust nightly source component if it is not already available:

```powershell
rustup toolchain install nightly --profile minimal --component rust-src
```

Restore and build the x64 app:

```powershell
dotnet restore .\StreamioX.slnx
dotnet build .\StreamioX.slnx -c Debug -p:Platform=x64
```

The x64 MSBuild target invokes Cargo to build the native bridge before packaging its DLL. The equivalent native build command is:

```powershell
cargo +nightly build -Z build-std=std,panic_abort --manifest-path .\native\torrent-bridge\Cargo.toml --target x86_64-uwp-windows-msvc --release
```

For an app build that deliberately skips the native bridge target (for example, to isolate managed compilation), pass `-p:BuildTorrentBridge=false`. This does not produce a package with working torrent playback unless a compatible bridge DLL is supplied separately.

Build and package settings live in `StreamioX.App/StreamioX.App.csproj`. Store signing certificates and machine-specific packaging settings should be configured locally; do not commit private signing keys or credentials.

More setup and troubleshooting notes: [Development guide](docs/DEVELOPMENT.md).

## Configuration and local data

- The app uses Windows `ApplicationData` for local settings, cache and add-on storage.
- Stremio account auth keys are stored in Windows `PasswordVault`; older LocalSettings tokens are migrated when possible.
- Add-on manifests are installed from HTTP(S) manifest URLs and cached locally.
- The torrent engine receives app-container-safe local data/cache paths. It must not depend on a repository or project directory at runtime.
- No API keys should be added to source control. Review the privacy and platform permission implications before changing storage or networking.

## Documentation

- [Architecture and request flows](docs/ARCHITECTURE.md)
- [Development, build and troubleshooting](docs/DEVELOPMENT.md)
- [Playback and torrent bridge](docs/PLAYBACK.md)
- [Privacy policy](docs/PRIVACY-POLICY.md)
- [Store listing](docs/STORE-LISTING.md)
- [Phase 1 execution plan](docs/PHASE-1-EXECUTION-PLAN.md)
- [Native torrent bridge README](native/torrent-bridge/README.md)

## Scope and non-goals

StreamioX implements Stremio-compatible client protocols in C#. The Rust torrent bridge is a focused native playback component; the app does not require the Stremio Rust core as its foundational application architecture. The separate `native/stream-server` directory is research material and should not be confused with the x64 bridge built by the app project.

## Contributing

Keep changes focused and preserve the controller-first experience. When changing API models, update the source-generated JSON metadata in `StremioJsonContext`. When changing native exports, keep the Rust ABI and C# `LibraryImport` declarations in sync. Update the relevant documentation when build requirements, service flows, or runtime capabilities change.
