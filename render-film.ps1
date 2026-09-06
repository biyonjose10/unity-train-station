<#
    Builds the scenes, renders the film headlessly, and muxes the result into an MP4.

    Nothing here needs the Unity editor window. The render runs in batch mode with
    Time.captureFramerate pinned, so the output plays at real speed no matter how long the
    machine actually takes per frame.

    Usage:
        .\render-film.ps1                       # full film, 1280x720
        .\render-film.ps1 -Probe                # 8 seconds at 640x360, to check the pipeline
        .\render-film.ps1 -SkipBuild            # reuse the scenes already on disk
#>

[CmdletBinding()]
param(
    [switch]$Probe,
    [switch]$SkipBuild,
    [string]$Unity = "",
    [string]$Output = "Recordings\AshfordHill.mp4"
)

$ErrorActionPreference = "Stop"

$proj = $PSScriptRoot
$logDir = Join-Path $proj "Logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Find-Unity {
    # Unity Hub can be told to install editors anywhere, and on a machine without admin rights it
    # will not be under Program Files at all. Search the usual places rather than assuming one.
    $roots = @(
        (Join-Path $HOME "Unity\Editors"),
        (Join-Path $env:ProgramFiles "Unity\Hub\Editor"),
        (Join-Path ${env:ProgramFiles(x86)} "Unity\Hub\Editor")
    )

    foreach ($root in $roots) {
        if (-not (Test-Path $root)) { continue }

        $found = Get-ChildItem $root -Directory -ErrorAction SilentlyContinue |
                 Sort-Object Name -Descending |
                 ForEach-Object { Join-Path $_.FullName "Editor\Unity.exe" } |
                 Where-Object { Test-Path $_ } |
                 Select-Object -First 1

        if ($found) { return $found }
    }

    throw "Unity editor not found. Pass -Unity <path to Unity.exe>."
}

function Find-Ffmpeg {
    $cmd = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    # winget installs it under the user's package directory and only adds it to PATH for new
    # shells, so a freshly installed ffmpeg is invisible to the session that installed it.
    $pkgRoot = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages"
    if (Test-Path $pkgRoot) {
        $found = Get-ChildItem $pkgRoot -Filter "ffmpeg.exe" -Recurse -Depth 4 -ErrorAction SilentlyContinue |
                 Select-Object -First 1
        if ($found) { return $found.FullName }
    }

    throw "ffmpeg not found. Install it with: winget install --id Gyan.FFmpeg -e"
}

function Invoke-Unity([string]$method, [string]$logName) {
    $log = Join-Path $logDir $logName
    Write-Host "Unity: $method  (log: $log)"

    $p = Start-Process -FilePath $Unity -NoNewWindow -Wait -PassThru -ArgumentList @(
        "-batchmode", "-projectPath", $proj, "-executeMethod", $method, "-logFile", $log
    )

    $errors = Select-String -Path $log -Pattern "error CS" -ErrorAction SilentlyContinue
    if ($errors) {
        $errors | Select-Object -First 10 -ExpandProperty Line | ForEach-Object { Write-Host $_ }
        throw "Compilation failed."
    }

    if ($p.ExitCode -ne 0) { throw "$method failed with exit code $($p.ExitCode)." }
}

if ([string]::IsNullOrWhiteSpace($Unity)) { $Unity = Find-Unity }
if (-not (Test-Path $Unity)) { throw "Unity not found at $Unity" }

$ffmpeg = Find-Ffmpeg
Write-Host "Unity:  $Unity"
Write-Host "ffmpeg: $ffmpeg"

if (-not $SkipBuild) {
    Invoke-Unity "TrainStation.Build.BuildAll.BatchBuild" "build.log"
}

$method = if ($Probe) { "TrainStation.Build.RenderFilm.BatchProbe" }
          else        { "TrainStation.Build.RenderFilm.BatchRender" }

Remove-Item (Join-Path $proj "Capture") -Recurse -Force -ErrorAction SilentlyContinue
Invoke-Unity $method "render.log"

$frameDir = Join-Path $proj "Capture\frames"
$wav = Join-Path $proj "Capture\audio.wav"
$frames = (Get-ChildItem $frameDir -Filter *.jpg -ErrorAction SilentlyContinue).Count

Write-Host "Rendered $frames frames."
if ($frames -lt 10) { throw "Render produced almost nothing; check $logDir\render.log." }
if (-not (Test-Path $wav)) { throw "No audio was captured." }

$outPath = Join-Path $proj $Output
New-Item -ItemType Directory -Force -Path (Split-Path $outPath) | Out-Null

Write-Host "Muxing -> $outPath"
& $ffmpeg -y -loglevel error `
    -framerate 30 -i (Join-Path $frameDir "f%05d.jpg") `
    -i $wav `
    -c:v libx264 -pix_fmt yuv420p -crf 19 -preset medium `
    -c:a aac -b:a 192k -shortest `
    $outPath

if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed with exit code $LASTEXITCODE." }

$size = (Get-Item $outPath).Length
Write-Host ("Done: {0} ({1:N1} MB)" -f $outPath, ($size / 1MB))
