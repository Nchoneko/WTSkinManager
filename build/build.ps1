# WTSkinManager 一键构建脚本
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File build/build.ps1
#
# 只依赖 Windows 10/11 自带的 .NET Framework 4.x（csc.exe）与 PowerShell 5.1，
# 不需要安装 .NET SDK、Visual Studio 或任何 NuGet 包。
#
# 流程：
#   1. 校验 src/AssemblyInfo.cs 与 package.json 的版本一致；
#   2. 校验 assets/vehicles/manifest.json 与每张 PNG（存在、≤120KB、SHA-256）；
#   3. 从 assets/vehicles/*.png 重新生成 out/vehicles.zip（不依赖预置 zip）；
#   4. 校验 assets/data 的 schemaVersion / dataVersion / count 头；
#   5. 生成 out/generated/data-manifest.json 与 build-info.ini；
#   6. csc 编译为单文件 out/WTSkinManager.exe；
#   7. 可选：设置 WTSM_SIGN_PFX / WTSM_SIGN_PASSWORD 时调用 build/sign.ps1 签名。
#
# 产物全部写入 out/（已被 .gitignore 排除），不会改动仓库里的资产文件。

[CmdletBinding()]
param(
    [string]$OutputDir = '',
    [switch]$SkipSignature,
    [switch]$SkipReleaseZip
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
$ProgressPreference = 'SilentlyContinue'

$Root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $Root 'out'
}
$OutputDir = [IO.Path]::GetFullPath($OutputDir)
$GeneratedDir = Join-Path $OutputDir 'generated'

$SrcDir = Join-Path $Root 'src'
$AssetDir = Join-Path $Root 'assets'
$IconDir = Join-Path $AssetDir 'icons'
$VehicleDir = Join-Path $AssetDir 'vehicles'
$DataDir = Join-Path $AssetDir 'data'
$CertAsset = Join-Path $AssetDir 'certs\publisher.cer'
$ChangelogPath = Join-Path $Root 'CHANGELOG.md'

function Write-Step([string]$Text) {
    Write-Host ''
    Write-Host ("==> " + $Text) -ForegroundColor Cyan
}

function Get-FullPath([string]$Path) {
    return [IO.Path]::GetFullPath($Path)
}

function Count-TabLines([string]$Path) {
    $n = 0
    foreach ($line in Get-Content -Encoding UTF8 $Path) {
        $s = $line.Trim()
        if ($s.Length -eq 0 -or $s.StartsWith('#') -or $s.StartsWith(';')) { continue }
        if ($s.Contains("`t")) { $n++ }
    }
    return $n
}

function Count-IniEntries([string]$Path) {
    $section = ''
    $n = 0
    foreach ($line in Get-Content -Encoding UTF8 $Path) {
        $s = $line.Trim()
        if ($s.Length -eq 0 -or $s.StartsWith('#') -or $s.StartsWith(';')) { continue }
        if ($s.StartsWith('[') -and $s.EndsWith(']')) {
            $section = $s.Substring(1, $s.Length - 2).Trim().ToLowerInvariant()
            continue
        }
        if (($section -eq 'nations' -or $section -eq 'short') -and $s.Contains('=')) { $n++ }
    }
    return $n
}

function Read-HeaderCount([string]$Path) {
    foreach ($line in Get-Content -Encoding UTF8 $Path) {
        $s = $line.Trim()
        if ($s.StartsWith('#') -or $s.StartsWith(';')) { $s = $s.Substring(1).Trim() }
        if ($s -match '^count\s*=\s*(\d+)$') { return [int]$matches[1] }
    }
    return -1
}

function Require-Header([string]$Path, [int]$Count, [int]$SchemaVersion, [string]$DataVersion) {
    $text = Get-Content -Raw -Encoding UTF8 $Path
    if ($text -notmatch "(?m)^\s*#?\s*schemaVersion\s*=\s*$SchemaVersion\s*$") {
        throw ("$Path 缺少 schemaVersion=$SchemaVersion 头")
    }
    if ($text -notmatch "(?m)^\s*#?\s*dataVersion\s*=\s*$([regex]::Escape($DataVersion))\s*$") {
        throw ("$Path 缺少 dataVersion=$DataVersion 头")
    }
    if ($Count -ge 0 -and $text -notmatch "(?m)^\s*#?\s*count\s*=\s*$Count\s*$") {
        throw ("$Path 的 count 头与条目数 $Count 不一致")
    }
}

