# 版本号的唯一来源。其它脚本用 . "$PSScriptRoot\version.ps1" 引入本文件。
# 注意：本文件含中文，必须以 UTF-8 BOM 保存，否则 Windows PowerShell 5.1 会按 ANSI 解析而乱码。
$AppVersion = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
if ($AppVersion -notmatch '^\d+(\.\d+)+$') { throw "VERSION 内容不是合法版本号：'$AppVersion'" }
$DeliveryName = "校园网一键连接-v$AppVersion"
$DeliveryPath = Join-Path $PSScriptRoot "release\$DeliveryName"
$ZipPath = Join-Path $PSScriptRoot 'release\校园网一键连接-Windows-x64.zip'
