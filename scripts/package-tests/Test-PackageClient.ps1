[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$packageScript = Join-Path $workspace 'scripts\Package-Client.ps1'
$scratch = Join-Path $env:TEMP ('ai\20261031\mir2-package-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch -Force | Out-Null
$script:passed = 0
function Assert-True([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
    $script:passed++
    Write-Host "PASS $script:passed`: $Message"
}
function Write-Fixture([string]$Directory, [string]$RelativePath, [string]$Value) {
    $path = Join-Path $Directory $RelativePath
    New-Item -ItemType Directory -Path (Split-Path $path -Parent) -Force | Out-Null
    [IO.File]::WriteAllText($path, $Value)
}
function Assert-Rejected([scriptblock]$Action, [string]$Expected, [string]$Message) {
    $failure = $null
    try { & $Action | Out-Null } catch { $failure = $_.Exception.Message }
    Assert-True ($null -ne $failure -and $failure.Contains($Expected)) $Message
}

foreach ($leaf in @('mir2-client', '自定义 客户端', 'Mir2', '-client')) {
    $caseRoot = Join-Path $scratch ([Guid]::NewGuid().ToString('N'))
    $client = Join-Path $caseRoot $leaf
    $distribution = Join-Path $caseRoot 'distribution'
    $reportPath = Join-Path $caseRoot 'report.json'
    foreach ($file in @('Client.exe', 'Client.dll', 'Start-Client.cmd', 'Data\nested\asset.Lib', 'Map\map.map', 'Localization\Chinese.json')) {
        Write-Fixture $client $file "fixture: $file"
    }
    foreach ($private in @('Mir2Config.ini', 'Mir2Test.ini', 'Error.txt', 'KeyBinds.ini', 'ChatLogs\chat.txt', 'Screenshots\shot.jpg', 'Data\UserData\user.ini')) {
        Write-Fixture $client $private 'SECRET_DO_NOT_PACKAGE'
    }
    $originalConfigHash = (Get-FileHash -LiteralPath (Join-Path $client 'Mir2Config.ini')).Hash
    $invokeArguments = @{
        ClientDirectory = $client + [IO.Path]::DirectorySeparatorChar
        DistributionDirectory = $distribution
        PackageName = 'fixture.zip'
        ReportPath = $reportPath
    }
    # Also exercise PowerShell-relative paths independently of the process CWD.
    $buildArguments = $invokeArguments.Clone()
    if ($leaf -eq '-client') {
        $buildArguments.ClientDirectory = '.\-client\'
        $buildArguments.DistributionDirectory = '.\distribution'
    }
    Push-Location $caseRoot
    try { & $packageScript @buildArguments *> (Join-Path $caseRoot 'package.log') }
    finally { Pop-Location }
    $zip = Join-Path $distribution 'fixture.zip'
    $unpacked = Join-Path $caseRoot 'unpacked'
    [IO.Compression.ZipFile]::ExtractToDirectory($zip, $unpacked)
    $top = @(Get-ChildItem -LiteralPath $unpacked)
    Assert-True ($top.Count -eq 1 -and $top[0].Name -ceq 'Mir2') "$leaf produces exactly the Mir2 archive root"
    $packagedClient = Join-Path $unpacked 'Mir2'
    Assert-True ((Get-Content -LiteralPath (Join-Path $packagedClient 'Data\nested\asset.Lib') -Raw) -eq 'fixture: Data\nested\asset.Lib') "$leaf preserves nested resource contents"
    $contentFiles = @(Get-ChildItem -LiteralPath $packagedClient -Recurse -File)
    $leaks = @($contentFiles | Where-Object { [IO.File]::ReadAllText($_.FullName).Contains('SECRET_DO_NOT_PACKAGE') })
    Assert-True ($leaks.Count -eq 0) "$leaf excludes all personal configuration and data"
    foreach ($ini in @('Mir2Config.ini', 'Mir2Test.ini')) {
        $configuration = Get-Content -LiteralPath (Join-Path $packagedClient $ini) -Raw
        Assert-True ($configuration.Contains('UseConfig=True') -and $configuration.Contains('IPAddress=s.f-li.cn') -and $configuration.Contains('Port=7000')) "$leaf inserts clean $ini"
        Assert-True ($configuration.Contains('Resolution=1366') -and $configuration.Contains('FullScreen=False') -and $configuration.Contains('FontName=Microsoft YaHei')) "$leaf inserts widescreen Chinese defaults in $ini"
    }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-True ($report.ZipIntegrity -eq 'PASS' -and $report.SHA256 -eq $hash -and (Get-Content -LiteralPath ($zip + '.sha256') -Raw).StartsWith($hash)) "$leaf passes CRC and distribution SHA checks"
    Assert-True ((Get-FileHash -LiteralPath (Join-Path $client 'Mir2Config.ini')).Hash -eq $originalConfigHash) "$leaf leaves the source configuration untouched"
    Assert-Rejected { & $packageScript @invokeArguments } 'Distribution package exists' "$leaf preserves an existing distribution package"
    Assert-True ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant() -eq $hash) "$leaf existing archive remains unchanged"
}

$invalid = Join-Path $scratch 'invalid-client'
Write-Fixture $invalid 'Client.exe' 'client'
$invalidArguments = @{
    ClientDirectory = $invalid
    DistributionDirectory = (Join-Path $scratch 'rejected-distribution')
    PackageName = 'rejected.zip'
    ReportPath = (Join-Path $scratch 'rejected-report.json')
}
Assert-Rejected { & $packageScript @invalidArguments } 'Client.dll' 'An incomplete client directory is rejected'
Write-Fixture $invalid 'Client.dll' 'client library'
Write-Fixture $invalid 'Start-Client.cmd' 'launcher'
Write-Fixture $invalid 'Server.MirADB' 'server account data'
Assert-Rejected { & $packageScript @invalidArguments } 'Server data found' 'A directory containing server account data is rejected'
Assert-True (!(Test-Path -LiteralPath $invalidArguments.DistributionDirectory)) 'Rejected inputs create no distribution directory'
Write-Host "All $script:passed package checks passed. TEMP evidence: $scratch"
