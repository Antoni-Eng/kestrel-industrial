# Starts the native Windows Qdrant binary (no Docker / WSL required).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$tools = Join-Path $root "tools\qdrant"
$exe = Join-Path $tools "qdrant.exe"

if (-not (Test-Path $exe)) {
    Write-Host "Downloading Qdrant Windows binary..."
    New-Item -ItemType Directory -Force -Path $tools | Out-Null
    $zip = Join-Path $tools "qdrant-windows.zip"
    Invoke-WebRequest `
        -Uri "https://github.com/qdrant/qdrant/releases/download/v1.15.5/qdrant-x86_64-pc-windows-msvc.zip" `
        -OutFile $zip
    Expand-Archive -Path $zip -DestinationPath $tools -Force
}

$listening = Get-NetTCPConnection -LocalPort 6333 -ErrorAction SilentlyContinue
if ($listening) {
    Write-Host "Qdrant already listening on :6333"
    exit 0
}

Start-Process -FilePath $exe -WorkingDirectory $tools -WindowStyle Minimized
Write-Host "Started $exe"
Write-Host "Dashboard/API: http://localhost:6333"
