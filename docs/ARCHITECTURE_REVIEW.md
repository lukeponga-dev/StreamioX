# StreamioX architecture and reliability review

**Review date:** 15 November 2026  
**Scope:** Details UX, playback orchestration, metadata, stream discovery, watch history, dependency injection, serialization, HTTP client lifetime, and native bridge TODOs.

## Executive summary

The highest-impact changes are implemented in the app layer. Playback actions no longer wait for every add-on before attempting playback, stream selection no longer depends on provider response order, metadata providers are queried concurrently, synopsis fields are normalized, and the player now exposes elapsed/total/remaining and buffered progress. Continue Watching and Library navigation now pass an explicit auto-resume intent.

The Release x64 UWP app assembly builds successfully with the native torrent bridge disabled, and the DI/source-generation test suite passes. This is not an Xbox runtime certification: controller focus, platform share UI, native torrent playback, codecs, and provider-specific stream behavior still need hands-on validation on target hardware.

## Root-cause analysis

### 1. Details action buttons

The shared primary/secondary button styles set colors and dimensions but relied on the platform's implicit button template. The Details page also composed icon and text children without consistently setting their foregrounds. That left the visible background blocks dependent on theme/template defaults, while nested content could inherit an unsuitable foreground.

**Changes**
- Added a shared FluentButtonTemplate with an explicit ContentPresenter.
- Added pointer-over, pressed, and disabled visual states.
- Applied consistent corner radius, padding, minimum height, typography, and system focus visuals.
- Set foregrounds explicitly on nested icons and labels.
- Added automation names to the primary and secondary actions.
- Kept Xbox focus scaling/border feedback and stopped provider results from stealing focus from the action row.
- Added Trailer (only when metadata provides a valid HTTP(S) URL), Add/Remove from Library, Mark Watched, Share, Resume, and Watch Now actions.

### 2. Playback failures and resume

The Details ViewModel used Streams[0] in Watch Now, episode play, and resume paths. That made playback depend on provider completion order. Those commands also awaited the complete stream-discovery task, even though StreamService was already publishing provider results incrementally.

The player only considered resume complete once the session became seekable. For non-seekable sources, that state could remain false indefinitely and block watch-progress persistence.

**Changes**
- Added a common wait-for-first-playable-stream path for Watch Now, Play Episode, and Resume.
- Added deterministic source scoring: viable HTTP sources are preferred, then quality hints (2160p/4K, 1080p, 720p), HDR, and codec hints.
- Playback starts from the best source currently available instead of waiting for the slowest provider.
- Movie and episode resume use the saved position and the correct movie/episode identifiers.
- Continue Watching and Library entries carry an explicit auto-resume navigation request.
- If a source cannot seek, the player reports that the saved position cannot be restored, continues from the beginning, and enables normal progress saving after a short grace period.
- Trailer playback deliberately omits library identity so trailers do not overwrite movie watch history.

### 3. Missing synopsis

The Movie DTO only mapped the Stremio description field. Providers returning overview or plot could return valid text that was never copied into the property displayed by the Details page.

**Changes**
- Added overview and plot mappings.
- Merged full metadata and catalog preview values without overwriting non-empty fields.
- Added a loading synopsis placeholder and a clear "No description available." fallback.
- Home hero synopsis now falls back through description, overview, and plot.
- Metadata diagnostics record provider response time, synopsis length, and missing-synopsis warnings.

### 4. Stream discovery delays

StreamService already ran provider requests concurrently and published results as they arrived. The user-facing playback commands negated that benefit by waiting for the final Task.WhenAll result and selecting the first array item. Provider requests were also unbounded.

**Changes**
- Provider requests are ordered by observed latency and run with a maximum of four active requests.
- Each provider has a timeout and status updates: Searching, Complete, Failed, Timed out, or Cancelled.
- Provider results continue to be dispatched to the UI as soon as they arrive.
- Added a Cancel search action and loading progress/skeleton feedback.
- Existing request coalescing and stream-response caching are retained.
- Metadata discovery now uses bounded parallel requests, returns the first valid result, and caches metadata responses for two minutes.

## Implementation summary

| Area | Implementation |
|---|---|
| Details buttons | Explicit Fluent-inspired template, content presenter, pointer states, explicit foregrounds, accessibility names, Xbox focus behavior |
| Playback entry points | Shared first-playable-stream wait and preferred-source ranking |
| Continue Watching | Home and Library entry points pass auto-resume intent |
| Synopsis | Description/overview/plot fallback and merge across Details, Home, Search, and Library |
| Discovery feedback | ProgressRing, skeleton cards, provider states, cancel action |
| Stream discovery | Four-provider concurrency cap, latency ordering, timeout, progressive UI callbacks |
| Metadata discovery | Four-provider concurrency cap, first valid result, eight-second provider timeout, two-minute cache |
| Player timeline | Current, total, remaining time and buffered percentage |
| Watch history | Periodic, pause/exit, and app-suspend saving; clamped position, last-watched display, corrupt-file recovery, atomic replacement |
| DI | Named factory-managed auth HttpClient; existing typed StremioClient remains factory-managed |
| Serialization | Explicit source-generated registrations for stream DTOs and nested transport models |
| Tests | Dedicated DI test project; source-generated DTO/synopsis/trailer deserialization test |