Write-Host "WTSkinManager build" -ForegroundColor Yellow
Write-Host ("Repo:   " + $Root)
Write-Host ("Output: " + $OutputDir)

# ---------------- 环境检查 ----------------
Write-Step '检查构建环境'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw '找不到 .NET Framework 4.x 的 csc.exe，请确认系统已安装 .NET Framework 4.x'
}
$gac = Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL'
$compression = Join-Path $gac 'System.IO.Compression\v4.0_4.0.0.0__b77a5c561934e089\System.IO.Compression.dll'
$compressionFs = Join-Path $gac 'System.IO.Compression.FileSystem\v4.0_4.0.0.0__b77a5c561934e089\System.IO.Compression.FileSystem.dll'
if (-not (Test-Path $compression)) { throw "找不到 System.IO.Compression.dll：$compression" }
if (-not (Test-Path $compressionFs)) { throw "找不到 System.IO.Compression.FileSystem.dll：$compressionFs" }
Write-Host ("csc: " + $csc)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

New-Item -ItemType Directory -Force $OutputDir | Out-Null
New-Item -ItemType Directory -Force $GeneratedDir | Out-Null

# ---------------- 版本一致性 ----------------
Write-Step '校验版本号（AssemblyInfo.cs / package.json）'
& (Join-Path $PSScriptRoot 'check-version.ps1') -RepoRoot $Root
if ($LASTEXITCODE -ne 0) { throw '版本号校验失败' }

$asmText = Get-Content -Raw -Encoding UTF8 (Join-Path $SrcDir 'AssemblyInfo.cs')
$asmMatch = [regex]::Match($asmText, 'AssemblyVersion\("([0-9]+(?:\.[0-9]+){1,3})"\)')
if (-not $asmMatch.Success) { throw 'src\AssemblyInfo.cs 里找不到 AssemblyVersion' }
$buildVersion = $asmMatch.Groups[1].Value
$shortVersion = (($buildVersion.Split('.')[0..2]) -join '.')

# ---------------- 图标清单 ----------------
Write-Step '校验载具图标清单'
if (-not (Test-Path $VehicleDir)) { throw "找不到图标目录：$VehicleDir" }
$manifestPath = Join-Path $VehicleDir 'manifest.json'
if (-not (Test-Path $manifestPath)) { throw "缺少清单：$manifestPath" }
if (-not (Test-Path (Join-Path $VehicleDir '_placeholder.png'))) {
    throw '缺少统一占位图 assets\vehicles\_placeholder.png'
}

$manifest = Get-Content -Raw -Encoding UTF8 $manifestPath | ConvertFrom-Json
$entries = @($manifest.entries)
if ($entries.Count -eq 0) { throw '图标清单为空' }
if ([int]$manifest.count -ne $entries.Count) {
    throw ("图标清单 count=$($manifest.count) 与条目数 $($entries.Count) 不一致")
}

$ok = 0
$retrying = 0
$permanent = 0
$totalBytes = 0
$unique = @{}
$bad = @()
$zipItems = New-Object System.Collections.ArrayList

foreach ($e in $entries) {
    $slug = [string]$e.slug
    if ($slug -notmatch '^[a-z0-9_]+$') { $bad += "非法 slug: $slug"; continue }
    switch ([string]$e.status) {
        'ok' {
            $ok++
            $file = [string]$e.file
            if ($file -notmatch '^[a-z0-9_]+\.png$') { $bad += "非法文件名 $slug -> $file"; continue }
            $path = Join-Path $VehicleDir $file
            if (-not (Test-Path $path)) { $bad += "清单登记但文件缺失: $slug -> $file"; continue }
            $size = (Get-Item $path).Length
            if ($size -gt 120KB) { $bad += "超过 120KB: $file $size"; continue }
            if (-not $unique.ContainsKey($file)) {
                $unique[$file] = $size
                $totalBytes += $size
                $hash = Get-Sha256Hex $path
                if ($hash -ne ([string]$e.sha256).ToLowerInvariant()) {
                    $bad += "sha256 不一致: $slug"
                }
                [void]$zipItems.Add([pscustomobject]@{ Name = ($slug + '.png'); Path = $path })
            }
        }
        'retrying' { $retrying++ }
        'permanent' { $permanent++ }
        default { $bad += "未知状态 $slug : $($e.status)" }
    }
}
if ($bad.Count -gt 0) {
    $bad | Select-Object -First 20 | ForEach-Object { Write-Host ("  ! " + $_) -ForegroundColor Red }
    throw ("图标清单校验失败，共 $($bad.Count) 条")
}
if ($totalBytes -gt 350MB) { throw ("图标总体积超出 350MB 预算: {0:N0} bytes" -f $totalBytes) }
Write-Host ("ICONS 清单 {0} 条：成功 {1} · 重试中 {2} · 永久失败 {3}" -f $entries.Count, $ok, $retrying, $permanent)
Write-Host ("ICONS 唯一文件 {0} 张  {1:N0} bytes（{2:N1} MB）" -f $unique.Count, $totalBytes, ($totalBytes / 1MB))

