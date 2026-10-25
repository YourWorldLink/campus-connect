$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
& '.\build.ps1'
& '.\update-ui.ps1'
