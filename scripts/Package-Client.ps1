[CmdletBinding()]
param(
    [string]$ClientDirectory = 'D:\private-server\mir2-client',
    [string]$DistributionDirectory = 'E:\rxdev\webhost\mir2',
    [string]$PackageName = 'Crystal-Mir2-Client-win-x64-20261030.zip',
    [string]$ReportPath
)
$ErrorActionPreference = 'Stop'
$client = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ClientDirectory)
if (!(Test-Path -LiteralPath $client -PathType Container)) { throw 'Client directory does not exist.' }
$client = (Get-Item -LiteralPath $client).FullName.TrimEnd([IO.Path]::DirectorySeparatorChar)
$clientLeaf = Split-Path $client -Leaf
foreach ($required in @('Client.exe', 'Client.dll', 'Start-Client.cmd')) {
    if (!(Test-Path -LiteralPath (Join-Path $client $required) -PathType Leaf)) {
        throw "Expected a client directory containing $required."
    }
}
foreach ($serverFile in @('Server.exe', 'Server.Library.dll', 'Server.MirDB', 'Server.MirADB')) {
    if (Test-Path -LiteralPath (Join-Path $client $serverFile)) { throw "Server data found in client directory: $serverFile." }
}
$workspace = Split-Path $PSScriptRoot -Parent
if (!$ReportPath) { $ReportPath = Join-Path $workspace 'docs\client-package.json' }
$download = Get-Content -LiteralPath (Join-Path $workspace 'docs\client-resource-download.json') -Raw | ConvertFrom-Json
if ($download.failures -ne 0 -or $download.completed -ne $download.files) { throw 'Resource download is incomplete.' }
if ($PackageName -ne [IO.Path]::GetFileName($PackageName) -or !$PackageName.EndsWith('.zip')) { throw 'Expected a ZIP file name.' }
$distribution = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($DistributionDirectory)
$final = Join-Path $distribution $PackageName
if (Test-Path -LiteralPath $final) { throw 'Distribution package exists; it was preserved.' }
$scratch = Join-Path $env:TEMP ('ai\' + (Get-Date -Format yyyyMMdd) + '\mir2-crystal\package-' + [Guid]::NewGuid().ToString('N'))
$seed = Join-Path $scratch 'seed\Mir2'
New-Item -ItemType Directory -Path $seed -Force | Out-Null
$archive = Join-Path $scratch $PackageName
$config = @'
[Graphics]
FullScreen=False
Borderless=False
AlwaysOnTop=False
MouseClip=False
Resolution=1366
FPSCap=True

[Network]
UseConfig=True
IPAddress=s.f-li.cn
Port=7000

[Game]
Language=Chinese
FontName=Microsoft YaHei
AutoSellNormalEquipment=True
AutoSpecialRepairEquipment=True
SmartShift=True

[Launcher]
Enabled=False
Host=https://mirfiles.com/resources/mir2/crystal/patch/
'@
foreach ($name in @('Mir2Config.ini','Mir2Test.ini')) {
    [IO.File]::WriteAllText((Join-Path $seed $name),$config,[Text.UTF8Encoding]::new($false))
}
@'
Crystal Mir2 Windows x64 中文客户端

完整解压 Mir2 文件夹后，运行 Start-Client.cmd。
已包含 .NET 8 运行库及本地 VC++ / DirectX 依赖。
默认连接 s.f-li.cn:7000；中文字体为 Microsoft YaHei。
游戏视口固定 1366x768，默认窗口模式；登录和选人保留原生布局。
点击商人自动出售背包普通装备；仅在 NPC 提供特殊修理时自动维修身上装备。
可在 INI 的 Game 中将 AutoSellNormalEquipment / AutoSpecialRepairEquipment 设为 False 关闭。
更换服务端时修改 Mir2Config.ini 的 Network/IPAddress 和 Port。
服务端也需要监听相应地址。安卓 Wine / Winlator 实际游戏和触控尚未验证。
此包不包含服务端账号数据库或预置账号。
游戏资源来源：Crystal 社区指南 / MirFiles；经典译名向盛大 1.76 靠拢。
具体职业、英雄及玩法由服务端配置决定，当前内容包含后续版本扩展。
'@ | Set-Content -LiteralPath (Join-Path $seed 'README.txt') -Encoding utf8
Push-Location (Split-Path $client -Parent)
try {
    $exclude = @('Mir2Config.ini', 'Mir2Test.ini', 'Error.txt', 'KeyBinds.ini', 'ChatLogs', 'Screenshots', 'Data\UserData') |
        ForEach-Object { '-x!' + $clientLeaf + '\' + $_ }
    & 7z a -tzip $archive -mx=1 -mmt=4 -bsp0 @exclude -- $clientLeaf
    if ($LASTEXITCODE -ne 0) { throw 'Client ZIP creation failed.' }
} finally { Pop-Location }
if ($clientLeaf -cne 'Mir2') {
    & 7z rn $archive -bsp0 -- $clientLeaf Mir2
    if ($LASTEXITCODE -ne 0) { throw 'Client ZIP root normalization failed.' }
}
Push-Location (Split-Path $seed -Parent)
try {
    & 7z a -tzip $archive Mir2 -mx=1 -mmt=4 -bsp0
    if ($LASTEXITCODE -ne 0) { throw 'Clean default configuration insertion failed.' }
} finally { Pop-Location }
& 7z t $archive -bsp0
if ($LASTEXITCODE -ne 0) { throw 'ZIP integrity verification failed.' }
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
New-Item -ItemType Directory -Path $distribution -Force | Out-Null
[IO.File]::Copy($archive, [IO.Path]::GetFullPath($final), $false)
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
    DefaultEndpoint = 's.f-li.cn:7000'
}
$report | ConvertTo-Json | Set-Content -LiteralPath $ReportPath -Encoding utf8
$report | ConvertTo-Json
