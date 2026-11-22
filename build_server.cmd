@echo off
setlocal
set "JCHRONO_ROOT=%~dp0"
set "JCHRONO_SCRIPT=%~f0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& ([scriptblock]::Create(((Get-Content -LiteralPath $env:JCHRONO_SCRIPT -Raw -Encoding UTF8) -split '(?m)^# POWERSHELL BEGIN\r?$',2)[1]))"
set "JCHRONO_EXIT_CODE=%ERRORLEVEL%"
if /i not "%~1"=="--no-pause" pause
exit /b %JCHRONO_EXIT_CODE%
# POWERSHELL BEGIN
$ErrorActionPreference = 'Stop'
$projectRoot = $env:JCHRONO_ROOT
$runtimeDirectory = Join-Path $projectRoot 'artifacts/server'
$executablePath = Join-Path $projectRoot 'src/JChrono.Web/bin/Debug/net10.0/JChrono.Web.exe'
$controlLock = $null
$transcribing = $false
$failed = $false
function Invoke-Checked([scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE. See artifacts/server/build_server.log." }
}
try {
    New-Item -ItemType Directory -Path $runtimeDirectory -Force | Out-Null
    $controlLock = [System.IO.File]::Open((Join-Path $runtimeDirectory 'control.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    Start-Transcript -Path (Join-Path $runtimeDirectory 'build_server.log') -Force | Out-Null
    $transcribing = $true
    $runningServer = Get-Process -Name JChrono.Web -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $executablePath }
    if ($runningServer) { throw 'Server is running. Use stop.cmd before building, then run.cmd after building.' }
    Set-Location -LiteralPath $projectRoot
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet-home'
    Invoke-Checked { dotnet restore JChrono.slnx --configfile NuGet.Config }
    Invoke-Checked { dotnet build JChrono.slnx --no-restore }
    $message = 'Server build completed. Use run.cmd to start it.'
    Write-Host $message
} catch {
    $failed = $true
    $message = "Server build failed: $($_.Exception.Message)"
    Write-Host $message
} finally {
    if ($transcribing) { Stop-Transcript | Out-Null }
    if ($controlLock) { $controlLock.Dispose() }
}
Write-Host ''
if ($failed) {
    Write-Host ('=' * 72) -ForegroundColor Red
    Write-Host '>>> ВЫПОЛНЕНИЕ СКРИПТА ЗАВЕРШЕНО С ОШИБКОЙ <<<' -ForegroundColor Red
    Write-Host ('=' * 72) -ForegroundColor Red
    exit 1
}
Write-Host ('=' * 72) -ForegroundColor Green
Write-Host '>>> ВЫПОЛНЕНИЕ СКРИПТА УСПЕШНО ЗАВЕРШЕНО <<<' -ForegroundColor Green
Write-Host ('=' * 72) -ForegroundColor Green
exit 0
