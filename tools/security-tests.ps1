# 导入安全回归测试：路径校验 + 恶意 zip 拒绝。
# 用法：powershell -NoProfile -ExecutionPolicy Bypass -File tools/security-tests.ps1 -Exe out/WTSkinManager.exe
param([string]$Exe = '')

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Exe)) { $Exe = Join-Path $Root 'out\WTSkinManager.exe' }
if (-not (Test-Path $Exe)) { throw "找不到 $Exe" }

$asm = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Resolve-Path $Exe)))
$util = $asm.GetType('WtSkin.Util')
$isSafe = $util.GetMethod('IsSafeRel', [Reflection.BindingFlags]'Public,Static')
$fail = 0

function Expect($actual, $expected, $name) {
    if ($actual -ne $expected) { Write-Host ("FAIL " + $name + " expected=" + $expected + " actual=" + $actual); $script:fail++ }
    else { Write-Host ("PASS " + $name) }
}

Expect ($isSafe.Invoke($null, @('a/b.txt'))) $true 'safe normal'
Expect ($isSafe.Invoke($null, @('../evil.txt'))) $false 'reject dotdot'
Expect ($isSafe.Invoke($null, @('a/../../b'))) $false 'reject nested dotdot'
Expect ($isSafe.Invoke($null, @('C:/x'))) $false 'reject drive'
Expect ($isSafe.Invoke($null, @('/abs'))) $false 'reject absolute'
Expect ($isSafe.Invoke($null, @('a//b'))) $false 'reject empty segment'

$tmp = Join-Path $env:TEMP ('wtsm-sec-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $tmp | Out-Null
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
function New-Zip($path, $entries) {
    $fs = [IO.File]::Create($path)
    $zip = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)
    foreach ($k in $entries.Keys) {
        $e = $zip.CreateEntry($k)
        $w = New-Object IO.StreamWriter($e.Open())
        $w.Write($entries[$k]); $w.Dispose()
    }
    $zip.Dispose(); $fs.Dispose()
}

$evil = Join-Path $tmp 'evil.zip'
New-Zip $evil ([ordered]@{ '../evil.txt' = 'pwn'; 'su_34.blk' = 'name:t="user"' })
$good = Join-Path $tmp 'good.zip'
New-Zip $good ([ordered]@{ 'su_34.blk' = 'name:t="user"' })

$pkgType = $asm.GetType('WtSkin.SkinPackage')
$analyze = $pkgType.GetMethod('Analyze', [Reflection.BindingFlags]'Public,Static')
$evilPkg = $analyze.Invoke($null, [object[]]@([string]$evil))
$goodPkg = $analyze.Invoke($null, [object[]]@([string]$good))

Expect $evilPkg.Ready $false 'evil zip rejected'
if ($evilPkg.Error -notmatch '不安全') { Write-Host ("FAIL evil error text: " + $evilPkg.Error); $fail++ } else { Write-Host 'PASS evil error text' }
Expect $goodPkg.Ready $true 'benign zip accepted'

