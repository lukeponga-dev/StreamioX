$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $repositoryRoot "StreamioX.App\StreamioX.App.csproj"
$testProject = Join-Path $repositoryRoot "StreamioX.App.Tests\StreamioX.App.Tests.csproj"

$vswhere = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86) + "\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    throw "Visual Studio Installer vswhere.exe was not found. Install Visual Studio with MSBuild and UWP tooling."
}

$msbuild = & $vswhere -latest -products Microsoft.VisualStudio.Product.Community -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\amd64\MSBuild.exe" | Select-Object -First 1
if (-not $msbuild -or -not (Test-Path $msbuild)) {
    $msbuild = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)) "Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
}
if (-not (Test-Path $msbuild)) {
    throw "A Visual Studio Community MSBuild installation with UWP tooling was not found."
}

Write-Host "Building the UWP app assembly without the native torrent bridge..."
& $msbuild $appProject /t:Build /m /restore /nologo /v:minimal /p:Configuration=Release /p:Platform=x64 /p:BuildTorrentBridge=false
if ($LASTEXITCODE -ne 0) { throw "StreamioX.App build failed with exit code $LASTEXITCODE." }

Write-Host "Running DI tests..."
dotnet test $testProject -c Release -p:Platform=x64 -p:BuildTorrentBridge=false -p:BuildProjectReferences=false --logger "console;verbosity=minimal"
if ($LASTEXITCODE -ne 0) { throw "StreamioX DI tests failed with exit code $LASTEXITCODE." }
