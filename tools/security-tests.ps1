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

Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
if ($fail -gt 0) { Write-Host ("SECURITY TESTS FAILED: " + $fail); exit 1 }
Write-Host 'SECURITY TESTS OK'