# ---- SafeCombine 根目录边界 ----
$combine = $util.GetMethod('SafeCombine', [Reflection.BindingFlags]'Public,Static')
$rootDir = Join-Path $env:TEMP ('wtsm-root-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $rootDir | Out-Null
try {
    $null = $combine.Invoke($null, [object[]]@([string]$rootDir, [string]'a/b.txt'))
    Write-Host 'PASS safe combine inside root'
} catch { Write-Host ('FAIL safe combine: ' + $_.Exception.Message); $script:fail++ }
try {
    $null = $combine.Invoke($null, [object[]]@([string]$rootDir, [string]'../escape.txt'))
    Write-Host 'FAIL combine escape accepted'; $script:fail++
} catch { Write-Host 'PASS combine rejects escape' }
Remove-Item -Recurse -Force $rootDir -ErrorAction SilentlyContinue

# ---- 备份清理：手动备份不被删、按时间保留最新 ----
$config = $asm.GetType('WtSkin.Config')
$installField = $config.GetField('InstallDir', [Reflection.BindingFlags]'Public,Static')
$keepField = $config.GetField('KeepBackups', [Reflection.BindingFlags]'Public,Static')
$oldInstall = $installField.GetValue($null)
$oldKeep = $keepField.GetValue($null)
$tmpInstall = Join-Path $env:TEMP ('wtsm-trim-' + [Guid]::NewGuid().ToString('N'))
$backups = Join-Path $tmpInstall 'backups'
New-Item -ItemType Directory -Force (Join-Path $backups 'skins') | Out-Null
$autoA = Join-Path $backups 'template_a_20260101_120000'
$autoB = Join-Path $backups 'template_b_20260102_120000'
$autoC = Join-Path $backups 'template_c_20260103_120000'
foreach ($d in @($autoA,$autoB,$autoC)) { New-Item -ItemType Directory -Force $d | Out-Null }
$custom = Join-Path $backups 'my_manual_backup'
New-Item -ItemType Directory -Force $custom | Out-Null
[IO.File]::WriteAllText((Join-Path $autoA 'a.blk'),'a')
[IO.File]::WriteAllText((Join-Path $autoB 'b.blk'),'b')
[IO.File]::WriteAllText((Join-Path $autoC 'c.blk'),'c')
(Get-Item $autoA).LastWriteTimeUtc = (Get-Date).AddDays(-3)
(Get-Item $autoB).LastWriteTimeUtc = (Get-Date).AddDays(-2)
(Get-Item $autoC).LastWriteTimeUtc = (Get-Date).AddDays(-1)
$installField.SetValue($null, $tmpInstall)
$keepField.SetValue($null, 2)
$importer = $asm.GetType('WtSkin.Importer')
$trim = $importer.GetMethod('TrimBackups', [Reflection.BindingFlags]'NonPublic,Static')
$trim.Invoke($null, $null)
Expect (Test-Path (Join-Path $backups 'skins')) $true 'manual backups preserved'
Expect (Test-Path $autoA) $false 'oldest auto removed'
Expect (Test-Path $custom) $true 'custom backup preserved'
Expect (Test-Path $autoB) $true 'newer auto kept'
Expect (Test-Path $autoC) $true 'newest auto kept'
$installField.SetValue($null, $oldInstall)
$keepField.SetValue($null, $oldKeep)
Remove-Item -Recurse -Force $tmpInstall -ErrorAction SilentlyContinue

# ---- 资源上限：超过 5000 个条目应被拒绝 ----
$big = Join-Path $tmp 'big.zip'
$fs = [IO.File]::Create($big)
$zip = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)
for ($i = 0; $i -lt 5001; $i++) { $null = $zip.CreateEntry('f' + $i + '.bin') }
$zip.Dispose(); $fs.Dispose()
$bigPkg = $analyze.Invoke($null, [object[]]@([string]$big))
Expect $bigPkg.Ready $false 'oversized zip rejected'
if ($bigPkg.Error -notmatch '安全上限') { Write-Host ('FAIL oversized error: ' + $bigPkg.Error); $fail++ } else { Write-Host 'PASS oversized error text' }


# ---- 导入前清空：只清内容、保留文件夹，越界目录拒绝 ----
$gameField = $config.GetField('GameDir', [Reflection.BindingFlags]'Public,Static')
$oldGame = $gameField.GetValue($null)
$tmpGame = Join-Path $env:TEMP ('wtsm-clear-' + [Guid]::NewGuid().ToString('N'))
$userSkins = Join-Path $tmpGame 'UserSkins'
$target = Join-Path $userSkins 'unit_test'
New-Item -ItemType Directory -Force (Join-Path $target 'sub') | Out-Null
[IO.File]::WriteAllText((Join-Path $target 'old.blk'),'old')
[IO.File]::WriteAllText((Join-Path $target 'sub\old.png'),'old')
$gameField.SetValue($null, $tmpGame)
$clear = $importer.GetMethod('ClearFolder', [Reflection.BindingFlags]'NonPublic,Static')
$clear.Invoke($null, [object[]]@([string]$target))
Expect (Test-Path $target) $true 'target folder kept'
Expect ((Get-ChildItem $target -Force | Measure-Object).Count) 0 'target cleared'
$outside = Join-Path $env:TEMP ('wtsm-outside-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $outside | Out-Null
try {
    $clear.Invoke($null, [object[]]@([string]$outside))
    Write-Host 'FAIL clear outside accepted'; $script:fail++
} catch { Write-Host 'PASS clear rejects outside path' }
Remove-Item -Recurse -Force $tmpGame,$outside -ErrorAction SilentlyContinue
$gameField.SetValue($null, $oldGame)

Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
if ($fail -gt 0) { Write-Host ("SECURITY TESTS FAILED: " + $fail); exit 1 }
Write-Host 'SECURITY TESTS OK'
