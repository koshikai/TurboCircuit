param(
    [switch]$Build,
    [string]$ShotUi = "",
    [int]$Course = -1,
    [string]$Progress = "0.0,0.25,0.5,0.75",
    [float]$Yaw = 0.0,
    [switch]$NoHud,
    [string]$ShotModel = "",
    [switch]$AutoShot
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")
Set-Location $ProjectRoot

$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe"
$GameExe  = Join-Path $ProjectRoot "Build\Windows\TurboCircuit.exe"
$DataDir  = Join-Path $ProjectRoot "Build\Windows\TurboCircuit_Data"
$LogDir   = Join-Path $ProjectRoot "Logs"
$LockFile = Join-Path $ProjectRoot "Temp\UnityLockfile"

if (-not (Test-Path $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir | Out-Null
}

if ($Build -or -not (Test-Path $GameExe)) {
    $BuildLog = Join-Path $LogDir "build_verify.log"
    $MaxAttempts = 2
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        if ((Test-Path $LockFile) -and -not (Get-Process "Unity" -ErrorAction SilentlyContinue)) {
            Remove-Item $LockFile -Force -ErrorAction SilentlyContinue
        }
        Write-Host "[Verify] Running Unity headless batch build (Attempt $attempt/$MaxAttempts)..." -ForegroundColor Cyan
        cmd.exe /c """$UnityExe"" -batchmode -nographics -projectPath ""$ProjectRoot"" -executeMethod BuildTool.BuildWindows -logFile ""$BuildLog"""
        if ($LASTEXITCODE -eq 0) {
            break
        }
        if ((Test-Path $BuildLog) -and (Select-String -Path $BuildLog -Pattern "ILPPTrigger: Can't find file" -Quiet) -and ($attempt -lt $MaxAttempts)) {
            Write-Host "[Verify] Detected transient Unity ILPP named-pipe timeout. Cleaning up stale ILPP runner and retrying..." -ForegroundColor Yellow
            Get-Process "Unity.ILPP*" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
            continue
        }
        Write-Host "[Verify] Build failed with exit code $LASTEXITCODE. Checking errors:" -ForegroundColor Red
        if (Test-Path $BuildLog) {
            Get-Content $BuildLog | Select-String -Pattern "error CS|Build result|Exception|ILPPTrigger|Aborting" | ForEach-Object { Write-Host $_.Line -ForegroundColor Red }
        }
        exit $LASTEXITCODE
    }
    Get-Content $BuildLog | Select-String -Pattern "Build result|Successfully exported" | ForEach-Object { Write-Host $_.Line -ForegroundColor Green }
}

$GameArgs = @()
if ($ShotModel -ne "") {
    $GameArgs += @("-shot-model", $ShotModel)
}
elseif ($Course -ge 0) {
    $GameArgs += @("-shot-spot", "$Course", $Progress, "$Yaw")
    if ($NoHud) { $GameArgs += "nohud" }
}
elseif ($ShotUi -ne "") {
    $GameArgs += @("-shot-ui", $ShotUi)
}
elseif ($AutoShot) {
    $GameArgs += @("-autoshot")
}

if ($GameArgs.Count -gt 0) {
    Write-Host "[Verify] Launching TurboCircuit.exe $($GameArgs -join ' ') ..." -ForegroundColor Cyan
    Push-Location (Join-Path $ProjectRoot "Build\Windows")
    try {
        $p = Start-Process -FilePath $GameExe -ArgumentList $GameArgs -Wait -PassThru
        Write-Host "[Verify] Capture completed (ExitCode: $($p.ExitCode))." -ForegroundColor Green
    }
    finally {
        Pop-Location
    }

    Write-Host "[Verify] Generated verification files in Build/Windows/TurboCircuit_Data/:" -ForegroundColor Yellow
    Get-ChildItem -Path $DataDir -Filter "verify_*" | Sort-Object LastWriteTime -Descending | Select-Object -First 12 | ForEach-Object {
        Write-Host "  -> $($_.FullName)"
    }
}
