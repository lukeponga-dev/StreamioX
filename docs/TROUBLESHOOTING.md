# StreamioX troubleshooting

Use this guide to isolate failures by stage. Capture the first meaningful error and its full stack trace before changing code.

## 1. Build and packaging

### UWP or Windows SDK target is missing

- Confirm the .NET SDK and Windows SDK versions required by `StreamioX.App.csproj` are installed.
- Confirm Visual Studio has the UWP / Windows application development tooling and C++ build tools.
- Open the solution in Visual Studio and inspect the first MSBuild error. Later XAML or generated-code errors may be consequences of the first failure.

### NativeAOT serialization reports missing metadata

The app uses source-generated `System.Text.Json` metadata. When a model is serialized or deserialized, make sure its type is registered in `StremioJsonContext` and that the call uses the generated type information. Do not rely on reflection-based serializer overloads under the current NativeAOT configuration.

### Native DLL or entry point cannot be loaded

- Build the bridge for the exact app architecture.
- Confirm `streamio_torrent_bridge.dll` is present in the app output/package.
- Compare the C# `LibraryImport` entry-point names with the Rust exports.
- Check that the app package and DLL architectures match.
- Remember that the currently documented torrent bridge target is x64 UWP.

## 2. Startup and XAML

### App exits before the Home page appears

1. Reproduce once and note the timestamp.
2. Inspect the startup diagnostics log in the packaged app's local temporary storage.
3. Find the first `XAML UNHANDLED EXCEPTION`, not merely the last log line.
4. Record the HRESULT, exception type and complete stack trace.
5. Check the failing page, resource, binding or service initialization path.

A first-chance exception is not automatically fatal. Treat it as the cause only when it is followed by an unhandled exception or a reproducible failure at the same call site.

### XAML binding fails for a collection property

For NativeAOT/UWP binding scenarios, avoid assuming every property on a framework collection is exposed as a bindable custom property. If binding to a collection count fails, expose a simple bindable scalar property on the ViewModel (for example, `StreamCount`) and raise `PropertyChanged` when the collection changes.

## 3. Add-ons, catalogs and metadata

### Catalog is empty

- Confirm at least one installed add-on advertises the `catalog` resource for the requested media type.
- Check the catalog ID and the provider's manifest.
- Confirm the request route is `/catalog/{type}/{catalogId}.json`.
- Only send search extras supported by that catalog.
- Inspect provider-specific HTTP errors; one failing add-on should not be treated as proof that all providers are unavailable.

### Metadata or streams are missing

- Confirm the content ID and media type are valid for the provider.
- Check manifest resource declarations, supported types and ID prefixes.
- Verify the expected `meta` or `stream` resource route.
- Separate provider discovery failures from individual provider request failures.

## 4. Torrent bridge and local HTTP

### Bridge fails to start

- Confirm the native DLL exists and matches the app architecture.
- Inspect the bridge's returned error string and startup diagnostics.
- Ensure configuration/DHT and cache paths are inside the app's writable `ApplicationData` folders.
- Do not let the native engine infer a project directory or write into the source checkout.
- Confirm the requested port and returned bound port are handled consistently.

### Local stream request times out or returns an HTTP error

- Confirm the bridge reports that it is listening.
- Check the loopback URL and selected file index.
- Inspect the HTTP status and response headers from the local endpoint.
- For media seeking, verify that byte-range requests receive a valid `206 Partial Content` response where expected.
- Distinguish torrent metadata/peer acquisition timeouts from media decoding failures.

## 5. Media playback

### Player opens but playback stops or the app crashes

Capture the full exception stack and identify the exact property or method that failed. Do not assume the last visible player action is the root cause: an exception may occur later in a timer, event handler or UI update callback.

Some FFmpeg-backed `MediaPlaybackSession` implementations may not support every WinRT session property. In particular, reading `BufferingProgress` can raise `E_NOINTERFACE` (`0x80004002`) on an unsupported implementation. The Player page should treat this optional metric defensively: catch the unsupported-interface exception, disable further reads for that session, and continue updating playback time and state. Do not repeatedly access a property after it has been identified as unsupported.

### Direct URL fails

- Confirm the URL is reachable from the app's network context.
- Check redirects, required headers and response content type.
- Ensure transport-managed headers such as `Host`, `Range`, `Content-Length` and `Connection` are not overwritten.
- Confirm the codec/container is supported by the active Windows/Xbox media path.

### FFmpeg reports an invalid source or probe failure

- Capture the original URL host, final URL host, HTTP status, content type and range response.
- Confirm the response body is media data rather than an HTML error page or provider landing page.
- Check whether the configured FFmpeg path is actually enabled for the target build. A package reference alone does not prove that every FFmpeg feature is active.

## 6. Diagnostics and safe reporting

When reporting a failure, include:

- App build configuration and architecture (for example, Debug x64).
- Device/target (Windows development machine, emulator or physical Xbox).
- Exact reproduction steps and timestamp.
- First relevant exception, HRESULT and full stack trace.
- Relevant provider or local HTTP status and response headers.
- Whether the app continued, recovered or terminated.

Before sharing logs, redact auth keys, passwords, cookies, API keys, private URLs and personal information. Never commit credentials, private signing certificates or generated local secrets.

## 7. Validation checklist

A fix is not fully verified by a successful compile alone. Record each level separately:

- [ ] Source change reviewed.
- [ ] `git diff --check` passes.
- [ ] Managed app build passes.
- [ ] Native bridge build passes (when changed or required).
- [ ] MSIX package builds and contains the expected native assets.
- [ ] App launches on the intended target.
- [ ] Relevant user flow is exercised end to end.
- [ ] Physical Xbox behavior is verified before claiming console validation.
