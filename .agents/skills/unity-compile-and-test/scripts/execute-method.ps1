# Editor の static メソッドをバッチ実行する汎用ラッパー。
# 使い方: pwsh -File .agents/skills/unity-compile-and-test/scripts/execute-method.ps1 -Method "BuildTool.Setup" [-TimeoutSec 900]
param(
    [Parameter(Mandatory)][string]$Method,
    [int]$TimeoutSec = 900
)
$ErrorActionPreference = "Stop"
$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path
Set-Location $ProjectRoot
$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe"
$Log = Join-Path $ProjectRoot "Logs\execute_method.log"
if (-not (Test-Path (Split-Path $Log))) { New-Item -ItemType Directory -Path (Split-Path $Log) | Out-Null }
if (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue) {
    Write-Host "[Exec] Unity エディタが起動中のため実行できません。エディタを閉じてください。" -ForegroundColor Yellow; exit 2
}
$Lock = Join-Path $ProjectRoot "Temp\UnityLockfile"
if (Test-Path $Lock) { Remove-Item $Lock -Force -ErrorAction SilentlyContinue }
$a = @("-batchmode","-nographics","-quit","-projectPath","`"$ProjectRoot`"","-executeMethod",$Method,"-logFile","`"$Log`"")
$p = Start-Process -FilePath $UnityExe -ArgumentList $a -PassThru -WindowStyle Hidden
if (-not $p.WaitForExit($TimeoutSec * 1000)) { Stop-Process -Id $p.Id -Force; Write-Host "[Exec] タイムアウト" -ForegroundColor Red; exit 3 }
Select-String -Path $Log -Pattern "error CS\d+|Exception|executeMethod method .* not found|\[BuildTool\]" | ForEach-Object { $_.Line.Trim() } | Select-Object -Unique | ForEach-Object { Write-Host "  $_" }
Write-Host "[Exec] exit code $($p.ExitCode) (log: Logs/execute_method.log)"
exit $p.ExitCode
