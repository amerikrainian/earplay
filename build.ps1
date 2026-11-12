# build.ps1 - Build Earplay and deploy it into the game. Deploy itself is the host project's Debug
# post-build target (plugin + Contracts + Roslyn + Core + Module into BepInEx\plugins\Earplay,
# prism.dll next to Word Play.exe); this script checks BepInEx is set up and runs the build. Close the
# game first for a HOST change (its DLL is locked while the game runs); Core/Module changes deploy
# fine with the game running and take effect on POST /reload or F6.

param(
    [switch]$Help,
    [switch]$ModuleOnly
)

if ($Help) {
    Write-Host "Usage: .\build.ps1 [-ModuleOnly] [-Help]"
    Write-Host "  Builds the solution and deploys the mod into the game folder."
    Write-Host "  -ModuleOnly builds just the reloadable Core + Module (the hot loop)."
    Write-Host "  Run setup-bepinex.ps1 once first."
    exit 0
}

$ErrorActionPreference = "Stop"

$Game = $env:WORDPLAY_DIR
if (-not $Game) { $Game = "C:\Program Files (x86)\Steam\steamapps\common\Word Play" }
if (-not (Test-Path "$Game\Word Play.exe")) {
    Write-Host "ERROR: Word Play not found at: $Game (set WORDPLAY_DIR)" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path "$Game\BepInEx\core\BepInEx.dll")) {
    Write-Host "ERROR: BepInEx is not installed at $Game\BepInEx. Run setup-bepinex.ps1 first." -ForegroundColor Red
    exit 1
}

if ($ModuleOnly) {
    dotnet build "$PSScriptRoot\src\Earplay.Module\Earplay.Module.csproj" -c Debug
} else {
    dotnet build "$PSScriptRoot\Earplay.slnx" -c Debug
}
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build FAILED." -ForegroundColor Red
    exit 1
}
Write-Host ""
Write-Host "Done. Launch Word Play through Steam and listen for the startup line, or POST /reload if it is running." -ForegroundColor Cyan
