# StreamioX architecture

## Design goals

StreamioX is a native, controller-first Stremio-compatible client for Xbox. Its primary application code is C# and UWP/XAML. It communicates with Stremio-compatible add-on servers using their HTTP/JSON resources and delegates media playback to the Windows media stack.

The architecture favors clear service boundaries and source-generated JSON metadata for NativeAOT-conscious serialization. The Rust component is limited to the torrent bridge; it is not the app's foundational UI or protocol layer.

## Application layers

| Area | Responsibility |
|---|---|
| `Views` | XAML pages, controls, and UI event integration |
| `ViewModels` | Presentation state and commands, implemented with CommunityToolkit.Mvvm |
| `Models` | Stremio manifests, catalog/meta/stream responses, account and playback data |
| `Services` | HTTP, authentication, add-on lifecycle, catalog/search, metadata, stream resolution, playback |
| `Navigation` | Route enum and navigation service |
| `Themes`, `Layout`, `Helpers` | Shared visual resources, TV layout constants, focus and conversion helpers |
| `AppServices` | Dependency-injection composition root and torrent-engine initialization |
| `native/torrent-bridge` | Rust library exposing a small native ABI and local HTTP stream endpoint |

## Dependency injection

`AppServices.Configure()` builds the service provider. It registers the navigation and Stremio services as singletons and the page view models as transient services. Pages or view models resolve dependencies through this provider.

Important registrations include:

- `StremioClient` — typed HTTP client used for add-on HTTP requests.
- `IAuthService` / `AuthService` — Stremio account session and account add-on synchronization.
- `IAddonService` / `AddonService` — installed manifest collection and provider capabilities.
- `ICatalogService` / `CatalogService` — catalog and supported search requests.
- `IMetadataService` / `MetadataService` — metadata lookup.
- `IStreamService` / `StreamService` — stream resource lookup.
- `ITorrentBridgeService` / `TorrentBridgeService` — native torrent server lifecycle and local URL generation.
- `FFmpegPlaybackService` and `PlaybackSessionService` — playback integration and shared player session.

When adding a service, register its interface and implementation in `AppServices.Configure()` and keep view models dependent on interfaces where practical.

## Stremio protocol flow

### Add-on installation and refresh

1. The user supplies an add-on manifest URL through the Add-ons experience, or account synchronization imports an add-on descriptor.
2. `AddonService` normalizes the URL to an HTTP(S) `manifest.json` endpoint.
3. The manifest is fetched through `StremioClient` and validated for an ID, name, and supported resources.
4. The add-on's resource names, supported types, ID prefixes, catalogs and manifest metadata are retained locally.
5. Installed manifests are refreshed at initialization. A failed refresh is isolated so the last-known-good local manifest can remain available.
6. The official Cinemeta manifest is installed on first initialization when it is not already present. It is protected from uninstall through the service.

Installed add-ons are persisted in the app's local storage. Account add-on synchronization is handled separately by `AuthService`.

### Catalog and search

`CatalogService` selects installed providers that declare the catalog resource and support the requested media type. Catalog requests use the Stremio-compatible route:

```text
/catalog/{type}/{catalogId}.json
```

Search is not assumed to exist on every catalog. The implementation checks the manifest's catalog extras for a `search` extra and avoids sending incomplete requests when other required extras are not supplied.

Catalog results from providers are combined and duplicate IDs are filtered. Provider failures are collected/isolated rather than treated as proof that all providers are unavailable.

### Metadata

Metadata lookup is exposed through `IMetadataService` / `MetadataService`. Provider selection is based on the installed add-on's declared `meta` resource, type and ID-prefix support. Check the current implementation and manifest declarations before assuming a provider supports every content ID.

### Stream resolution

1. The selected media type and content ID are passed to `StreamService`.
2. The add-on service selects providers declaring the `stream` resource and matching type/ID support.
3. Each provider is queried through `/stream/{type}/{id}.json`.
4. Provider requests run concurrently; successful provider results can be surfaced while slower providers continue.
5. A provider failure is isolated so it does not suppress results from other add-ons.

Stream responses are cached briefly in memory. A returned stream may be a direct HTTP source or a torrent-backed source.

## Authentication and account sync

`AuthService` supports:

- Stremio device-link flow (QR/browser approval and polling).
- Email/password login.
- Existing auth-key login.
- Profile refresh and logout.
- Import/export synchronization of the user's account add-on collection.

Auth keys are persisted in Windows `PasswordVault`. The service includes migration from a legacy LocalSettings token. User profile data is stored in app-local settings. A missing or stale credential-vault entry should not prevent the app from starting signed out.

Account add-on synchronization is intentionally conservative: if remote descriptors cannot be imported, it does not overwrite the remote collection with a partial local list. Only supported HTTP(S) manifest transports are imported by the current implementation; legacy/IPFS transports need dedicated adapters.

## Serialization and NativeAOT

`StremioClient` uses `System.Text.Json` with `StremioJsonContext` as its type-info resolver. New request/response model types used in serialization must be registered in the source-generated JSON context. Avoid relying on reflection-based serialization, which is disabled or restricted by the current NativeAOT configuration.

## Navigation

The current route enum contains Home, Discover, Library, Search, Add-ons, Settings, Details, Player and Diagnostics. The shell and navigation service own route transitions; feature pages should not introduce parallel navigation mechanisms without a deliberate design change.

## Native bridge boundary

The managed app calls three exported native functions from `streamio_torrent_bridge.dll`:

- `streamio_torrent_bridge_start`
- `streamio_torrent_bridge_stop`
- `streamio_torrent_bridge_last_error`

The native server is started with explicit app-container-safe configuration and cache paths. The managed code must remain aligned with the actual exported ABI; do not assume additional exports exist unless both sides are updated and built.

See [Playback and torrent bridge](PLAYBACK.md) for the stream lifecycle and platform limitations.
