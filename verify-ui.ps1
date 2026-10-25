param([switch]$InstallShortcut)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$image = Join-Path $PSScriptRoot 'artifacts\campus-connect.png'
$report = Join-Path $PSScriptRoot 'artifacts\browser-tests.txt'
$watchdog = 90
$testProcess = Start-Process -FilePath (Join-Path $PSScriptRoot 'dist\BrowserTests.exe') -ArgumentList ('"' + $report + '"') -PassThru -WindowStyle Hidden
if (-not $testProcess.WaitForExit($watchdog * 1000)) { Stop-Process -Id $testProcess.Id; throw 'Browser tests timed out.' }
Get-Content -LiteralPath $report
if ($testProcess.ExitCode -ne 0) { throw 'Browser integration tests failed.' }
$preview = Start-Process -FilePath (Join-Path $PSScriptRoot 'dist\CampusConnect.exe') -ArgumentList ('--screenshot="' + $image + '"') -PassThru -WindowStyle Hidden
if (-not $preview.WaitForExit(20000)) { Stop-Process -Id $preview.Id; throw 'UI preview timed out.' }
if (-not (Test-Path -LiteralPath $image)) { throw 'Screenshot was not generated.' }
Write-Output "UI screenshot: $image"
$compactImage = Join-Path $PSScriptRoot 'artifacts\campus-connect-compact.png'
$compactPreview = Start-Process -FilePath (Join-Path $PSScriptRoot 'dist\CampusConnect.exe') -ArgumentList @('--compact', ('--screenshot="' + $compactImage + '"')) -PassThru -WindowStyle Hidden
if (-not $compactPreview.WaitForExit(20000)) { Stop-Process -Id $compactPreview.Id; throw 'Compact UI preview timed out.' }
if (-not (Test-Path -LiteralPath $compactImage)) { throw 'Compact screenshot was not generated.' }
Write-Output "Compact UI screenshot: $compactImage"
$largeImage = Join-Path $PSScriptRoot 'artifacts\campus-connect-maximized.png'
$largePreview = Start-Process -FilePath (Join-Path $PSScriptRoot 'dist\CampusConnect.exe') -ArgumentList @('--maximized', ('--screenshot="' + $largeImage + '"')) -PassThru -WindowStyle Hidden
if (-not $largePreview.WaitForExit(20000)) { Stop-Process -Id $largePreview.Id; throw 'Maximized UI preview timed out.' }
if (-not (Test-Path -LiteralPath $largeImage)) { throw 'Maximized screenshot was not generated.' }
Write-Output "Maximized UI screenshot: $largeImage"
if ($InstallShortcut) {
    $installer = Start-Process -FilePath (Join-Path $PSScriptRoot 'dist\CampusConnect.exe') -ArgumentList '--install-shortcut' -Wait -PassThru -WindowStyle Hidden
    if ($installer.ExitCode -ne 0) { throw 'Shortcut creation failed.' }
    $shortcutPath = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) '校园网一键连接.lnk'
    if (-not (Test-Path -LiteralPath $shortcutPath)) { throw 'Desktop shortcut was not created.' }
    Write-Output "Desktop shortcut: $shortcutPath"
}
