$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
. "$PSScriptRoot\version.ps1"

# 先确认 dist 里的二进制确实是 VERSION 对应的版本，避免把旧 exe 装进新版本目录。
$distExe = Join-Path $PSScriptRoot 'dist\CampusConnect.exe'
if (-not (Test-Path -LiteralPath $distExe)) { throw '未找到 dist\CampusConnect.exe，请先运行 build.ps1。' }
$builtVersion = (Get-Item -LiteralPath $distExe).VersionInfo.ProductVersion
if ($builtVersion -ne $AppVersion) {
    throw "dist\CampusConnect.exe 的版本是 '$builtVersion'，与 VERSION（$AppVersion）不一致。请先运行 build.ps1 重新编译。"
}

# 交付目录必须从空开始：残留文件会被 zip 和安装包一并打包带走。
if (Test-Path -LiteralPath $DeliveryPath) { Remove-Item -LiteralPath $DeliveryPath -Recurse -Force }
New-Item -ItemType Directory -Path $DeliveryPath -Force | Out-Null
$files = @('CampusConnect.exe','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll','campus.ico','WebView2-LICENSE.txt','使用说明.txt','ICON-SOURCES.md','THIRD-PARTY-NOTICES.md')
foreach ($name in $files) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot "dist\$name") -Destination $DeliveryPath -Force }
Compress-Archive -LiteralPath $DeliveryPath -DestinationPath $ZipPath -Force
Get-Item -LiteralPath $ZipPath | Select-Object FullName,Length
Write-Output "Delivery folder: $DeliveryPath"
