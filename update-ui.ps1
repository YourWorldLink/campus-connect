$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
. "$PSScriptRoot\version.ps1"
& '.\package.ps1'
& '.\make-installer.ps1'
& '.\dist\ActivationTests.exe' (Join-Path $DeliveryPath 'CampusConnect.exe')
if ($LASTEXITCODE -ne 0) { throw 'Updated window activation tests failed.' }
& '.\install.ps1'
