using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// アプリ内アップデート（バージョン確認・ダウンロード・自己更新適用）を管理するクラス
/// </summary>
public class AppUpdater : MonoBehaviour
{
    public static AppUpdater Instance { get; private set; }

    // 現在のアプリバージョン
    public const string CurrentVersion = "v1.9.1";

    // GitHub リポジトリ情報
    public const string GitHubRepo = "koshikai/TurboCircuit";
    public const string GitHubApiUrl = "https://api.github.com/repos/koshikai/TurboCircuit/releases/latest";
    public const string VersionJsonUrl = "https://raw.githubusercontent.com/koshikai/TurboCircuit/main/version.json";
    public const string ReleasesPageUrl = "https://github.com/koshikai/TurboCircuit/releases";

    public enum UpdateState
    {
        Idle,
        Checking,
        UpToDate,
        UpdateAvailable,
        Downloading,
        Downloaded,
        Applying,
        Error
    }

    [Header("Status")]
    public UpdateState State = UpdateState.Idle;
    public string LatestVersion = "";
    public string ReleaseName = "";
    public string ReleaseNotes = "";
    public string DownloadUrl = "";
    public float DownloadProgress = 0f;
    public ulong DownloadedBytes = 0;
    public ulong TotalBytes = 0;
    public string StatusMessage = "Ready to check for updates.";
    public string ErrorMessage = "";

    public bool ShowModal = false;
    public bool HasCheckedOnce = false;
    public DateTime LastCheckedTime = DateTime.MinValue;

    UnityWebRequest currentDownloadReq;
    Coroutine checkCoroutine;
    Coroutine downloadCoroutine;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 前回のアップデート適用後に残った一時フォルダがあれば起動時に掃除
        CleanupTempUpdateDir();

