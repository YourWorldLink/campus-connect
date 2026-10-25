$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
. "$PSScriptRoot\version.ps1"
New-Item -ItemType Directory -Path $DeliveryPath -Force | Out-Null
$files = @('CampusConnect.exe','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll','campus.ico','WebView2-LICENSE.txt','使用说明.txt','ICON-SOURCES.md','THIRD-PARTY-NOTICES.md')
foreach ($name in $files) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot "dist\$name") -Destination $DeliveryPath -Force }
Compress-Archive -LiteralPath $DeliveryPath -DestinationPath $ZipPath -Force
Get-Item -LiteralPath $ZipPath | Select-Object FullName,Length
Write-Output "Delivery folder: $DeliveryPath"
