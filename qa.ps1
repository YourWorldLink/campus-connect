$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
& '.\build.ps1'
& '.\verify-ui.ps1'
