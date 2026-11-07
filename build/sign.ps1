# 可选：给 out/WTSkinManager.exe 做 Authenticode 签名。
#
#   $env:WTSM_SIGN_PFX = 'C:\path\to\codesign.pfx'
#   $env:WTSM_SIGN_PASSWORD = '********'
#   powershell -NoProfile -ExecutionPolicy Bypass -File build/sign.ps1
#
# 未设置 WTSM_SIGN_PFX 时直接跳过并以 0 退出，方便在公开仓库/CI 中无证书构建。
# 私钥文件（.pfx）绝不应提交到仓库；.gitignore 已排除 *.pfx。

param(
    [string]$Exe = '',
    [string]$Pfx = $env:WTSM_SIGN_PFX,
    [string]$Password = $env:WTSM_SIGN_PASSWORD,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($Exe)) {
    $Exe = Join-Path $Root 'out\WTSkinManager.exe'
}

if ([string]::IsNullOrWhiteSpace($Pfx)) {
    Write-Host 'SIGN 跳过：未设置 WTSM_SIGN_PFX（无证书构建是受支持的默认路径）'
    exit 0
}

if (-not (Test-Path $Pfx)) { throw "找不到签名证书：$Pfx" }
if (-not (Test-Path $Exe)) { throw "找不到待签名文件：$Exe，请先运行 build/build.ps1" }

$signtool = $null
$cmd = Get-Command 'signtool.exe' -ErrorAction SilentlyContinue
if ($cmd -ne $null) {
    $signtool = $cmd.Source
}
if ($null -eq $signtool) {
    $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (Test-Path $kits) {
        $candidates = @(Get-ChildItem $kits -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            ForEach-Object {
                $x64 = Join-Path $_.FullName 'x64\signtool.exe'
                $x86 = Join-Path $_.FullName 'x86\signtool.exe'
                if (Test-Path $x64) { $x64 } elseif (Test-Path $x86) { $x86 }
            })
        if ($candidates.Count -gt 0) { $signtool = $candidates[0] }
    }
}
if ($null -eq $signtool) {
    throw '找不到 signtool.exe：请安装 Windows SDK，或把 signtool 加入 PATH'
}

$signArgs = @('sign', '/fd', 'SHA256', '/f', $Pfx, '/p', $Password)
if (-not [string]::IsNullOrWhiteSpace($TimestampUrl)) {
    $signArgs += @('/tr', $TimestampUrl, '/td', 'SHA256')
}
$signArgs += $Exe

Write-Host ("SIGN {0}" -f $signtool)
& $signtool @signArgs
if ($LASTEXITCODE -ne 0) { throw "签名失败，退出码 $LASTEXITCODE" }
Write-Host 'SIGN OK'
exit 0
