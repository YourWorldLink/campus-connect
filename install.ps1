$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
. "$PSScriptRoot\version.ps1"
$program = Join-Path $DeliveryPath 'CampusConnect.exe'
if (-not (Test-Path -LiteralPath $program)) { throw 'Build the release using package.ps1 first.' }
$installer = Start-Process -FilePath $program -ArgumentList '--install-shortcut' -Wait -PassThru -WindowStyle Hidden
if ($installer.ExitCode -ne 0) { throw 'Shortcut creation failed.' }
$shortcutPath = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) '校园网一键连接.lnk'
if (-not (Test-Path -LiteralPath $shortcutPath)) { throw 'Desktop shortcut missing.' }
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
if ($shortcut.TargetPath -ne $program) { throw 'Shortcut target verification failed.' }
Write-Output "Desktop shortcut: $shortcutPath"
Write-Output "Target: $($shortcut.TargetPath)"
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) | Out-Null
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
# This is the user's interactive configuration window, so show it normally.
Start-Process -FilePath $program -ArgumentList '--settings' -WindowStyle Normal
