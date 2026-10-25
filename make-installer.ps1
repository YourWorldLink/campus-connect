$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
. "$PSScriptRoot\version.ps1"

$makensis = Join-Path $PSScriptRoot 'vendor\nsis\makensis.exe'
if (-not (Test-Path -LiteralPath $makensis)) { throw '缺少 vendor\nsis，安装包无法编译。见 vendor\nsis\SOURCE.txt。' }
if (-not (Test-Path -LiteralPath $DeliveryPath)) { throw "未找到 $DeliveryPath，请先运行 package.ps1。" }

# installer.nsi 含中文，必须带 UTF-8 BOM，否则 makensis 会按系统代码页解析而乱码。
$nsi = Join-Path $PSScriptRoot 'installer.nsi'
$bytes = [System.IO.File]::ReadAllBytes($nsi)
if ($bytes.Length -lt 3 -or $bytes[0] -ne 0xEF -or $bytes[1] -ne 0xBB -or $bytes[2] -ne 0xBF) {
    throw 'installer.nsi 缺少 UTF-8 BOM，其中的中文会被 makensis 误读。'
}

# VIProductVersion 需要四段数字。
$version4 = if ($AppVersion -match '^\d+\.\d+$') { "$AppVersion.0.0" } else { $AppVersion }
$out = Join-Path $PSScriptRoot "release\校园网一键连接-$AppVersion-安装程序.exe"

& $makensis "/DAPP_VERSION=$AppVersion" "/DAPP_VERSION4=$version4" "/DSOURCE_DIR=$DeliveryPath" "/DOUT_FILE=$out" $nsi
if ($LASTEXITCODE -ne 0) { throw 'makensis 编译失败。' }
if (-not (Test-Path -LiteralPath $out)) { throw '安装包未生成。' }
Get-Item -LiteralPath $out | Select-Object FullName, Length
Write-Output "Installer: $out"
