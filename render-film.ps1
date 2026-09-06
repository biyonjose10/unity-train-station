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
    [string]$Unity = "C:\Users\biyon\Unity\Editors\6000.0.83f1\Editor\Unity.exe",
    [string]$Output = "Recordings\AshfordHill.mp4"
)

$ErrorActionPreference = "Stop"

$proj = $PSScriptRoot
$logDir = Join-Path $proj "Logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Find-Ffmpeg {
    $cmd = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    # winget installs it under the user's package directory and only adds it to PATH for new shells.
    $guess = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-9.0.1-full_build\bin\ffmpeg.exe"
    if (Test-Path $guess) { return $guess }

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

if (-not (Test-Path $Unity)) { throw "Unity not found at $Unity" }
$ffmpeg = Find-Ffmpeg

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
