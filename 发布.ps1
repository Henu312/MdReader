#requires -Version 7.0
<#
.SYNOPSIS
    发布 MdReader 启动器：框架依赖 + 单文件，产物约 250 KB。

.DESCRIPTION
    运行时不在产物内，全部使用本机已安装的 .NET Desktop Runtime。
    发布完成后会检查本机是否真的有 Microsoft.WindowsDesktop.App，没有就给出警告。

.PARAMETER Output
    输出目录，默认是项目根目录下的 dist。

.EXAMPLE
    pwsh -File .\发布.ps1

.EXAMPLE
    pwsh -File .\发布.ps1 -Output D:\data\reader\发布\md-launcher
#>
[CmdletBinding()]
param(
    [string]$Output = (Join-Path $PSScriptRoot 'dist')
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\MdReader.Launcher\MdReader.Launcher.csproj'

Write-Host '→ 发布 MdReader（框架依赖 + 单文件）' -ForegroundColor Cyan
dotnet publish $project -c Release -o $Output
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失败（退出码 $LASTEXITCODE）"
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
