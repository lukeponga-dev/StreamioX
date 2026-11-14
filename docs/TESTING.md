# Dependency-injection tests

The test project validates the application's actual registration method without launching the Xbox app or compiling/loading the native torrent bridge.

## Run locally

From the repository root, use PowerShell:

```powershell
.\scripts\test-di.ps1
```

The script uses Visual Studio MSBuild to compile the UWP app assembly (the project contains XAML-generated code), explicitly sets BuildTorrentBridge=false, then runs the .NET test project without rebuilding its already-built project reference.

## Coverage

- ServiceProvider construction with ValidateScopes and ValidateOnBuild
- Resolution of every concrete application service registration
- All registered ViewModels resolve and have transient lifetime
- Core services and the navigation service resolve as singletons
- Singleton, scoped, and transient lifetime semantics
- Shared and typed HttpClient registrations
- StreamioXOptions binding from configuration and propagation to HttpClient settings

The test process does not invoke AppServices.InitializeEngineAsync, start the torrent server, or call native bridge entry points. The build disables the native bridge build target.

## CI requirements

Run on a Windows agent with the .NET 10 SDK, Visual Studio MSBuild, and the Windows 10/11 SDK required by the app's UWP target. The native Rust toolchain and torrent bridge are not required for this test job.
