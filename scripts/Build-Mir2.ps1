[CmdletBinding()]
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
if (!$OutputDirectory) { throw 'Choose a new staging output directory; this script never publishes over a running deployment.' }
$source = Split-Path $PSScriptRoot -Parent
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Output directory already exists; choose a new staging directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
dotnet publish (Join-Path $source 'Server.MirForms\Server.csproj') -c Release -r win-x64 --self-contained false -o (Join-Path $output 'Server') -p:PublishSingleFile=false -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Server publish failed.' }
dotnet publish (Join-Path $source 'Client\Client.csproj') -c Release -r win-x64 --self-contained true -o (Join-Path $output 'Client') -p:PublishSingleFile=false -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Client publish failed.' }
Write-Output "Staged binaries: $output"
