param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path)

$ErrorActionPreference = 'Stop'
$game = Join-Path $ProjectRoot 'Build/Windows/TurboCircuit.exe'
$logDir = Join-Path $ProjectRoot 'Logs'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$hostLog = Join-Path $logDir 'verify_online_host.log'
$clientLog = Join-Path $logDir 'verify_online_client.log'
$processes = @()
try {
    $processes += Start-Process -FilePath $game -WorkingDirectory (Split-Path $game) -WindowStyle Hidden -ArgumentList @('-nethost', '-netmenu', '-verify-online-start', '-logFile', "`"$hostLog`"") -PassThru
    $processes += Start-Process -FilePath $game -WorkingDirectory (Split-Path $game) -WindowStyle Hidden -ArgumentList @('-netjoin', '127.0.0.1', '-netmenu', '-verify-online-start', '-logFile', "`"$clientLog`"") -PassThru
    $deadline = (Get-Date).AddSeconds(120)
    while (($processes | Where-Object { -not $_.HasExited }) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
    }
    foreach ($process in $processes) {
        if (-not $process.HasExited) { throw 'Online verification timed out.' }
        if ($process.ExitCode -ne 0) { throw "Online verification failed: exit code $($process.ExitCode). See $logDir" }
    }
    foreach ($log in @($hostLog, $clientLog)) {
        $result = Select-String -LiteralPath $log -Pattern '\[VerifyOnline\] PASS:'
        if (-not $result) { throw "PASS result missing from $log" }
        $result.Line
    }
}
finally {
    foreach ($process in $processes) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    }
}
