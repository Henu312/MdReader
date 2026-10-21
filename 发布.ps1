#requires -Version 7.0
<#
.SYNOPSIS
    发布 MdReader 启动器：框架依赖 + 单文件，产物约 250 KB。

.DESCRIPTION
    运行时不在产物内，全部使用本机已安装的 .NET Desktop Runtime。
    发布目标是单文件 exe，它正在运行时 Windows 会锁住文件、覆盖会失败，
    所以默认先结束正在运行的 MdReader；不想被打断请加 -KeepRunning。

.PARAMETER Output
    输出目录，默认是项目根目录下的 dist。

.PARAMETER KeepRunning
    不自动结束正在运行的 MdReader。此时如果 exe 被占用，脚本会直接报错。

.EXAMPLE
    pwsh -File .\发布.ps1

.EXAMPLE
    pwsh -File .\发布.ps1 -Output D:\data\reader\发布\md-launcher
#>
[CmdletBinding()]
param(
    [string]$Output = (Join-Path $PSScriptRoot 'dist'),
    [switch]$KeepRunning
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\MdReader.Launcher\MdReader.Launcher.csproj'

Write-Host '→ 发布 MdReader（框架依赖 + 单文件）' -ForegroundColor Cyan

# 单文件 exe 运行中会被锁定，必须先把进程结束掉才能覆盖。
$running = @(Get-Process -Name 'MdReader' -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    $ids = ($running | ForEach-Object { $_.Id }) -join ', '
    if ($KeepRunning) {
        throw "MdReader 正在运行（PID $ids），exe 被占用无法覆盖。先关闭它，或去掉 -KeepRunning 再试。"
    }
    Write-Host "→ 先结束正在运行的 MdReader（PID $ids），否则 exe 无法覆盖" -ForegroundColor Yellow
    $running | Stop-Process -Force -ErrorAction SilentlyContinue
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 150
        if (-not (Get-Process -Name 'MdReader' -ErrorAction SilentlyContinue)) { break }
    }
}

dotnet publish $project -c Release -o $Output
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失败（退出码 $LASTEXITCODE）。若提示文件被占用，先关闭 MdReader.exe 再试。"
}

$exe = Join-Path $Output 'MdReader.exe'
if (-not (Test-Path $exe)) {
    throw "没有找到产物：$exe"
}

$info = Get-Item $exe
Write-Host ("→ 产物：{0}" -f $exe)
Write-Host ("→ 大小：{0:N0} 字节（{1:N0} KB）" -f $info.Length, ($info.Length / 1KB))
Write-Host '→ 同目录必须保留：viewer.html、browser.txt、extension-id.txt'

$desktop = @(dotnet --list-runtimes | Where-Object { $_ -match '^Microsoft\.WindowsDesktop\.App ' })
if ($desktop.Count -eq 0) {
    Write-Warning '本机没有 Microsoft.WindowsDesktop.App，双击 exe 会提示安装 .NET Desktop Runtime。'
}
else {
    Write-Host '→ 本机可用的 Desktop 运行时：'
    $desktop | ForEach-Object { Write-Host "    $_" }
}

Write-Host '→ 自检：' -NoNewline
& $exe --selftest | Out-Host
