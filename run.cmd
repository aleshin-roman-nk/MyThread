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
$webDirectory = Join-Path $projectRoot 'src/JChrono.Web'
$executablePath = Join-Path $webDirectory 'bin/Debug/net10.0/JChrono.Web.exe'
$serverUrl = 'http://localhost:5080'
$controlLock = $null
$transcribing = $false
$failed = $false

try {
    New-Item -ItemType Directory -Path $runtimeDirectory -Force | Out-Null
    $controlLock = [System.IO.File]::Open((Join-Path $runtimeDirectory 'control.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    Start-Transcript -Path (Join-Path $runtimeDirectory 'run.log') -Force | Out-Null
    $transcribing = $true
    $alreadyRunning = $false
    if (Test-Path -LiteralPath $statePath) {
        $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
        $previousProcess = Get-Process -Id $state.ProcessId -ErrorAction SilentlyContinue
        # Check identity as Windows can reuse the PID of a stopped process.
        if ($previousProcess -and $previousProcess.Path -eq $executablePath -and
            $previousProcess.StartTime.ToUniversalTime().Ticks -eq ([DateTimeOffset]$state.StartTime).UtcDateTime.Ticks) {
            $alreadyRunning = $true
        }
        if (!$alreadyRunning) { Remove-Item -LiteralPath $statePath }
    }

    if ($alreadyRunning) {
        $message = "Server is already running: $serverUrl"
    } else {
        if (!(Test-Path -LiteralPath $executablePath)) { throw 'Server is not built. Run build_server.cmd first.' }
        $portProbe = [System.Net.Sockets.TcpClient]::new()
        try {
            $portInUse = $portProbe.ConnectAsync('localhost', 5080).Wait(1000) -and $portProbe.Connected
        } catch { $portInUse = $false }
        finally { $portProbe.Dispose() }
        if ($portInUse) { throw 'Port 5080 is already in use. Stop the other server before running run.cmd.' }

        Write-Host 'Starting server in the background. Logs: artifacts/server/stdout.log and stderr.log'
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        $serverProcess = Start-Process -FilePath $executablePath -ArgumentList @('--urls', $serverUrl) `
            -WorkingDirectory $webDirectory -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput (Join-Path $runtimeDirectory 'stdout.log') `
            -RedirectStandardError (Join-Path $runtimeDirectory 'stderr.log')
        try {
            @{
                ProcessId = $serverProcess.Id
                StartTime = $serverProcess.StartTime.ToUniversalTime().ToString('o')
            } | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding UTF8

            $deadline = [DateTime]::UtcNow.AddSeconds(30)
            $ready = $false
            while ([DateTime]::UtcNow -lt $deadline) {
                if ($serverProcess.HasExited) { throw 'Server exited. See artifacts/server/stdout.log and stderr.log.' }
                try {
                    $response = Invoke-WebRequest "$serverUrl/api/subjects" -UseBasicParsing -TimeoutSec 2
                    if ($response.StatusCode -eq 200) { $ready = $true; break }
                } catch { Start-Sleep -Milliseconds 300 }
            }
            if (!$ready) { throw 'Server did not become ready. See artifacts/server/stdout.log and stderr.log.' }
        } catch {
            if (!$serverProcess.HasExited) { $serverProcess.Kill(); $serverProcess.WaitForExit() }
            Remove-Item -LiteralPath $statePath -ErrorAction SilentlyContinue
            throw
        }
        $message = "Server started: $serverUrl`nUse stop.cmd to stop it."
    }
    Write-Host $message
} catch {
    $failed = $true
    $message = "Start failed: $($_.Exception.Message)"
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
