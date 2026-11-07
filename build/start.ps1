# 启动已构建的程序（构建产物在 out/，不提交）。
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File build/start.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File build/start.ps1 -Wait

[CmdletBinding()]
param(
    [string]$Exe = '',
    [switch]$Wait
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Exe)) {
    $Exe = Join-Path $Root 'out\WTSkinManager.exe'
}
if (-not (Test-Path $Exe)) {
    throw "找不到 $Exe，请先运行 build\build.ps1"
}

$process = Start-Process -FilePath $Exe -PassThru
Write-Host ("START {0}  pid={1}" -f $Exe, $process.Id)
if ($Wait) {
    $process.WaitForExit()
    Write-Host ("EXIT  code={0}" -f $process.ExitCode)
}
