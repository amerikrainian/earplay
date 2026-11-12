# setup-bepinex.ps1 - Install the vendored BepInEx 5 (third_party/bepinex) into the Word Play folder.
# Idempotent: safe to re-run (e.g. after a game update or a Steam file verification wipes it). Then
# run build.ps1 to deploy the mod. Word Play is a Mono game, so there is no first-launch proxy step.

$ErrorActionPreference = "Stop"

# --- Locate the game install: WORDPLAY_DIR env var, else Steam library folders, else the default.
$Game = $env:WORDPLAY_DIR
if (-not $Game) {
    $RegSteam = (Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -Name InstallPath -ErrorAction SilentlyContinue).InstallPath
    $DefaultSteam = if ($RegSteam) { $RegSteam } else { "C:\Program Files (x86)\Steam" }
    $SteamPaths = @()
    if (Test-Path "$DefaultSteam\steamapps") { $SteamPaths += $DefaultSteam }
    $LibFolders = "$DefaultSteam\steamapps\libraryfolders.vdf"
    if (Test-Path $LibFolders) {
        $content = Get-Content $LibFolders -Raw
        [regex]::Matches($content, '"path"\s+"([^"]+)"') | ForEach-Object {
            $p = $_.Groups[1].Value -replace '\\', '\'
            if ($p -ne $DefaultSteam -and (Test-Path "$p\steamapps")) { $SteamPaths += $p }
        }
    }
    foreach ($steam in $SteamPaths) {
        $candidate = "$steam\steamapps\common\Word Play"
        if (Test-Path "$candidate\Word Play.exe") { $Game = $candidate; break }
    }
    if (-not $Game) { $Game = "C:\Program Files (x86)\Steam\steamapps\common\Word Play" }
}
if (-not (Test-Path "$Game\Word Play.exe")) {
    Write-Host "ERROR: Word Play not found at: $Game" -ForegroundColor Red
    Write-Host "Set the WORDPLAY_DIR environment variable to the game folder." -ForegroundColor Red
    exit 1
}

$Src = "$PSScriptRoot\third_party\bepinex"
if (-not (Test-Path "$Src\BepInEx\core\BepInEx.dll")) {
    Write-Host "ERROR: vendored BepInEx not found under third_party\bepinex" -ForegroundColor Red
    exit 1
}

Write-Host "Installing BepInEx into $Game ..." -ForegroundColor Cyan

# The vendored folder is the game-folder layout (winhttp.dll, doorstop_config.ini, .doorstop_version,
# BepInEx\core); its changelog stays behind.
Copy-Item "$Src\winhttp.dll", "$Src\doorstop_config.ini", "$Src\.doorstop_version" -Destination $Game -Force
New-Item -ItemType Directory -Force "$Game\BepInEx\core" | Out-Null
Copy-Item "$Src\BepInEx\core\*" -Destination "$Game\BepInEx\core" -Recurse -Force

# --- No BepInEx console window: the mod speaks, and a second window only steals focus from the
# screen reader. BepInEx keeps whatever values it finds in BepInEx.cfg and fills in the rest on the
# first launch, so seeding just this key is enough; on re-runs the existing file is patched in place.
$ConfigDir = "$Game\BepInEx\config"
$Config = "$ConfigDir\BepInEx.cfg"
$Seed = "[Logging.Console]`r`nEnabled = false`r`n"
New-Item -ItemType Directory -Force $ConfigDir | Out-Null
if (Test-Path $Config) {
    $text = [IO.File]::ReadAllText($Config)
    if ($text -match "\[Logging\.Console\]") {
        $patched = [regex]::Replace($text, "(\[Logging\.Console\][^\[]*?^Enabled = )true", "`$1false", "Multiline, Singleline")
    } else {
        $patched = $text.TrimEnd() + "`r`n`r`n" + $Seed
    }
    if ($patched -ne $text) { [IO.File]::WriteAllText($Config, $patched) }
} else {
    [IO.File]::WriteAllText($Config, $Seed)
}
Write-Host "BepInEx console window disabled ([Logging.Console] Enabled = false in BepInEx\config\BepInEx.cfg)." -ForegroundColor Cyan

Write-Host ""
Write-Host "BepInEx installed. Now run build.ps1 to deploy the mod, then launch Word Play through Steam" -ForegroundColor Cyan
Write-Host "(steam.exe -applaunch 3586660)." -ForegroundColor Cyan
