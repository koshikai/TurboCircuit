# Unity バッチモードでコンパイルのみ実行し、error CS 行を抽出して表示する。
# 使い方: pwsh -File .agents/skills/unity-compile-and-test/scripts/compile-check.ps1 [-TimeoutSec 600]
# 終了コード: 0=エラーなし / 1=コンパイルエラーあり / 2=エディタ起動中でロック / 3=Unity異常終了・タイムアウト
param(
    [int]$TimeoutSec = 600
)

$ErrorActionPreference = "Stop"
$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path
Set-Location $ProjectRoot

$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe"
$LogDir   = Join-Path $ProjectRoot "Logs"
$Log      = Join-Path $LogDir "compile_check.log"
$Lock     = Join-Path $ProjectRoot "Temp\UnityLockfile"

if (-not (Test-Path $UnityExe)) { Write-Host "[Compile] Unity.exe が見つかりません: $UnityExe" -ForegroundColor Red; exit 3 }
if (-not (Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir | Out-Null }

# 既に Unity エディタが起動していないか検出（同一プロジェクトは二重に開けない）
$running = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "[Compile] Unity エディタが起動中です (PID: $($running.ProcessId -join ', '))。" -ForegroundColor Yellow
    Write-Host "  同一プロジェクトは二重に開けないため、バッチ実行できません。次のいずれかを行ってください:" -ForegroundColor Yellow
    Write-Host "   - エディタを閉じてから再実行する" -ForegroundColor Yellow
    Write-Host "   - エディタの Console でエラーを確認する（スクリプト保存で自動再コンパイルされる）" -ForegroundColor Yellow
    Write-Host "   - エディタログを確認する: Select-String -Path `"`$env:LOCALAPPDATA\Unity\Editor\Editor.log`" -Pattern 'error CS'" -ForegroundColor Yellow
    exit 2
}
if (Test-Path $Lock) {
    Write-Host "[Compile] Unity プロセスは無いが古い Temp\UnityLockfile が残っていたため削除します。" -ForegroundColor DarkYellow
    Remove-Item $Lock -Force -ErrorAction SilentlyContinue
}

if (Test-Path $Log) { Remove-Item $Log -Force }
Write-Host "[Compile] Unity バッチコンパイル開始 (timeout ${TimeoutSec}s)..." -ForegroundColor Cyan
$sw = [Diagnostics.Stopwatch]::StartNew()
$unityArgs = @("-batchmode","-nographics","-quit","-projectPath","`"$ProjectRoot`"","-logFile","`"$Log`"")
$p = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -PassThru -WindowStyle Hidden
if (-not $p.WaitForExit($TimeoutSec * 1000)) {
    Write-Host "[Compile] タイムアウト。Unity を終了します。" -ForegroundColor Red
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    exit 3
}
$code = $p.ExitCode
Write-Host ("[Compile] 終了コード {0} / {1:N0}s" -f $code, $sw.Elapsed.TotalSeconds)

if (-not (Test-Path $Log)) { Write-Host "[Compile] ログが生成されませんでした。" -ForegroundColor Red; exit 3 }

if (Select-String -Path $Log -Pattern "another Unity instance is running|Multiple Unity instances" -Quiet) {
    Write-Host "[Compile] 別の Unity インスタンスがこのプロジェクトを開いています。" -ForegroundColor Yellow
    exit 2
}

# 重複排除して error CS 行のみ表示
$errs = @(Select-String -Path $Log -Pattern "error CS\d+" | ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique)
if ($errs.Count -gt 0) {
    Write-Host "[Compile] コンパイルエラー $($errs.Count) 件:" -ForegroundColor Red
    $errs | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}
if ($code -ne 0) {
    Write-Host "[Compile] error CS は無いが Unity が異常終了 ($code)。ログ末尾:" -ForegroundColor Red
    Get-Content $Log -Tail 30
    exit 3
}
Write-Host "[Compile] OK: コンパイルエラーなし" -ForegroundColor Green
exit 0