# ---------------- 数据版本 ----------------
Write-Step '校验随包数据版本头'
$versionFile = Join-Path $DataDir 'data-version.ini'
if (-not (Test-Path $versionFile)) { throw "缺少 assets\data\data-version.ini" }
$versionMap = @{}
foreach ($line in Get-Content -Encoding UTF8 $versionFile) {
    if ($line -match '^\s*[#;]') { continue }
    if ($line -match '^\s*([^=]+)=(.*)$') { $versionMap[$matches[1].Trim()] = $matches[2].Trim() }
}
$schemaVersion = 0
if (-not [int]::TryParse([string]$versionMap['schemaVersion'], [ref]$schemaVersion) -or $schemaVersion -le 0) {
    throw 'data-version.ini 的 schemaVersion 无效'
}
$dataVersion = [string]$versionMap['dataVersion']
if ([string]::IsNullOrWhiteSpace($dataVersion)) { throw 'data-version.ini 缺少 dataVersion' }

$specs = @()
$vehiclesCount = Count-TabLines (Join-Path $DataDir 'vehicles.dat')
Require-Header (Join-Path $DataDir 'vehicles.dat') $vehiclesCount $schemaVersion $dataVersion
$specs += @{ file = 'vehicles.dat'; entries = $vehiclesCount }

$nationsCount = Count-TabLines (Join-Path $DataDir 'nations.dat')
Require-Header (Join-Path $DataDir 'nations.dat') $nationsCount $schemaVersion $dataVersion
$specs += @{ file = 'nations.dat'; entries = $nationsCount }

$shortCount = Count-IniEntries (Join-Path $DataDir 'shortnames.ini')
Require-Header (Join-Path $DataDir 'shortnames.ini') $shortCount $schemaVersion $dataVersion
$specs += @{ file = 'shortnames.ini'; entries = $shortCount }

$metaPath = Join-Path $DataDir 'vehicle_meta.dat'
$metaCount = Read-HeaderCount $metaPath
$metaActual = Count-TabLines $metaPath
if ($metaCount -ge 0 -and $metaCount -ne $metaActual) {
    throw ("vehicle_meta.dat 的 count=$metaCount 与实际 $metaActual 不一致")
}
Require-Header $metaPath $metaActual $schemaVersion $dataVersion
$specs += @{ file = 'vehicle_meta.dat'; entries = $metaActual }

$manifestSchema = [int]$manifest.schemaVersion
$manifestData = [string]$manifest.dataVersion
if ($manifestSchema -ne $schemaVersion) { throw 'manifest.json 的 schemaVersion 与 data-version.ini 不一致' }
if ($manifestData -ne $dataVersion) { throw 'manifest.json 的 dataVersion 与 data-version.ini 不一致' }
$specs += @{ file = 'vehicles/manifest.json'; entries = $entries.Count }

$dataManifest = [ordered]@{
    schemaVersion = $schemaVersion
    dataVersion = $dataVersion
    generated = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    count = $specs.Count
    files = @()
}
foreach ($spec in $specs) {
    $dataManifest.files += [ordered]@{
        file = [string]$spec.file
        schemaVersion = $schemaVersion
        dataVersion = $dataVersion
        entries = [int]$spec.entries
    }
}
$dataManifestPath = Join-Path $GeneratedDir 'data-manifest.json'
$dataManifestJson = $dataManifest | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText($dataManifestPath, $dataManifestJson, (New-Object Text.UTF8Encoding($false)))
Write-Host ("DATA schema={0} dataVersion={1} 文件 {2} 份" -f $schemaVersion, $dataVersion, $specs.Count)