        // 起動時にバックグラウンドで最新バージョンをチェック（モーダルは開かず静かに通知）
        StartCoroutine(AutoCheckOnStartupRoutine());
    }

    static void CleanupTempUpdateDir()
    {
        try
        {
            string updateDir = Path.Combine(Path.GetTempPath(), "TurboCircuit_Update");
            if (Directory.Exists(updateDir))
            {
                Directory.Delete(updateDir, true);
            }
        }
        catch
        {
            // ロック中などの場合は無視
        }
    }

    IEnumerator AutoCheckOnStartupRoutine()
    {
        // 起動直後のフレーム落ちやロード競合を避けるため少し待機
        yield return new WaitForSeconds(2.5f);
        CheckForUpdates(openModalIfNew: false);
    }

    /// <summary>
    /// アップデートを確認する
    /// </summary>
    public void CheckForUpdates(bool openModalIfNew = false)
    {
        if (State == UpdateState.Checking || State == UpdateState.Downloading || State == UpdateState.Applying)
            return;

        if (checkCoroutine != null)
            StopCoroutine(checkCoroutine);

        checkCoroutine = StartCoroutine(CheckForUpdatesRoutine(openModalIfNew));
    }

    IEnumerator CheckForUpdatesRoutine(bool openModalIfNew)
    {
        State = UpdateState.Checking;
        StatusMessage = "Checking for updates...";
        ErrorMessage = "";

        string json = null;

        // 1. まず GitHub Releases API から取得を試みる
        using (var req = UnityWebRequest.Get(GitHubApiUrl))
        {
            req.SetRequestHeader("User-Agent", "TurboCircuit-GameApp");
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                json = req.downloadHandler.text;
            }
        }

        // 2. GitHub API がレートリミット等の理由で失敗した場合、version.json へフォールバック
        if (string.IsNullOrEmpty(json))
        {
            using (var req = UnityWebRequest.Get(VersionJsonUrl))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    json = req.downloadHandler.text;
                }
            }
        }

        if (string.IsNullOrEmpty(json))
        {
            State = UpdateState.Error;
            ErrorMessage = "Could not check for updates. Check internet connection.";
            StatusMessage = "Update check failed.";
            yield break;
        }

        HasCheckedOnce = true;
        LastCheckedTime = DateTime.Now;

        // JSON をパースしてバージョン情報・アセットURLを取得
        ParseReleaseJson(json);

        if (string.IsNullOrEmpty(LatestVersion))
        {
            State = UpdateState.Error;
            ErrorMessage = "Failed to parse update information.";
            yield break;
        }

        bool isNewer = CompareVersions(LatestVersion, CurrentVersion) > 0;
        if (isNewer)
        {
            State = UpdateState.UpdateAvailable;
            StatusMessage = $"New update available: {LatestVersion}";
            if (openModalIfNew)
            {
                ShowModal = true;
            }
        }
        else
        {
            State = UpdateState.UpToDate;
            StatusMessage = $"Turbo Circuit is up to date ({CurrentVersion}).";
        }
    }

    void ParseReleaseJson(string json)
    {
        try
        {
            // tag_name (または "version")
            var tagMatch = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            if (!tagMatch.Success)
                tagMatch = Regex.Match(json, "\"version\"\\s*:\\s*\"([^\"]+)\"");
            LatestVersion = tagMatch.Success ? tagMatch.Groups[1].Value : "";

            // name (アセットの .zip 名に誤マッチした場合はバージョン名にフォールバック)
            var nameMatch = Regex.Match(json, "\"name\"\\s*:\\s*\"([^\"]+)\"");
            string rawName = nameMatch.Success ? nameMatch.Groups[1].Value : "";
            if (string.IsNullOrEmpty(rawName) || rawName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                ReleaseName = $"Turbo Circuit {LatestVersion}";
            else
                ReleaseName = CleanSingleLine(Regex.Unescape(rawName));

            // body / notes
            var bodyMatch = Regex.Match(json, "\"body\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"");
            if (!bodyMatch.Success)
                bodyMatch = Regex.Match(json, "\"notes\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"");

            if (bodyMatch.Success)
            {
                ReleaseNotes = CleanReleaseNotes(Regex.Unescape(bodyMatch.Groups[1].Value));
            }
            else
            {
                ReleaseNotes = "New update available!";
            }

            // download_url (assets 内の .zip または "download_url")
            var directDownloadMatch = Regex.Match(json, "\"download_url\"\\s*:\\s*\"([^\"]+)\"");
            if (directDownloadMatch.Success)
            {
                DownloadUrl = directDownloadMatch.Groups[1].Value;
            }
            else
            {
                // browser_download_url から .zip を探す
                var zipMatches = Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+\\.zip)\"");
                if (zipMatches.Count > 0)
                {
                    // "TurboCircuit" を含むものを優先、なければ最初のzip
                    string bestUrl = zipMatches[0].Groups[1].Value;
                    foreach (Match m in zipMatches)
                    {
                        if (m.Groups[1].Value.IndexOf("TurboCircuit", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            bestUrl = m.Groups[1].Value;
                            break;
                        }
                    }
                    DownloadUrl = bestUrl;
                }
            }

            // フォールバック URL
            if (string.IsNullOrEmpty(DownloadUrl) && !string.IsNullOrEmpty(LatestVersion))
            {
                DownloadUrl = $"https://github.com/{GitHubRepo}/releases/download/{LatestVersion}/TurboCircuit-win64.zip";
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AppUpdater] Parse error: " + ex.Message);
        }
    }

    /// <summary>
    /// バージョン比較 (v1.7.0 vs v1.6.0)
    /// </summary>
    public static int CompareVersions(string vA, string vB)
    {
        if (string.IsNullOrEmpty(vA)) return -1;
        if (string.IsNullOrEmpty(vB)) return 1;

        string cleanA = vA.TrimStart('v', 'V');
        string cleanB = vB.TrimStart('v', 'V');

        string[] partsA = cleanA.Split(new[] { '.', '-', '_' });
        string[] partsB = cleanB.Split(new[] { '.', '-', '_' });

        int maxLen = Mathf.Max(partsA.Length, partsB.Length);
        for (int i = 0; i < maxLen; i++)
        {
            int numA = 0, numB = 0;
            if (i < partsA.Length) int.TryParse(partsA[i], out numA);
            if (i < partsB.Length) int.TryParse(partsB[i], out numB);

            if (numA != numB)
                return numA.CompareTo(numB);
        }

        return 0;
    }

    /// <summary>
    /// アップデートのダウンロードを開始
    /// </summary>
    public void StartDownload()
    {
        if (State == UpdateState.Downloading || State == UpdateState.Applying)
            return;

        if (string.IsNullOrEmpty(DownloadUrl))
        {
            ErrorMessage = "Download URL not found.";
            State = UpdateState.Error;
            return;
        }

        if (downloadCoroutine != null)
            StopCoroutine(downloadCoroutine);

        downloadCoroutine = StartCoroutine(DownloadRoutine());
    }

    /// <summary>
    /// ダウンロードをキャンセル
    /// </summary>
    public void CancelDownload()
    {
        if (currentDownloadReq != null && !currentDownloadReq.isDone)
        {
            currentDownloadReq.Abort();
        }

        if (downloadCoroutine != null)
        {
            StopCoroutine(downloadCoroutine);
            downloadCoroutine = null;
        }

        State = UpdateState.UpdateAvailable;
        StatusMessage = "Download canceled.";
    }

    IEnumerator DownloadRoutine()
    {
        State = UpdateState.Downloading;
        DownloadProgress = 0f;
        DownloadedBytes = 0;
        TotalBytes = 0;
        StatusMessage = "Starting download...";
        ErrorMessage = "";

        string updateDir = Path.Combine(Path.GetTempPath(), "TurboCircuit_Update");
        try
        {
            if (Directory.Exists(updateDir))
                Directory.Delete(updateDir, true);
            Directory.CreateDirectory(updateDir);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AppUpdater] Could not prepare update dir: " + ex.Message);
        }

        string saveZipPath = Path.Combine(updateDir, "TurboCircuit_Update.zip");

        using (currentDownloadReq = new UnityWebRequest(DownloadUrl, UnityWebRequest.kHttpVerbGET))
        {
            currentDownloadReq.downloadHandler = new DownloadHandlerFile(saveZipPath);
            currentDownloadReq.SetRequestHeader("User-Agent", "TurboCircuit-GameApp");
            currentDownloadReq.SendWebRequest();

            while (!currentDownloadReq.isDone)
            {
                DownloadProgress = currentDownloadReq.downloadProgress;
                DownloadedBytes = currentDownloadReq.downloadedBytes;
                StatusMessage = $"Downloading: {DownloadedBytes / (1024f * 1024f):F1} MB ({Mathf.RoundToInt(DownloadProgress * 100f)}%)";
                yield return null;
            }

            if (currentDownloadReq.result != UnityWebRequest.Result.Success)
            {
                State = UpdateState.Error;
                ErrorMessage = "Download failed: " + currentDownloadReq.error;
                StatusMessage = "Download failed.";
                yield break;
            }
        }

        DownloadProgress = 1f;
        State = UpdateState.Downloaded;
        StatusMessage = "Download complete! Ready to install.";
    }

    /// <summary>
    /// アップデートを適用してゲームを再起動
    /// </summary>
    public void ApplyUpdateAndRestart()
    {
        if (State != UpdateState.Downloaded) return;

        State = UpdateState.Applying;
        StatusMessage = "Applying update and restarting...";

        string updateDir = Path.Combine(Path.GetTempPath(), "TurboCircuit_Update");
        string zipPath = Path.Combine(updateDir, "TurboCircuit_Update.zip");

        if (!File.Exists(zipPath))
        {
            State = UpdateState.Error;
            ErrorMessage = "Downloaded update package not found.";
            return;
        }

        // Unity Editor の場合はプロジェクト破損を防ぐため安全にスキップ
        if (Application.isEditor)
        {
            Debug.Log("[AppUpdater] Running inside Unity Editor: simulated self-update successful!");
            StatusMessage = "Editor mode: Update downloaded successfully. (Self-update only runs in standalone build).";
            Application.OpenURL(ReleasesPageUrl);
            State = UpdateState.UpToDate;
            return;
        }

        // スタンドアロン実行時：ターゲットディレクトリ（ゲームのルートフォルダ）
        // Application.dataPath は "Build/Windows/TurboCircuit_Data" なので、その親が実行フォルダ
        string targetDir = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string exeName = "TurboCircuit.exe";
        string exeFullPath = Path.Combine(targetDir, exeName);
        string extractDir = Path.Combine(updateDir, "extracted");
        string ps1Path = Path.Combine(updateDir, "apply_update.ps1");
        string batPath = Path.Combine(updateDir, "apply_update.bat");
        int currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;

        string EscPs(string s) => s.Replace("'", "''");

        // PowerShell スクリプト（UTF-8 with BOM で保存することで日本語パス・空白・特殊文字に完全対応）
        string ps1Content = $@"$ErrorActionPreference = 'Stop'
$pidToWait = {currentPid}
$zipPath = '{EscPs(zipPath)}'
$extractDir = '{EscPs(extractDir)}'
$targetDir = '{EscPs(targetDir)}'
$exePath = '{EscPs(exeFullPath)}'

Write-Host '====================================================================' -ForegroundColor Cyan
Write-Host '                  TURBO CIRCUIT AUTO UPDATER                        ' -ForegroundColor Cyan
Write-Host '====================================================================' -ForegroundColor Cyan
Write-Host ''
Write-Host '[1/4] Waiting for game process to safely exit...'
try {{
    Wait-Process -Id $pidToWait -Timeout 15 -ErrorAction SilentlyContinue
}} catch {{}}
Start-Sleep -Seconds 1

Write-Host '[2/4] Extracting update package...'
if (Test-Path -LiteralPath $extractDir) {{
    Remove-Item -LiteralPath $extractDir -Recurse -Force -ErrorAction SilentlyContinue
}}
Expand-Archive -LiteralPath $zipPath -DestinationPath $extractDir -Force

$src = $extractDir
$items = @(Get-ChildItem -LiteralPath $src)
if ($items.Count -eq 1 -and $items[0].PSIsContainer) {{
    $src = $items[0].FullName
}}

Write-Host '[3/4] Applying update files to install directory...'
Get-ChildItem -LiteralPath $src | ForEach-Object {{
    Copy-Item -LiteralPath $_.FullName -Destination $targetDir -Recurse -Force
}}

Write-Host '[4/4] Cleaning up temporary files & relaunching Turbo Circuit...'
Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $extractDir -Recurse -Force -ErrorAction SilentlyContinue

Start-Sleep -Milliseconds 600
Start-Process -FilePath $exePath -WorkingDirectory $targetDir
";

        // 起動用バッチファイル（%~dp0 で自身のフォルダにある apply_update.ps1 を呼び出す）
        string batContent = @"@echo off
chcp 65001 >nul
title Turbo Circuit - Auto Updater
powershell -NoProfile -ExecutionPolicy Bypass -File ""%~dp0apply_update.ps1""
exit
";

        // .ps1 は UTF-8 (BOMあり)、.bat は UTF-8 (BOMなし) で書き出し
        File.WriteAllText(ps1Path, ps1Content, new System.Text.UTF8Encoding(true));
        File.WriteAllText(batPath, batContent, new System.Text.UTF8Encoding(false));

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{batPath}\"\"",
                WorkingDirectory = updateDir,
                CreateNoWindow = false,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);

            // ゲーム本体を終了
            Application.Quit();
        }
        catch (Exception ex)
        {
            State = UpdateState.Error;
            ErrorMessage = "Failed to launch updater: " + ex.Message;
            Debug.LogError("[AppUpdater] Launch updater error: " + ex.Message);
        }
    }

    /// <summary>
    /// IMGUI で文字化けするサロゲートペア絵文字・CR(\r)・Markdown記法を整形して読みやすいプレーンテキストにする
    /// </summary>
    public static string CleanReleaseNotes(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";

        // 1. 改行コード統一（\r が残ると Unity IMGUI で行末に □ が描画される）
        string s = raw.Replace("\r\n", "\n").Replace("\r", "\n");

        // 2. UTF-16 サロゲートペア（カラー絵文字等）および異体字セレクタ（\uFE00-\uFE0F）を除去
        s = Regex.Replace(s, @"[\uD800-\uDFFF\uFE00-\uFE0F]", "");

        // 3. Markdown 見出し・装飾・箇条書きを読みやすい記号に変換
        s = Regex.Replace(s, @"^#{1,4}\s*(.+)$", "■ $1", RegexOptions.Multiline);
        s = Regex.Replace(s, @"\*\*([^*]+)\*\*", "$1");
        s = Regex.Replace(s, @"`([^`]+)`", "$1");
        s = Regex.Replace(s, @"^[ \t]{2,}[\*\-]\s+", "    - ", RegexOptions.Multiline);
        s = Regex.Replace(s, @"^[\*\-]\s+", "  • ", RegexOptions.Multiline);

        // 4. 3行以上の連続空行を圧縮
        s = Regex.Replace(s, @"\n{3,}", "\n\n");

        return s.Trim();
    }

    public static string CleanSingleLine(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        return Regex.Replace(raw, @"[\r\n\uD800-\uDFFF\uFE00-\uFE0F]", "").Trim();
    }

    /// <summary>
    /// テスト・シミュレーション用（デバッグ用）
    /// </summary>
    public void SimulateUpdateAvailableForTesting()
    {
        LatestVersion = "v2.0.0-preview";
        ReleaseName = "Turbo Circuit v2.0.0 (Preview)";
        ReleaseNotes = CleanReleaseNotes(
            "## What's New in Turbo Circuit\n" +
            "- 10 Drivers x 8 Custom Kart Machines (80 Combinations!)\n" +
            "  * New Animal Racers: PANDA, PENGUIN, FOX, BUNNY, LION\n" +
            "  * 8 Custom Machines: STANDARD, PIPE BUGGY, MACH FORMULA, MONSTER, CYBER GT, ROADSTER, MUSCLE V8, JET POD\n" +
            "- 3D Item Visual Overhaul (Item Box, Banana, Homing Missile, 3D Shield)\n" +
            "- UI Modal Click & Japanese Release Notes Rendering Fix\n\n" +
            "### 主なアップデート内容\n" +
            "- ドライバー10名 × カートマシン8種の自由なセッティングに対応\n" +
            "- アイテム3Dモデル・ポップアイコン・白煙エフェクトを全面刷新\n" +
            "- アップデート画面の日本語表示およびボタンクリック判定を改善");
        DownloadUrl = "https://github.com/koshikai/TurboCircuit/releases/download/v1.9.1/TurboCircuit-win64.zip";
        State = UpdateState.UpdateAvailable;
        StatusMessage = "New update available: v2.0.0-preview";
        ShowModal = true;
    }
}
