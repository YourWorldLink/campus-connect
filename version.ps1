# 版本号的唯一来源。其它脚本用 . "$PSScriptRoot\version.ps1" 引入本文件。
# 注意：本文件含中文，必须以 UTF-8 BOM 保存，否则 Windows PowerShell 5.1 会按 ANSI 解析而乱码。
$AppVersion = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
# 允许 1–3 段数字（1 / 1.19 / 1.19.1），再补齐成四段：
# .NET 的 AssemblyVersion 与 NSIS 的 VIProductVersion 都要求恰好四段。
if ($AppVersion -notmatch '^\d+(\.\d+){0,2}$') { throw "VERSION 内容不是合法版本号（1–3 段数字，如 1.19）：'$AppVersion'" }
$AppVersion4 = ((($AppVersion -split '\.') + @('0','0','0'))[0..3]) -join '.'
$DeliveryName = "CampusConnect-v$AppVersion"
$DeliveryPath = Join-Path $PSScriptRoot "release\$DeliveryName"
$ZipPath = Join-Path $PSScriptRoot "release\CampusConnect-$AppVersion-win64.zip"