## Before vs after

| Concern | Before | After |
|---|---|---|
| Watch Now / episode / resume | Waited for all providers; chose Streams[0] | Starts on first playable result; ranks currently available sources |
| Provider fan-out | All providers launched at once | Latency-ordered, maximum four concurrent requests |
| Metadata | Provider requests were sequential | Bounded parallel requests; first valid response wins |
| Provider feedback | Limited feedback while waiting | Per-provider state, progress ring, skeletons, cancel |
| Resume on non-seekable stream | Resume could remain pending and suppress progress saves | User-facing fallback to beginning; progress saving resumes |
| Synopsis mapping | description only | description, overview, plot fallback |
| Player timeline | Current and remaining values | Current, total, remaining, buffered percentage |
| Library progress | Home cards showed progress; Library lacked a bar/timestamp | Home, Library, and Details show progress and last-watched context |

No wall-clock performance benchmark was run against live add-ons during this review. The expected improvement is reduced time-to-first-playable-stream and removal of serial metadata latency; actual milliseconds depend on provider latency, network, cache state, and stream type.

## Validation performed

- Release x64 UWP app assembly build: **passed** with BuildTorrentBridge=false.
- DI and serialization tests: **8 passed, 0 failed, 0 skipped**.
- git diff --check: run after implementation; no whitespace errors were reported at the time of the final check.
- The test build reports an MSB3277 WindowsBase version conflict involving a transitive WebView2 WPF reference. It is a warning, not a test failure.
- No Xbox console/device playback session or visual regression capture was performed in this pass.

Run the tests on a Windows CI agent with Visual Studio MSBuild and the Windows SDK:

~~~powershell
.\scripts\test-di.ps1
~~~

The test script builds without the native torrent bridge. It still requires the Windows UWP toolchain; it is not a Linux-only test target.

## Native bridge and remaining technical debt

The TODO audit found no TODO that currently blocks the direct HTTP Stremio metadata/catalog/stream path. Remaining placeholders are mostly diagnostics or lower-priority native work:

- native/stream-server/bindings/libtorrent-sys/cpp/wrapper.cpp: peer connection_type currently uses placeholder value 0; this affects peer diagnostics, not stream playback.
- native/stream-server/enginefs/src/backend/librqbit.rs: per-file downloaded byte counts remain zero; aggregate torrent progress is separate.
- Archive handling retains split-detection and async archive TODOs; not part of the normal Stremio stream lookup path.
- Player progress is now saved on app suspension. App.xaml.cs still has a TODO for restoring broader navigation/application state after suspension.
- SettingsViewModel.ClearImageCache: command remains unimplemented; the Home artwork cache is currently bounded by clearing at its size threshold.
- External-player deep links and authenticated route guards are not implemented.
- The Share UI and trailer action depend on OS/provider support. Trailer is hidden unless metadata contains a valid HTTP(S) trailer URL.
- The preferred-source score is intentionally heuristic. It does not yet use measured bitrate, file size, language, provider reliability history, or device codec support.
- A real provider integration test suite is still needed for authentication headers, timeout behavior, malformed manifests, duplicate sources, and cancellation races.
- Player buffering and seeking behavior varies by source and codec; the new UI exposes the MediaPlaybackSession state but does not guarantee every source is seekable.

## Prioritized roadmap

### P0 — validate and stabilize
1. Run the updated Details page on Xbox and desktop; verify button labels, hover/focus states, scaling, and XYFocus traversal.
2. Test Watch Now, Play Episode, Resume, Continue Watching, Library, and Search with at least one direct HTTP source and one torrent-backed source.
3. Verify movie and episode resume across app navigation, pause, and process termination.
4. Validate provider cancellation and timeouts against slow/offline add-ons.
5. Verify trailer and Share actions on target platforms; confirm Mark Watched updates the intended movie/episode history.

### P1 — reliability and integration coverage
1. Add provider-level tests using a local fake Stremio add-on server.
2. Add tests for source ranking, synopsis merging, runtime parsing, and resume fallback.
3. Add Xbox UI smoke tests or a repeatable manual QA checklist with screenshots.
4. Verify NativeAOT serialization for every new DTO and add a CI assertion for generated metadata.
5. Exercise the native torrent bridge and document supported stream formats/limitations.

### P2 — architecture and performance
1. Add configurable provider concurrency/timeouts and collect anonymized latency diagnostics.
2. Evaluate provider reliability/quality ranking based on real playback outcomes.
3. Add a dedicated artwork cache abstraction with explicit eviction and a working Clear Image Cache action.
4. Add tests for corrupt-library preservation and atomic file replacement.
5. Persist navigation state across suspension and add lifecycle/reactivation tests; player watch progress is already saved on suspension.

### P3 — future
1. Authentication route guards with return-to-destination behavior.
2. External player integration/deep links.
3. Richer playback options (subtitle/audio selection per stream, buffering diagnostics, codec capability hints).
