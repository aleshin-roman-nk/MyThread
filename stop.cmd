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
$statePath = Join-Path $runtimeDirectory 'process.json'
$executablePath = Join-Path $projectRoot 'src/JChrono.Web/bin/Debug/net10.0/JChrono.Web.exe'
$controlLock = $null
$failed = $false
try {
    New-Item -ItemType Directory -Path $runtimeDirectory -Force | Out-Null
    $controlLock = [System.IO.File]::Open((Join-Path $runtimeDirectory 'control.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    Write-Host 'Stopping JChrono server...'
    $message = 'No managed server is running.'
    if (Test-Path -LiteralPath $statePath) {
        $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
        $serverProcess = Get-Process -Id $state.ProcessId -ErrorAction SilentlyContinue
        if ($serverProcess -and $serverProcess.Path -eq $executablePath -and
            $serverProcess.StartTime.ToUniversalTime().Ticks -eq ([DateTimeOffset]$state.StartTime).UtcDateTime.Ticks) {
            Stop-Process -Id $serverProcess.Id -ErrorAction Stop
            if (!$serverProcess.WaitForExit(10000)) { throw 'Server did not stop within 10 seconds.' }
            $message = 'Server stopped.'
        }
        Remove-Item -LiteralPath $statePath
    }
} catch {
    $failed = $true
    $message = "Stop failed: $($_.Exception.Message)"
} finally {
    if ($controlLock) { $controlLock.Dispose() }
}
Write-Host $message
$message | Set-Content -LiteralPath (Join-Path $runtimeDirectory 'stop.log') -Encoding UTF8
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
