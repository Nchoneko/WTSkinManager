# 生成发布压缩包（构建产物，写入 out/，不提交）。
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File build/pack.ps1
#
# 包内包含：WTSkinManager.exe、LICENSE、NOTICE、README.md。

[CmdletBinding()]
param(
    [string]$OutputDir = ''
)

function Get-Sha256Hex {
    param([string]$Path)
    try {
        return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    } catch {
        $sha = [System.Security.Cryptography.SHA256]::Create()
        $stream = [System.IO.File]::OpenRead($Path)
        try {
            $bytes = $sha.ComputeHash($stream)
            return (($bytes | ForEach-Object { $_.ToString('x2') }) -join '')
        } finally { $stream.Dispose(); $sha.Dispose() }
    }
}

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $Root 'out'
}

$exe = Join-Path $OutputDir 'WTSkinManager.exe'
if (-not (Test-Path $exe)) {
    Write-Host '未找到 out\WTSkinManager.exe，先运行构建…'
    & (Join-Path $PSScriptRoot 'build.ps1') -OutputDir $OutputDir -SkipReleaseZip
    if ($LASTEXITCODE -ne 0) { throw '构建失败' }
}

$asmText = Get-Content -Raw -Encoding UTF8 (Join-Path $Root 'src\AssemblyInfo.cs')
$asmMatch = [regex]::Match($asmText, 'AssemblyVersion\("([0-9]+(?:\.[0-9]+){1,3})"\)')
if (-not $asmMatch.Success) { throw 'src\AssemblyInfo.cs 里找不到 AssemblyVersion' }
$version = $asmMatch.Groups[1].Value

$stageName = 'WTSkinManager-v' + $version + '-win64'
$stage = Join-Path $OutputDir $stageName
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null

Copy-Item $exe $stage -Force
foreach ($name in @('LICENSE', 'NOTICE', 'README.md')) {
    $path = Join-Path $Root $name
    if (Test-Path $path) { Copy-Item $path $stage -Force }
}

$zipPath = Join-Path $OutputDir ($stageName + '.zip')
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory(
    $stage, $zipPath, [IO.Compression.CompressionLevel]::Fastest, $false)
Remove-Item $stage -Recurse -Force

$info = Get-Item $zipPath
$hash = Get-Sha256Hex $zipPath
Write-Host ("PACK OK  {0}" -f $info.FullName) -ForegroundColor Green
Write-Host ("         {0:N1} MB  sha256 {1}" -f ($info.Length / 1MB), $hash)