# updates.dat 与 CHANGELOG.md 的版本段必须互相对应
$updatesPath = Join-Path $DataDir 'updates.dat'
if (-not (Test-Path $updatesPath)) { throw '缺少 assets\data\updates.dat' }
$updateVersions = @()
foreach ($line in Get-Content -Encoding UTF8 $updatesPath) {
    if ($line -match '^\s*\[(\d+(?:\.\d+){1,3})\]\s*$') { $updateVersions += $matches[1] }
}
if ($updateVersions.Count -eq 0) { throw 'updates.dat 没有任何版本段' }
$changelogVersions = @()
foreach ($line in Get-Content -Encoding UTF8 $ChangelogPath) {
    if ($line -match '^##\s+(\d+(?:\.\d+){1,3})') { $changelogVersions += $matches[1] }
}
$updateShort = @()
foreach ($version in $updateVersions) { $updateShort += (($version.Split('.')[0..2]) -join '.') }
$changelogShort = @()
foreach ($version in $changelogVersions) { $changelogShort += (($version.Split('.')[0..2]) -join '.') }
foreach ($version in $updateShort) {
    if ($changelogShort -notcontains $version) {
        throw ("updates.dat 的版本 $version 在 CHANGELOG.md 里没有对应 ## 段落")
    }
}
foreach ($version in $changelogShort) {
    if ($updateShort -notcontains $version) {
        throw ("CHANGELOG.md 的版本 $version 在 updates.dat 里没有对应段落")
    }
}
Write-Host ("UPDATES 版本段 {0} 个，与 CHANGELOG.md 一致" -f $updateVersions.Count)

# ---------------- 证书 ----------------
Write-Step '校验发布者证书（仅公钥）'
$certExpected = '1DBFA4F88846892697ED714F02E562F86D0FA01A1105FA1E8B8C73F9D2643DF2'
if (-not (Test-Path $CertAsset)) {
    throw ("缺少发布者证书：$CertAsset（只含公钥；私钥不进入仓库）")
}
$certHash = (Get-Sha256Hex $CertAsset).ToUpperInvariant()
if ($certHash -ne $certExpected) {
    throw ("发布者证书 SHA-256 不符：期望 $certExpected，实际 $certHash")
}
$privateKeys = @(Get-ChildItem $AssetDir -Recurse -File |
    Where-Object { $_.Extension -in @('.pfx', '.p12', '.pvk', '.key', '.snk') })
if ($privateKeys.Count -gt 0) {
    $privateList = ($privateKeys | ForEach-Object { $_.FullName }) -join '; '
    throw ('assets 里不允许出现私钥文件：' + $privateList)
}
Write-Host ("CERT publisher.cer {0:N0} bytes SHA256 {1}" -f (Get-Item $CertAsset).Length, $certHash)

# ---------------- 重新生成 vehicles.zip ----------------
Write-Step '从 assets/vehicles/*.png 重新生成 vehicles.zip'
$iconsZip = Join-Path $OutputDir 'vehicles.zip'
if (Test-Path $iconsZip) { Remove-Item $iconsZip -Force }
$zip = [IO.Compression.ZipFile]::Open($iconsZip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($item in $zipItems) {
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip, $item.Path, $item.Name, [IO.Compression.CompressionLevel]::Optimal)
    }
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $zip, (Join-Path $VehicleDir '_placeholder.png'), '_placeholder.png',
        [IO.Compression.CompressionLevel]::Optimal)
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $zip, $manifestPath, 'manifest.json',
        [IO.Compression.CompressionLevel]::Optimal)
}
finally {
    $zip.Dispose()
}

$zipCheck = [IO.Compression.ZipFile]::OpenRead($iconsZip)
try {
    $zipEntryCount = $zipCheck.Entries.Count
    if ($zipCheck.GetEntry('_placeholder.png') -eq $null) { throw 'vehicles.zip 缺少 _placeholder.png' }
    if ($zipCheck.GetEntry('a2d.png') -eq $null -and $zipItems.Count -gt 0) {
        throw 'vehicles.zip 缺少首条图标，生成失败'
    }
}
finally {
    $zipCheck.Dispose()
}
if ($zipEntryCount -ne ($zipItems.Count + 2)) {
    throw ("vehicles.zip 条目数 $zipEntryCount 与预期 $($zipItems.Count + 2) 不一致")
}
$zipBytes = (Get-Item $iconsZip).Length
Write-Host ("ZIP {0} 个条目  {1:N0} bytes（{2:N1} MB）" -f $zipEntryCount, $zipBytes, ($zipBytes / 1MB))

