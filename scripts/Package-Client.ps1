[CmdletBinding()]
param(
    [string]$ClientDirectory = 'D:\Program Files\Game\Mir2',
    [string]$DistributionDirectory = 'E:\rxdev\webhost\mir2',
    [string]$PackageName = 'Crystal-Mir2-Client-win-x64-20261030.zip'
)
$ErrorActionPreference = 'Stop'
$client = [IO.Path]::GetFullPath($ClientDirectory)
if ((Split-Path $client -Leaf) -ne 'Mir2') { throw 'Expected a client directory named Mir2.' }
$workspace = Split-Path $PSScriptRoot -Parent
$download = Get-Content -LiteralPath (Join-Path $workspace 'docs\client-resource-download.json') -Raw | ConvertFrom-Json
if ($download.failures -ne 0 -or $download.completed -ne $download.files) { throw 'Resource download is incomplete.' }
if ($PackageName -ne [IO.Path]::GetFileName($PackageName) -or !$PackageName.EndsWith('.zip')) { throw 'Expected a ZIP file name.' }
$final = Join-Path $DistributionDirectory $PackageName
if (Test-Path -LiteralPath $final) { throw 'Distribution package exists; it was preserved.' }
$scratch = Join-Path $env:TEMP ('ai\20261030\mir2-crystal\package-' + [Guid]::NewGuid().ToString('N'))
$seed = Join-Path $scratch 'seed\Mir2'
New-Item -ItemType Directory -Path $seed -Force | Out-Null
$archive = Join-Path $scratch $PackageName
$config = @'
[Graphics]
FullScreen=False
Borderless=False
AlwaysOnTop=False
MouseClip=False
Resolution=1024
FPSCap=True

[Network]
UseConfig=True
IPAddress=127.0.0.1
Port=7000

[Game]
Language=Chinese
FontName=Microsoft YaHei

[Launcher]
Enabled=False
Host=https://mirfiles.com/resources/mir2/crystal/patch/
'@
foreach ($name in @('Mir2Config.ini','Mir2Test.ini')) {
    [IO.File]::WriteAllText((Join-Path $seed $name),$config,[Text.UTF8Encoding]::new($false))
}
@'
Crystal Mir2 Windows x64 client / 2026-10-30

Extract the entire Mir2 directory, then run Start-Client.cmd.
This package includes the .NET 8 runtime and application-local VC++/DirectX dependencies.
Default server: 127.0.0.1:7000 (same PC).
For another PC or Android Wine environment, set Network/IPAddress in Mir2Config.ini
to your server's reachable address. The server must also listen on that address.
Android/Winlator gameplay and touch controls have not been validated.
This package contains no server account database or preconfigured account.
Game resources: Crystal community guide / MirFiles; content is a baseline,
not a completed pure-1.76 ruleset. Extra classes and new heroes are disabled server-side.
'@ | Set-Content -LiteralPath (Join-Path $seed 'README.txt') -Encoding utf8
Push-Location (Split-Path $client -Parent)
try {
    & 7z a -tzip $archive Mir2 -mx=1 -mmt=4 -bsp0 '-x!Mir2\Mir2Config.ini' '-x!Mir2\Mir2Test.ini' '-x!Mir2\Error.txt' '-x!Mir2\KeyBinds.ini' '-x!Mir2\ChatLogs' '-x!Mir2\Screenshots' '-x!Mir2\Data\UserData'
    if ($LASTEXITCODE -ne 0) { throw 'Client ZIP creation failed.' }
} finally { Pop-Location }
Push-Location (Split-Path $seed -Parent)
try {
    & 7z a -tzip $archive Mir2 -mx=1 -mmt=4 -bsp0
    if ($LASTEXITCODE -ne 0) { throw 'Clean default configuration insertion failed.' }
} finally { Pop-Location }
& 7z t $archive -bsp0
if ($LASTEXITCODE -ne 0) { throw 'ZIP integrity verification failed.' }
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
New-Item -ItemType Directory -Path $DistributionDirectory -Force | Out-Null
Copy-Item -LiteralPath $archive -Destination $final
if ((Get-FileHash -LiteralPath $final -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) { throw 'Distribution copy checksum mismatch.' }
if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($archive)) -ne [IO.Path]::GetFullPath($scratch)) { throw 'Unexpected temporary archive path; it was preserved.' }
Remove-Item -LiteralPath $archive
[IO.File]::WriteAllText(($final + '.sha256'),($hash + '  ' + $PackageName + "`n"),[Text.UTF8Encoding]::new($false))
$report = [pscustomobject]@{
    Path = $final
    Bytes = (Get-Item -LiteralPath $final).Length
    SHA256 = $hash
    ZipIntegrity = 'PASS'
    DistributionCopyChecksum = 'PASS'
    IncludesServerData = $false
    IncludesUserSettingsOrLogs = $false
    DefaultEndpoint = '127.0.0.1:7000'
}
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $workspace 'docs\client-package.json') -Encoding utf8
$report | ConvertTo-Json
