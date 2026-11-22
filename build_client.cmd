@echo off
setlocal
set "JCHRONO_ROOT=%~dp0"
set "JCHRONO_SCRIPT=%~f0"
set "JCHRONO_NO_PAUSE=0"
set "JCHRONO_REINSTALL=0"
for %%A in (%*) do (
    if /i "%%~A"=="--no-pause" set "JCHRONO_NO_PAUSE=1"
    if /i "%%~A"=="--reinstall" set "JCHRONO_REINSTALL=1"
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& ([scriptblock]::Create(((Get-Content -LiteralPath $env:JCHRONO_SCRIPT -Raw -Encoding UTF8) -split '(?m)^# POWERSHELL BEGIN\r?$',2)[1]))"
set "JCHRONO_EXIT_CODE=%ERRORLEVEL%"
if "%JCHRONO_NO_PAUSE%"=="0" pause
exit /b %JCHRONO_EXIT_CODE%
# POWERSHELL BEGIN
$ErrorActionPreference = 'Stop'
$projectRoot = $env:JCHRONO_ROOT
$runtimeDirectory = Join-Path $projectRoot 'artifacts/server'
$controlLock = $null
$transcribing = $false
$failed = $false
function Invoke-Checked([scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE. See artifacts/server/build_client.log." }
}
function Get-DependencyHash([string]$Path) {
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $resolvedPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
        $bytes = [System.IO.File]::ReadAllBytes($resolvedPath)
        return [System.BitConverter]::ToString($algorithm.ComputeHash($bytes)).Replace('-', '')
    } finally { $algorithm.Dispose() }
}
try {
    New-Item -ItemType Directory -Path $runtimeDirectory -Force | Out-Null
    $controlLock = [System.IO.File]::Open((Join-Path $runtimeDirectory 'control.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    Start-Transcript -Path (Join-Path $runtimeDirectory 'build_client.log') -Force | Out-Null
    $transcribing = $true
    $env:npm_config_cache = Join-Path $projectRoot '.npm-cache'
    Push-Location (Join-Path $projectRoot 'client')
    try {
        $nodeVersion = & node --version
        if ($LASTEXITCODE -ne 0) { throw 'Cannot determine Node.js version.' }
        $npmVersion = & npm.cmd --version
        if ($LASTEXITCODE -ne 0) { throw 'Cannot determine npm version.' }
        $inputs = @(
            'jchrono-install-v1'
            (Get-DependencyHash 'package.json')
            (Get-DependencyHash 'package-lock.json')
            $nodeVersion
            $npmVersion
        )
        if (Test-Path -LiteralPath '.npmrc') {
            $inputs += Get-DependencyHash '.npmrc'
        }
        $inputSignature = $inputs -join '|'
        $stampPath = 'node_modules/.jchrono-install'
        $installedLockPath = 'node_modules/.package-lock.json'
        $dependenciesReady = $false
        if ((Test-Path -LiteralPath $stampPath) -and (Test-Path -LiteralPath $installedLockPath) -and
            (Test-Path -LiteralPath 'node_modules/.bin/ng.cmd')) {
            $currentSignature = $inputSignature + '|' + (Get-DependencyHash $installedLockPath)
            $dependenciesReady = (Get-Content -LiteralPath $stampPath -Raw).Trim() -eq $currentSignature
        }
        if (!$dependenciesReady -or $env:JCHRONO_REINSTALL -eq '1') {
            Write-Host 'Installing dependencies: first build, changed inputs, or --reinstall.'
            # Do not reuse a previous success marker if installation fails.
            if (Test-Path -LiteralPath $stampPath) { Remove-Item -LiteralPath $stampPath }
            Invoke-Checked { npm.cmd ci --include=dev --no-audit --no-fund }
            $installedSignature = $inputSignature + '|' + (Get-DependencyHash $installedLockPath)
            Set-Content -LiteralPath $stampPath -Value $installedSignature -Encoding UTF8
        } else {
            Write-Host 'Dependencies are unchanged. Skipping npm ci.'
        }
        Invoke-Checked { npm.cmd run build }
    } finally { Pop-Location }
    $message = 'Client build completed in wwwroot. Refresh the browser. Server was not restarted.'
    Write-Host $message
} catch {
    $failed = $true
    $message = "Client build failed: $($_.Exception.Message)"
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
