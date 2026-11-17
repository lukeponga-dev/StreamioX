# StreamioX documentation

This folder contains the engineering documentation for the native Xbox client.

## Start here

- [Architecture](ARCHITECTURE.md) — application layers, dependency injection, Stremio protocol flows and native boundary.
- [Development guide](DEVELOPMENT.md) — prerequisites, Visual Studio workflow, build commands and implementation guidance.
- [Playback](PLAYBACK.md) — direct streams, torrent bridge, FFmpeg integration and playback pipeline.
- [Troubleshooting](TROUBLESHOOTING.md) — symptom-led diagnostics for build, startup, providers, native bridge and playback.
- [Testing](TESTING.md) — dependency-injection test coverage and how to run it.

## Product and planning

- [Privacy policy](PRIVACY-POLICY.md)
- [Store listing](STORE-LISTING.md)
- [Store listing draft](STORE-LISTING-DRAFT.md)
- [Phase 1 execution plan](PHASE-1-EXECUTION-PLAN.md)
- [Architecture review](ARCHITECTURE_REVIEW.md)

## Native components

- [Torrent bridge README](../native/torrent-bridge/README.md)

## Documentation principles

- Describe the implementation as it exists, and label planned or unverified behavior clearly.
- Keep build instructions aligned with the project file and scripts.
- Do not include credentials, auth tokens, private signing material, or personal data in examples or logs.
- Record the exact validation level: source inspection, managed build, native build, package, emulator, or physical Xbox test.
