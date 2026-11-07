# 校验版本号唯一来源：src/AssemblyInfo.cs 与 package.json 必须一致。
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File build/check-version.ps1
#
# AssemblyInfo 使用四段版本（3.9.1.0），package.json 使用三段（3.9.1）；
# 约定：package.json 的 version 必须等于 AssemblyVersion 的前三段。
# 校验失败时退出码为 1，构建脚本会因此中止。

param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$assemblyPath = Join-Path $RepoRoot 'src\AssemblyInfo.cs'
$packagePath = Join-Path $RepoRoot 'package.json'

if (-not (Test-Path $assemblyPath)) { throw "缺少 $assemblyPath" }
if (-not (Test-Path $packagePath)) { throw "缺少 $packagePath" }

$assemblyText = Get-Content -Raw -Encoding UTF8 $assemblyPath
$assemblyMatch = [regex]::Match($assemblyText, 'AssemblyVersion\("([0-9]+(?:\.[0-9]+){1,3})"\)')
if (-not $assemblyMatch.Success) {
    throw 'src\AssemblyInfo.cs 里找不到 AssemblyVersion("x.y.z.w")'
}
$fileMatch = [regex]::Match($assemblyText, 'AssemblyFileVersion\("([0-9]+(?:\.[0-9]+){1,3})"\)')
if (-not $fileMatch.Success) {
    throw 'src\AssemblyInfo.cs 里找不到 AssemblyFileVersion("x.y.z.w")'
}

$assemblyVersion = $assemblyMatch.Groups[1].Value
$fileVersion = $fileMatch.Groups[1].Value
if ($assemblyVersion -ne $fileVersion) {
    throw "AssemblyVersion($assemblyVersion) 与 AssemblyFileVersion($fileVersion) 不一致"
}

$parts = $assemblyVersion.Split('.')
if ($parts.Count -lt 3) {
    throw "AssemblyVersion($assemblyVersion) 至少需要三段"
}
$shortVersion = ($parts[0..2] -join '.')

$package = Get-Content -Raw -Encoding UTF8 $packagePath | ConvertFrom-Json
$packageVersion = [string]$package.version
if ([string]::IsNullOrWhiteSpace($packageVersion)) {
    throw "package.json 缺少 version"
}
if ($packageVersion -ne $shortVersion) {
    throw ("版本不一致：package.json=$packageVersion，AssemblyInfo=$assemblyVersion（应为 $shortVersion）")
}

Write-Host ("VERSION OK  AssemblyInfo={0}  package.json={1}" -f $assemblyVersion, $packageVersion)
exit 0