# ---------------- 构建信息 ----------------
Write-Step '写入构建信息'
$buildTime = Get-Date -Format 'yyyy/MM/dd HH:mm:ss'
$buildInfoPath = Join-Path $GeneratedDir 'build-info.ini'
[IO.File]::WriteAllLines($buildInfoPath, @("version=$buildVersion", "buildTime=$buildTime"), (New-Object Text.UTF8Encoding($false)))
Write-Host ("BUILD {0} · {1}" -f $buildVersion, $buildTime)

# ---------------- 编译 ----------------
Write-Step 'csc 编译单文件 exe'
$sources = Get-ChildItem $SrcDir -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.FullName }
$exe = Join-Path $OutputDir 'WTSkinManager.exe'
if (Test-Path $exe) { Remove-Item $exe -Force }

$cscArgs = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/codepage:65001',
    '/debug-',
    ('/win32icon:' + (Join-Path $IconDir 'app.ico')),
    ('/win32manifest:' + (Join-Path $SrcDir 'app.manifest')),
    ('/resource:' + (Join-Path $DataDir 'vehicles.dat') + ',vehicles.dat'),
    ('/resource:' + (Join-Path $DataDir 'nations.dat') + ',nations.dat'),
    ('/resource:' + (Join-Path $IconDir 'icon-256.png') + ',app.png'),
    ('/resource:' + (Join-Path $DataDir 'shortnames.ini') + ',shortnames.dat'),
    ('/resource:' + (Join-Path $DataDir 'vehicle_meta.dat') + ',vehicle_meta.dat'),
    ('/resource:' + $dataManifestPath + ',data-manifest.json'),
    ('/resource:' + (Join-Path $DataDir 'updates.dat') + ',updates.dat'),
    ('/resource:' + $buildInfoPath + ',build-info.dat'),
    ('/resource:' + (Join-Path $Root 'assets\github.png') + ',github.png'),
    ('/resource:' + (Join-Path $Root 'assets\live.png') + ',live.png'),
    ('/resource:' + (Join-Path $Root 'assets\wtliker.png') + ',wtliker.png'),
    ('/resource:' + (Join-Path $Root 'assets\donate\alipay.jpg') + ',donate_alipay.jpg'),
    ('/resource:' + (Join-Path $Root 'assets\donate\wechat.png') + ',donate_wechat.png'),
    ('/resource:' + $CertAsset + ',publisher.cer'),
    ('/resource:' + $iconsZip + ',vehicles.zip'),
    ('/out:' + $exe),
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    ('/r:' + $compression),
    ('/r:' + $compressionFs)
)
$cscArgs += $sources

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码 $LASTEXITCODE" }
if (-not (Test-Path $exe)) { throw '编译结束但没有生成 exe' }
$exeInfo = Get-Item $exe
if ($exeInfo.Length -lt 1MB) { throw ("exe 体积异常：{0} bytes" -f $exeInfo.Length) }

# ---------------- 可选签名 ----------------
Write-Step '可选代码签名'
if ($SkipSignature) {
    Write-Host 'SIGN 已跳过（-SkipSignature）'
}
elseif ([string]::IsNullOrWhiteSpace($env:WTSM_SIGN_PFX)) {
    Write-Host 'SIGN 跳过（未设置 WTSM_SIGN_PFX；设置证书路径与 WTSM_SIGN_PASSWORD 后可自动签名）'
}
else {
    & (Join-Path $PSScriptRoot 'sign.ps1') -Exe $exe
    if ($LASTEXITCODE -ne 0) { throw '签名失败' }
}

# ---------------- 完成 ----------------
$finalExe = Get-Item $exe
$exeHash = Get-Sha256Hex $exe
Write-Step '构建完成'
Write-Host ("OK  {0}" -f $finalExe.FullName) -ForegroundColor Green
Write-Host ("    version {0}   {1:N0} bytes ({2:N1} MB)" -f $buildVersion, $finalExe.Length, ($finalExe.Length / 1MB))
Write-Host ("    sha256  {0}" -f $exeHash)
Write-Host ("    zip     {0:N0} bytes" -f $zipBytes)

if (-not $SkipReleaseZip) {
    Write-Host '提示：运行 build/pack.ps1 可生成带 LICENSE/NOTICE/README 的发布压缩包。'
}
