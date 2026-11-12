# StreamioX development guide

## Environment

The current app project declares:

- .NET target: `net10.0-windows10.0.26100.0`
- Minimum Windows platform: `10.0.19041.0`
- UWP enabled through `UseUwp`
- Platforms: x86, x64 and arm64
- Runtime identifiers: win-x86, win-x64 and win-arm64
- NativeAOT enabled
- MSIX tooling enabled
- x64 torrent bridge build integrated into MSBuild by default

Install the matching .NET SDK, Windows SDK, Visual Studio UWP tooling and C++ build tools. The UWP + NativeAOT combination is toolchain-sensitive; a successful restore alone does not prove that the app can be compiled, packaged or run.

## Rust bridge prerequisites

The x64 bridge uses a nightly Rust toolchain and builds the standard library from source for `x86_64-uwp-windows-msvc`.

```powershell
rustup toolchain install nightly --profile minimal --component rust-src
```

Check the target/toolchain availability locally before attempting the integrated build.

## Build from the repository root

Use a Visual Studio Developer PowerShell:

```powershell
dotnet restore .\StreamioX.slnx
dotnet build .\StreamioX.slnx -c Debug -p:Platform=x64
```

The app project's x64 MSBuild target runs the native Cargo build and copies the resulting DLL into the app package output. To build the native component separately:

```powershell
cargo +nightly build -Z build-std=std,panic_abort --manifest-path .\native\torrent-bridge\Cargo.toml --target x86_64-uwp-windows-msvc --release
```

To isolate a managed build from the native build target:

```powershell
dotnet build .\StreamioX.App\StreamioX.App.csproj -c Debug -p:Platform=x64 -p:BuildTorrentBridge=false
```

Skipping the bridge target is a diagnostic/build-isolation option, not a complete torrent-enabled deployment.

## Visual Studio workflow

1. Open `StreamioX.slnx`.
2. Select the `StreamioX.App` startup project.
3. Select x64 for the current native torrent bridge configuration.
4. Choose Debug for development or Release for package validation.
5. Build before deploying. Check both managed build output and the Cargo target output when torrent support is included.
6. Test controller focus, back navigation, catalog loading, sign-in, stream selection and playback independently.

The native bridge currently has only an x64 UWP target configured. Although the managed project lists x86 and ARM64, do not infer that those architectures have a working torrent bridge.

## Adding or changing Stremio models

- Update the relevant model in `StreamioX.App/Models`.
- Register any serialized types/properties in `StremioJsonContext`.
- Use the generated `JsonTypeInfo` path rather than reflection-based serializer overloads.
- Check the manifest's declared resources, types, ID prefixes and catalog extras before constructing an endpoint.
- Keep cancellation tokens flowing through HTTP and provider operations.
- Isolate individual provider failures where the feature is designed to aggregate results.

## Adding a service or view model

- Define an interface under `Services/Interfaces` when the service is consumed by view models or multiple features.
- Implement the service under `Services`.
- Register the implementation in `AppServices.Configure()`.
- Register view models with the appropriate lifetime (the current page view models are transient).
- Keep UI-specific state in the view model and platform-specific playback/native code behind services.

## Troubleshooting notes

### Source-generated JSON metadata missing

If serialization throws that type metadata is not registered, add the model type to `StremioJsonContext` and rebuild. Do not switch back to reflection-based serialization as a quick workaround under the current AOT settings.

### Native DLL missing or entry point not found

- Confirm the Cargo build completed for `x86_64-uwp-windows-msvc`.
- Confirm `streamio_torrent_bridge.dll` is copied into the x64 app output/package.
- Compare C# `LibraryImport` entry-point names with the exports in the Rust bridge.
- Ensure the DLL architecture matches the app architecture.

### Native bridge starts but cannot access its data directory

The app must pass explicit paths inside its Windows app-container storage. The native engine must not attempt to infer a project directory or write to the source checkout. Review `AppServices.InitializeEngineAsync` and `TorrentBridgeService`.

### Credential Vault reports an element/credential not found

A stale PasswordVault record can be returned by lookup even though its secret can no longer be retrieved. The authentication service handles this case by discarding the broken credential and allowing legacy migration or signed-out startup. Avoid making credential-vault retrieval a hard app-startup dependency.

### Catalog is empty

- Confirm the Add-ons page shows at least one installed provider with catalog capability.
- Check the manifest's resource and type declarations.
- Confirm the provider's catalog ID and `/catalog/{type}/{id}.json` route.
- Search only catalogs that advertise the `search` extra.
- Review provider-specific errors; an empty merged result does not necessarily mean the entire Stremio service is down.

### XAML build or generated binding errors

Check the first XAML compiler error rather than only the downstream C# errors. Confirm that controls, converters, resource keys and binding types exist in the current UWP/XAML version. Do not assume APIs available in WinUI 3 are also available in this UWP project.

## Packaging and signing

MSIX settings are in `StreamioX.App.csproj`, the package manifest and the publish profiles. Signing certificates and package identity must be configured for the local development or Store workflow. Do not commit private PFX files, passwords, auth keys or other credentials. Validate Store identity and package architecture before distribution.

## Validation expectations

Record what was actually run. A successful XML parse, source inspection, managed build, native build, MSIX package, emulator run and physical Xbox test are different levels of validation. Do not describe the app as console-verified unless the complete workflow has been exercised on the target hardware.
