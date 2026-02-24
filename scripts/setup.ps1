<# .SYNOPSIS
    Focus Assistant setup script for Windows.
    Installs prerequisites: ffmpeg (audio capture), Vosk model, checks .NET SDK.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$VoskModelName = 'vosk-model-small-en-us-0.15'
$VoskModelUrl  = "https://alphacephei.com/vosk/models/$VoskModelName.zip"
$ModelDir      = Join-Path $env:USERPROFILE '.focus-assistant' 'models'
$ModelPath     = Join-Path $ModelDir $VoskModelName

Write-Host '=== Focus Assistant Setup (Windows) ===' -ForegroundColor Cyan
Write-Host ''

# --- 1. ffmpeg (audio capture) ---
$ffmpegPath = Get-Command ffmpeg -ErrorAction SilentlyContinue
if ($ffmpegPath) {
    Write-Host "[OK] ffmpeg is already installed ($($ffmpegPath.Source))" -ForegroundColor Green
} else {
    Write-Host '[*] Installing ffmpeg via winget...' -ForegroundColor Yellow
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if ($winget) {
        winget install --id Gyan.FFmpeg -e --accept-source-agreements --accept-package-agreements
        if ($LASTEXITCODE -ne 0) {
            Write-Host '[WARN] winget install failed. Please install ffmpeg manually from https://ffmpeg.org' -ForegroundColor Red
        } else {
            Write-Host '[OK] ffmpeg installed. You may need to restart your terminal for PATH changes.' -ForegroundColor Green
        }
    } else {
        Write-Host '[ERROR] winget not found. Install ffmpeg manually from https://ffmpeg.org' -ForegroundColor Red
        Write-Host '        Or: choco install ffmpeg  (if Chocolatey is installed)' -ForegroundColor Red
    }
}

# --- 2. Vosk model ---
if (Test-Path $ModelPath) {
    Write-Host "[OK] Vosk model already exists at $ModelPath" -ForegroundColor Green
} else {
    Write-Host "[*] Downloading Vosk model: $VoskModelName..." -ForegroundColor Yellow
    if (-not (Test-Path $ModelDir)) {
        New-Item -ItemType Directory -Path $ModelDir -Force | Out-Null
    }

    $TmpZip = Join-Path $env:TEMP "vosk-model-$([guid]::NewGuid().ToString('N')).zip"
    try {
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $VoskModelUrl -OutFile $TmpZip -UseBasicParsing

        Write-Host "[*] Extracting model to $ModelDir..." -ForegroundColor Yellow
        Expand-Archive -Path $TmpZip -DestinationPath $ModelDir -Force

        if (Test-Path $ModelPath) {
            Write-Host "[OK] Vosk model installed at $ModelPath" -ForegroundColor Green
        } else {
            Write-Host "[ERROR] Extraction succeeded but model directory not found at $ModelPath" -ForegroundColor Red
            Write-Host "        Check contents of $ModelDir and rename if needed." -ForegroundColor Red
        }
    } finally {
        if (Test-Path $TmpZip) { Remove-Item $TmpZip -Force }
    }
}

# --- 3. .NET SDK check ---
$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnetCmd) {
    $dotnetVersion = & dotnet --version 2>$null
    Write-Host "[OK] .NET SDK found ($dotnetVersion)" -ForegroundColor Green
} else {
    Write-Host '[WARN] .NET SDK not found. Install .NET 8+ from https://dotnet.microsoft.com/download' -ForegroundColor Red
}

# --- 4. Windows TTS check (built-in) ---
Write-Host '[OK] Windows SAPI text-to-speech is built-in (System.Speech)' -ForegroundColor Green

Write-Host ''
Write-Host '=== Setup complete! ===' -ForegroundColor Cyan
Write-Host 'Run your assistant with:  .\run.ps1'
