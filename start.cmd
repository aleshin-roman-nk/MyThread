@echo off
setlocal
set "JCHRONO_ROOT=%~dp0"
set "JCHRONO_SCRIPT=%~f0"
set "JCHRONO_NO_PAUSE=0"
if /i "%~1"=="--no-pause" set "JCHRONO_NO_PAUSE=1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& ([scriptblock]::Create(((Get-Content -LiteralPath $env:JCHRONO_SCRIPT -Raw -Encoding UTF8) -split '(?m)^# POWERSHELL BEGIN\r?$',2)[1]))"
set "JCHRONO_EXIT_CODE=%ERRORLEVEL%"
if "%JCHRONO_NO_PAUSE%"=="0" pause
exit /b %JCHRONO_EXIT_CODE%
# POWERSHELL BEGIN
$ErrorActionPreference = 'Stop'
$projectRoot = $env:JCHRONO_ROOT

function Test-DotNet {
    $dotnet = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if (!$dotnet) { return $false }

    $sdks = & $dotnet.Source --list-sdks
    if ($LASTEXITCODE -ne 0) { return $false }
    $supportedSdks = $sdks | Where-Object { $_ -match '^(\d+)\.' -and [int]$Matches[1] -ge 10 }
    if (!$supportedSdks) { return $false }

    $runtimes = & $dotnet.Source --list-runtimes
    return ($LASTEXITCODE -eq 0 -and
        ($runtimes -match '^Microsoft\.NETCore\.App 10\.') -and
        ($runtimes -match '^Microsoft\.AspNetCore\.App 10\.'))
}

function Test-Node {
    $node = Get-Command node.exe -ErrorAction SilentlyContinue
    if (!$node -or !(Get-Command npm.cmd -ErrorAction SilentlyContinue)) { return $false }

    $version = & $node.Source --version
    if ($LASTEXITCODE -ne 0 -or $version -notmatch '^v(\d+\.\d+\.\d+)$') { return $false }

    $nodeVersion = [version]$Matches[1]
    return (($nodeVersion.Major -eq 20 -and $nodeVersion.Minor -ge 19) -or
        ($nodeVersion.Major -eq 22 -and $nodeVersion.Minor -ge 12) -or
        $nodeVersion.Major -eq 24)
}

function Invoke-ProjectScript([string]$Name) {
    & (Join-Path $projectRoot $Name) --no-pause
    if ($LASTEXITCODE -ne 0) { throw "Ошибка в $Name. См. сообщение выше и журналы в artifacts/server." }
}

try {
    Set-Location -LiteralPath $projectRoot
    $executable = Join-Path $projectRoot 'src/JChrono.Web/bin/Debug/net10.0/JChrono.Web.exe'
    $runningServer = Get-Process -Name JChrono.Web -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $executable }

    if (!$runningServer) {
        $needsDotNet = !(Test-DotNet)
        $needsNode = !(Test-Node)
        $packages = @(
            if ($needsDotNet) { 'Microsoft.DotNet.SDK.10' }
            if ($needsNode) { 'OpenJS.NodeJS.22' }
        )

        if ($packages.Count) {
            Write-Host "Требуется установить: $($packages -join ', ')."
            $winget = Get-Command winget.exe -ErrorAction SilentlyContinue
            if (!$winget -or $env:JCHRONO_NO_PAUSE -eq '1') {
                throw 'Запустите start.cmd интерактивно с WinGet или установите инструменты по инструкции в README.md.'
            }
            if ((Read-Host 'Установить через WinGet? [y/N]') -ne 'y') {
                throw 'Установка отменена. Инструменты можно установить вручную по инструкции в README.md.'
            }

            foreach ($package in $packages) {
                & $winget.Source install --id $package --exact --source winget
                if ($LASTEXITCODE -ne 0) {
                    throw "WinGet не смог установить $package (код $LASTEXITCODE). См. README.md для ручной установки."
                }
            }

            # This process and its child builds still have the original PATH.
            $programFiles = $env:ProgramW6432
            if (!$programFiles) { $programFiles = $env:ProgramFiles }
            $pathEntries = @(
                if ($needsDotNet) { Join-Path $programFiles 'dotnet' }
                if ($needsNode) { Join-Path $programFiles 'nodejs' }
                $env:Path
                [Environment]::GetEnvironmentVariable('Path', 'Machine')
                [Environment]::GetEnvironmentVariable('Path', 'User')
            )
            $env:Path = $pathEntries -join ';'

            if (!(Test-DotNet) -or !(Test-Node)) {
                throw 'Инструменты пока недоступны. Откройте новый терминал и запустите start.cmd снова. См. README.md.'
            }
        }

        Invoke-ProjectScript 'build_server.cmd'
        Invoke-ProjectScript 'build_client.cmd'
    }

    Invoke-ProjectScript 'run.cmd'
    Write-Host 'Откройте http://localhost:5080' -ForegroundColor Green
    exit 0
} catch {
    Write-Host "Не удалось запустить JChrono. $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
