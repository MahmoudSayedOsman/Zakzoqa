<#
.SYNOPSIS
    Runs Zakzoqa's automated tests headlessly and reports the result.

.DESCRIPTION
    The same suite the Test Runner window runs, driven from a command line so it can be a git hook,
    a scheduled job, or a CI step. Unity's own exit code is unreliable for this - it reports 0 on
    some failures and 2 on a clean run with warnings - so the NUnit XML is parsed and the verdict
    comes from the counts, not from the process.

.PARAMETER Mode
    EditMode, PlayMode, or All (default).

.PARAMETER P0Only
    Runs only tests tagged [Category("P0")]. These are the ones that mean the build is broken:
    save, economy, purchases, transformations. A pipeline can gate on this and let the rest report
    without blocking.

.EXAMPLE
    .\Tools\run-tests.ps1
    .\Tools\run-tests.ps1 -Mode PlayMode
    .\Tools\run-tests.ps1 -P0Only
#>
[CmdletBinding()]
param(
    [ValidateSet("EditMode", "PlayMode", "All")]
    [string]$Mode = "All",

    [switch]$P0Only,

    # Left blank to auto-discover. Pass explicitly on a build agent where the Hub is not installed.
    [string]$UnityExe = ""
)

$ErrorActionPreference = "Stop"

$projectPath = Split-Path -Parent $PSScriptRoot
$resultsDir  = Join-Path $projectPath "TestResults"
$version     = (Get-Content (Join-Path $projectPath "ProjectSettings\ProjectVersion.txt") |
                Select-String "^m_EditorVersion:" | ForEach-Object { $_.Line.Split(":")[1].Trim() })

Write-Host "Zakzoqa test run" -ForegroundColor Cyan
Write-Host "  project : $projectPath"
Write-Host "  editor  : $version"

# --- locate Unity -----------------------------------------------------------
# Matched against the project's own ProjectVersion.txt rather than "whatever is installed": running
# the suite on a different editor version is a different test run, and silently doing so would make
# a green CI meaningless.
if (-not $UnityExe) {
    $candidates = @(
        "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe",
        "D:\Unity\Hub\Editor\$version\Editor\Unity.exe",
        "$env:ProgramFiles\Unity\Hub\Editor\$version\Editor\Unity.exe"
    )
    $UnityExe = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $UnityExe -or -not (Test-Path $UnityExe)) {
    Write-Host "Unity $version not found. Pass -UnityExe with the full path." -ForegroundColor Red
    exit 1
}
Write-Host "  unity   : $UnityExe"

New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null

$modes = if ($Mode -eq "All") { @("EditMode", "PlayMode") } else { @($Mode) }
$overallFailed = 0
$overallTotal  = 0

foreach ($m in $modes) {
    $xml = Join-Path $resultsDir "$m.xml"
    $log = Join-Path $resultsDir "$m.log"
    if (Test-Path $xml) { Remove-Item $xml }

    Write-Host ""
    Write-Host "--- $m ---" -ForegroundColor Yellow

    $unityArgs = @(
        "-batchmode"
        "-runTests"
        "-projectPath", $projectPath
        "-testPlatform", $m
        "-testResults", $xml
        "-logFile", $log
        # No graphics device. Batchmode still initialises D3D without it, which is pointless for a
        # test run and is one more thing that can fail on a headless build agent.
        "-nographics"
    )
    if ($P0Only) { $unityArgs += @("-testCategory", "P0") }

    & $UnityExe @unityArgs | Out-Null

    if (-not (Test-Path $xml)) {
        # No results file means the run never got as far as the tests - a compile error, a missing
        # package, a licence problem. The log is the only thing that can say which.
        Write-Host "  no results written. Last 30 lines of the log:" -ForegroundColor Red
        if (Test-Path $log) { Get-Content $log -Tail 30 | ForEach-Object { "    $_" } }
        exit 1
    }

    [xml]$doc = Get-Content $xml
    $run = $doc.'test-run'
    $overallTotal  += [int]$run.total
    $overallFailed += [int]$run.failed

    $colour = if ([int]$run.failed -gt 0) { "Red" } else { "Green" }
    Write-Host ("  total {0}  passed {1}  failed {2}  skipped {3}  ({4}s)" -f
                $run.total, $run.passed, $run.failed, $run.skipped, $run.duration) -ForegroundColor $colour


    # Failures and skips reported apart. They used to share one list headed FAILED, so six
    # deliberately ignored cases read as six broken ones directly under a line saying "failed 0".
    foreach ($case in $doc.SelectNodes("//test-case[@result='Failed']")) {
        Write-Host ("    FAILED  " + $case.name) -ForegroundColor Red
        if ($case.failure.message) {
            $msg = ($case.failure.message.InnerText -replace "`r?`n", " ")
            Write-Host ("            " + $msg.Substring(0, [Math]::Min(200, $msg.Length))) -ForegroundColor DarkGray
        }
    }

    foreach ($case in $doc.SelectNodes("//test-case[@result='Skipped']")) {
        Write-Host ("    skipped " + $case.name) -ForegroundColor DarkYellow
    }
}

Write-Host ""
if ($overallFailed -gt 0) {
    Write-Host "$overallFailed of $overallTotal failed." -ForegroundColor Red
    exit 1
}

Write-Host "$overallTotal passed." -ForegroundColor Green
exit 0
