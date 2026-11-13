# Build the distributable mod zip: the vendored BepInEx 5 game-folder layout (BepInEx\core\,
# winhttp.dll, doorstop_config.ini, .doorstop_version) plus the Release plugin output under
# BepInEx\plugins\Earplay (the host, Contracts, Core and Module dlls, lang files), prism.dll next to
# Word Play.exe, and a BepInEx.cfg that keeps the loader's console window closed. The zip root IS the
# game folder, so the installer (and a manual user) extracts it straight into the game dir.
#
# The Release host is built without the dev server (its sources are excluded and Roslyn is not
# referenced), so only the four mod dlls ship.
#
# Adapted from the Non-Visual Calculus installer by Rashad Naqeeb (MIT),
# https://github.com/rashadnaqeeb/NonVisualCalculus

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$propsPath = Join-Path $scriptDir "Directory.Build.props"
$releaseDir = Join-Path $scriptDir "releases"
$stageDir = Join-Path $scriptDir "obj\release-stage"

[xml]$props = Get-Content $propsPath
$versionNode = $props.SelectSingleNode("/Project/PropertyGroup/Version")
if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
    throw "Could not read Version from $propsPath"
}
$version = $versionNode.InnerText.Trim()

# The vendored loader is unpacked as the game-folder layout (the same files setup-bepinex.ps1
# copies); its changelog.txt is BepInEx's own release notes and is not shipped.
$bepinexDir = Join-Path $scriptDir "third_party\bepinex"
$prismDll = Join-Path $scriptDir "third_party\prism\prism.dll"
$hostOutDir = Join-Path $scriptDir "src\Earplay\bin\Release\netstandard2.1"
$contractsOutDir = Join-Path $scriptDir "src\Earplay.Contracts\bin\Release\netstandard2.0"
$coreOutDir = Join-Path $scriptDir "src\Earplay.Core\bin\Release\netstandard2.0"
$moduleOutDir = Join-Path $scriptDir "src\Earplay.Module\bin\Release\netstandard2.1"
$langDir = Join-Path $scriptDir "lang"
$zipPath = Join-Path $releaseDir "Earplay-v$version.zip"

$loaderFiles = @("winhttp.dll", "doorstop_config.ini", ".doorstop_version", "BepInEx\core\BepInEx.dll")
foreach ($required in $loaderFiles) {
    if (-not (Test-Path (Join-Path $bepinexDir $required))) {
        throw "The vendored BepInEx under third_party\bepinex lacks $required"
    }
}
if (-not (Test-Path $prismDll)) {
    throw "Required file not found: $prismDll"
}

Push-Location $scriptDir
try {
    # The dlls are picked from the output dirs by name, so a stale DLL there (a renamed or removed
    # assembly dotnet build no longer owns) would ship. Start them empty.
    foreach ($dir in @($hostOutDir, $contractsOutDir, $coreOutDir, $moduleOutDir)) {
        if (Test-Path $dir) {
            Remove-Item -LiteralPath $dir -Recurse -Force
        }
    }

    dotnet build Earplay.slnx -c Release -v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed with exit code $LASTEXITCODE"
    }

    $modDlls = @(
        (Join-Path $hostOutDir "Earplay.dll"),
        (Join-Path $contractsOutDir "Earplay.Contracts.dll"),
        (Join-Path $coreOutDir "Earplay.Core.dll"),
        (Join-Path $moduleOutDir "Earplay.Module.dll")
    )
    foreach ($required in $modDlls) {
        if (-not (Test-Path $required)) {
            throw "Release build output not found: $required"
        }
    }

    if (Test-Path $stageDir) {
        Remove-Item -LiteralPath $stageDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force $stageDir | Out-Null
    New-Item -ItemType Directory -Force $releaseDir | Out-Null

    Copy-Item -LiteralPath (Join-Path $bepinexDir "winhttp.dll"), (Join-Path $bepinexDir "doorstop_config.ini"), (Join-Path $bepinexDir ".doorstop_version") -Destination $stageDir
    $stageCoreDir = Join-Path $stageDir "BepInEx\core"
    New-Item -ItemType Directory -Force $stageCoreDir | Out-Null
    Copy-Item -Path (Join-Path $bepinexDir "BepInEx\core\*") -Destination $stageCoreDir -Recurse

    # No BepInEx console window: the mod speaks, and a second window only steals focus from the
    # screen reader. BepInEx keeps the values it finds in BepInEx.cfg and fills in the rest on the
    # first launch, so seeding this one key is enough (setup-bepinex.ps1 does the same for a dev box).
    $configDir = Join-Path $stageDir "BepInEx\config"
    New-Item -ItemType Directory -Force $configDir | Out-Null
    [IO.File]::WriteAllText((Join-Path $configDir "BepInEx.cfg"), "[Logging.Console]`r`nEnabled = false`r`n")

    # The same mod files the Debug post-build target deploys, minus the dev server's Roslyn deps
    # and the PDBs.
    $pluginDir = Join-Path $stageDir "BepInEx\plugins\Earplay"
    New-Item -ItemType Directory -Force $pluginDir | Out-Null
    foreach ($dll in $modDlls) {
        Copy-Item -LiteralPath $dll -Destination $pluginDir
    }
    Copy-Item -LiteralPath $prismDll -Destination $stageDir

    if (Test-Path $langDir) {
        $langFiles = @(Get-ChildItem (Join-Path $langDir "*.txt"))
        if ($langFiles.Count -gt 0) {
            $stageLangDir = Join-Path $pluginDir "lang"
            New-Item -ItemType Directory -Force $stageLangDir | Out-Null
            Copy-Item -Path $langFiles.FullName -Destination $stageLangDir
        }
    }

    if (Test-Path $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    # Get-ChildItem -Force, not a "*" wildcard: Compress-Archive's wildcard skips hidden items, and
    # .doorstop_version must ship.
    Compress-Archive -LiteralPath (Get-ChildItem -LiteralPath $stageDir -Force).FullName -DestinationPath $zipPath -Force

    Remove-Item -LiteralPath $stageDir -Recurse -Force

    Write-Host "Release zip: $zipPath"
}
finally {
    Pop-Location
}
