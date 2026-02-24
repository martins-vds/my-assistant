<# .SYNOPSIS
    Focus Assistant run script for Windows.
    Auto-detects audio device and launches in voice or text mode.
#>
[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ExtraArgs
)

$ErrorActionPreference = 'SilentlyContinue'
$ProjectPath = Join-Path $PSScriptRoot '..' 'src' 'FocusAssistant.Cli'

# Try to detect a working audio input device via ffmpeg
$hasAudio = $false
$ffmpeg = Get-Command ffmpeg -ErrorAction SilentlyContinue
if ($ffmpeg) {
    # List DirectShow devices and check for audio
    $output = & ffmpeg -list_devices true -f dshow -i dummy 2>&1 | Out-String
    if ($output -match 'audio') {
        $hasAudio = $true
    }
}

$ErrorActionPreference = 'Stop'

if ($hasAudio) {
    Write-Host 'Audio device detected - starting in voice mode' -ForegroundColor Green
    & dotnet run --project $ProjectPath -- @ExtraArgs
} else {
    Write-Host 'No usable audio device found - starting in text mode' -ForegroundColor Yellow
    & dotnet run --project $ProjectPath -- --text @ExtraArgs
}
