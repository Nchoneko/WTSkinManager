# 从 CHANGELOG.md 抽取指定版本的说明，生成 out/release-notes.md 供 GitHub Release 使用。
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File build/release-notes.ps1 -Tag v3.9.1
#
# 未指定 Tag 时读取环境变量 GITHUB_REF_NAME；找不到对应版本时输出兜底说明。

param(
    [string]$Tag = ''
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($Tag)) { $Tag = $env:GITHUB_REF_NAME }
if ([string]::IsNullOrWhiteSpace($Tag)) { throw '缺少 -Tag（例如 v3.9.1）' }

$version = $Tag.TrimStart('v', 'V')
$changelogPath = Join-Path $Root 'CHANGELOG.md'
if (-not (Test-Path $changelogPath)) { throw "缺少 $changelogPath" }

$text = Get-Content -Raw -Encoding UTF8 $changelogPath
$pattern = '(?ms)^##\s+([0-9]+\.[0-9]+\.[0-9]+)[^\r\n]*\r?\n(.*?)(?=^##\s|\z)'
$found = [regex]::Matches($text, $pattern)
$body = $null
$matchedVersion = $null
foreach ($m in $found) {
    $v = $m.Groups[1].Value
    if ($v -eq $version -or $version.StartsWith($v + '.')) {
        $body = $m.Groups[2].Value.Trim()
        $matchedVersion = $v
        break
    }
}

$outDir = Join-Path $Root 'out'
New-Item -ItemType Directory -Force $outDir | Out-Null
$outFile = Join-Path $outDir 'release-notes.md'

if ($null -eq $body) {
    $fallback = @(
        "## WTSkinManager $Tag",
        '',
        '本版本未在 CHANGELOG 中找到对应段落，完整变更请查看仓库根目录的 CHANGELOG.md。'
    ) -join "`n"
    [IO.File]::WriteAllText($outFile, $fallback, (New-Object Text.UTF8Encoding($false)))
    Write-Host "RELEASE NOTES 兜底输出：未找到版本 $version"
    exit 0
}

$header = @(
    "## WTSkinManager $Tag",
    '',
    "> 版本 $matchedVersion 的变更，数据文件与安装包见下方附件。",
    ''
) -join "`n"
$footer = @(
    '',
    '## 下载与安装',
    '',
    '- `WTSkinManager.exe`：单文件程序，Windows 10/11，.NET Framework 4.x，无需安装运行时。',
    '- `WTSkinManager.exe.sha256`：下载后核对，命令 `certutil -hashfile WTSkinManager.exe SHA256`。',
    '- `updates.dat`：更新日志数据，供程序内「历史版本」使用。',
    '',
    '运行 exe，按向导选择安装目录与 War Thunder 游戏目录即可；升级会保留设置与用户数据。',
    '',
    '## 反馈',
    '',
    '- 问题反馈：https://github.com/Nchoneko/WTSkinManager/issues',
    '- 使用说明：https://github.com/Nchoneko/WTSkinManager#readme',
    ''
) -join "`n"
[IO.File]::WriteAllText($outFile, $header + "`n" + $body + "`n" + $footer, (New-Object Text.UTF8Encoding($false)))
Write-Host "RELEASE NOTES 已生成：$outFile（版本 $matchedVersion）"
