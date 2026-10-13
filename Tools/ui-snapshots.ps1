<#
.SYNOPSIS
    Renders every UI screen in every language to PNGs in TestResults/Snapshots.

.DESCRIPTION
    Runs Zakzoqa.Tests.UiSnapshots with a graphics device (the normal test run has none). Look at
    the Arabic pictures before a screen or a string lands: Shadow Escape's layouts broke in Arabic
    in ways no test could see. Unity must not have the project open.
#>
[CmdletBinding()]
param([string]$UnityExe = "")

$ErrorActionPreference = "Stop"
$projectPath = Split-Path -Parent $PSScriptRoot
$outDir      = Join-Path $projectPath "TestResults\Snapshots"
$version     = (Get-Content (Join-Path $projectPath "ProjectSettings\ProjectVersion.txt") |
                Select-String "^m_EditorVersion:" | ForEach-Object { $_.Line.Split(":")[1].Trim() })

if (-not $UnityExe) { $UnityExe = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe" }
if (-not (Test-Path $UnityExe)) { Write-Host "Unity $version not found. Pass -UnityExe." -ForegroundColor Red; exit 1 }

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Get-ChildItem $outDir -Filter *.png -ErrorAction SilentlyContinue | Remove-Item

$env:ZAKZOQA_SNAPSHOTS = $outDir
& $UnityExe -batchmode -runTests -projectPath $projectPath -testPlatform PlayMode `
    -testFilter "Zakzoqa.Tests.UiSnapshots" `
    -testResults (Join-Path $projectPath "TestResults\Snapshots.xml") `
    -logFile (Join-Path $projectPath "TestResults\Snapshots.log") | Out-Null

$pngs = Get-ChildItem $outDir -Filter *.png -ErrorAction SilentlyContinue
if (-not $pngs) {
    Write-Host "No pictures written. See TestResults\Snapshots.log." -ForegroundColor Red
    exit 1
}
$pngs | ForEach-Object { Write-Host "  $($_.FullName)" }
exit 0
