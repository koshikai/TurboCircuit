# Unity Test Framework をバッチ実行し、結果XMLを要約する。
# 使い方: pwsh -File .agents/skills/unity-compile-and-test/scripts/run-tests.ps1 [-Platform EditMode|PlayMode] [-Filter "Namespace.Class"] [-TimeoutSec 900]
# 終了コード: 0=全成功 / 1=失敗あり / 2=ロック / 3=異常終了・テスト0件 / 4=テスト基盤未導入
param(
    [ValidateSet("EditMode","PlayMode")][string]$Platform = "EditMode",
    [string]$Filter = "",
    [int]$TimeoutSec = 900
)

$ErrorActionPreference = "Stop"
$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path
Set-Location $ProjectRoot
$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe"
$LogDir = Join-Path $ProjectRoot "Logs"
$Log = Join-Path $LogDir "tests_$Platform.log"
$Xml = Join-Path $LogDir "tests_$Platform.xml"
if (-not (Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir | Out-Null }

# 前提チェック: test-framework パッケージとテスト用 asmdef
$manifest = Get-Content (Join-Path $ProjectRoot "Packages\manifest.json") -Raw
$asmdefs = @(Get-ChildItem (Join-Path $ProjectRoot "Assets") -Recurse -Filter *.asmdef -ErrorAction SilentlyContinue |
    Where-Object { (Get-Content $_.FullName -Raw) -match "UNITY_INCLUDE_TESTS|nunit" })
if ($manifest -notmatch "com.unity.test-framework" -or $asmdefs.Count -eq 0) {
    Write-Host "[Test] テスト基盤がありません（test-framework 未導入、またはテスト用 asmdef 無し）。" -ForegroundColor Yellow
    Write-Host "  最小手順は SKILL.md の「テストが無い場合」を参照してください。" -ForegroundColor Yellow
    exit 4
}

if (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue) {
    Write-Host "[Test] Unity エディタが起動中のためバッチ実行できません。エディタを閉じるか Window > General > Test Runner を使用してください。" -ForegroundColor Yellow
    exit 2
}
$Lock = Join-Path $ProjectRoot "Temp\UnityLockfile"
if (Test-Path $Lock) { Remove-Item $Lock -Force -ErrorAction SilentlyContinue }
foreach ($f in @($Log, $Xml)) { if (Test-Path $f) { Remove-Item $f -Force } }

# 注意: -runTests では -quit を付けない（テスト完了で自動終了）。PlayMode は -nographics を付けない。
$a = @("-batchmode","-projectPath","`"$ProjectRoot`"","-runTests","-testPlatform",$Platform,"-testResults","`"$Xml`"","-logFile","`"$Log`"")
if ($Platform -eq "EditMode") { $a += "-nographics" }
if ($Filter -ne "") { $a += @("-testFilter","`"$Filter`"") }
Write-Host "[Test] $Platform テスト実行中..." -ForegroundColor Cyan
$p = Start-Process -FilePath $UnityExe -ArgumentList $a -PassThru -WindowStyle Hidden
if (-not $p.WaitForExit($TimeoutSec * 1000)) {
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    Write-Host "[Test] タイムアウト" -ForegroundColor Red; exit 3
}

if (-not (Test-Path $Xml)) {
    Write-Host "[Test] 結果XMLなし (exit $($p.ExitCode))。コンパイルエラーの可能性:" -ForegroundColor Red
    if (Test-Path $Log) { Select-String -Path $Log -Pattern "error CS\d+" | ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique | ForEach-Object { Write-Host "  $_" -ForegroundColor Red } }
    exit 3
}

[xml]$doc = Get-Content $Xml
$run = $doc.'test-run'
Write-Host ("[Test] result={0} total={1} passed={2} failed={3} skipped={4} inconclusive={5} duration={6}s" -f $run.result, $run.total, $run.passed, $run.failed, $run.skipped, $run.inconclusive, $run.duration)
$failed = @($doc.SelectNodes("//test-case[@result='Failed']"))
foreach ($t in $failed) {
    Write-Host "  FAIL: $($t.fullname)" -ForegroundColor Red
    $msg = $t.failure.message.'#cdata-section'
    if (-not $msg) { $msg = $t.failure.message }
    if ($msg) { Write-Host "    $(($msg -split "`n")[0].Trim())" -ForegroundColor Red }
}
if ([int]$run.total -eq 0) { Write-Host "[Test] テストが 0 件です（Filter/asmdef を確認）。" -ForegroundColor Yellow; exit 3 }
if ($failed.Count -gt 0 -or $run.result -like "Failed*") { exit 1 }
Write-Host "[Test] OK" -ForegroundColor Green
exit 0
