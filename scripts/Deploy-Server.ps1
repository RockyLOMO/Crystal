[CmdletBinding()]
param(
    [string]$TargetDirectory = 'D:\private-server\mir2\Server',
    [string]$SourceDirectory = '',
    [switch]$SkipBuild,
    [switch]$SyncLocalizationOnly,
    [switch]$SyncClientVersion,
    [switch]$BackupBinaries = $true,
    [switch]$BackupData,
    [switch]$Restart,
    [switch]$ForceStop
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($SourceDirectory)) {
    $SourceDirectory = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
} else {
    $SourceDirectory = [IO.Path]::GetFullPath($SourceDirectory)
}
$TargetDirectory = [IO.Path]::GetFullPath($TargetDirectory)

if (-not (Test-Path -LiteralPath $TargetDirectory)) {
    throw "Target server directory does not exist: $TargetDirectory"
}

$serverExe = Join-Path $TargetDirectory 'Server.exe'
$manageScript = Join-Path $SourceDirectory 'scripts\Manage-Mir2.ps1'
$runtimeRoot = Split-Path $TargetDirectory -Parent

# 1. 检查服务器是否正在运行
$serverProcess = @(Get-CimInstance Win32_Process -Filter "Name='Server.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -eq $serverExe })

if ($serverProcess.Count -gt 0) {
    if ($Restart -or $ForceStop) {
        if (-not (Test-Path -LiteralPath $manageScript)) {
            throw "未找到管理脚本 $manageScript，拒绝强制终止运行中的服务端进程 (PID: $($serverProcess[0].ProcessId))。"
        }
        Write-Host "检测到服务端正在运行 (PID: $($serverProcess[0].ProcessId))，正在优雅停服..." -ForegroundColor Yellow
        & pwsh -NoLogo -NoProfile -File $manageScript -Action Stop -RuntimeRoot $runtimeRoot
        if ($LASTEXITCODE -ne 0) {
            throw "优雅停服失败，退出码: $LASTEXITCODE。已终止部署以保护存档数据。"
        }
        Start-Sleep -Seconds 2
        $remaining = @(Get-CimInstance Win32_Process -Filter "Name='Server.exe'" -ErrorAction SilentlyContinue |
            Where-Object { $_.ExecutablePath -eq $serverExe })
        if ($remaining.Count -gt 0) {
            throw "服务端未能正常停止，进程仍存在 (PID: $($remaining[0].ProcessId))。"
        }
        Write-Host "服务端已正常安全停止。" -ForegroundColor Green
    } else {
        throw "服务端当前正在运行 (PID: $($serverProcess[0].ProcessId))。请先停止服务，或添加参数 -Restart / -ForceStop。"
    }
}

# 2. 可选：全量运行数据/数据库备份
if ($BackupData) {
    if (-not (Test-Path -LiteralPath $manageScript)) {
        throw "未找到管理脚本 $manageScript，无法执行数据一致性备份，已终止部署。"
    }
    Write-Host "正在执行服务端完整数据一致性备份..." -ForegroundColor Cyan
    & pwsh -NoLogo -NoProfile -File $manageScript -Action Backup -RuntimeRoot $runtimeRoot
    if ($LASTEXITCODE -ne 0) {
        throw "服务端数据一致性备份失败，退出码: $LASTEXITCODE。已终止部署。"
    }
}

# 3. 自动备份目标目录中的旧二进制程序
if ($BackupBinaries) {
    $backupDir = Join-Path $TargetDirectory ('Back Up\binaries-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    $criticalFiles = @('Server.dll', 'Server.Library.dll', 'Shared.dll', 'Server.exe') |
        Where-Object { Test-Path (Join-Path $TargetDirectory $_) }
    if ($criticalFiles.Count -gt 0) {
        New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
        foreach ($file in $criticalFiles) {
            Copy-Item -LiteralPath (Join-Path $TargetDirectory $file) -Destination (Join-Path $backupDir $file) -Force
        }
        Write-Host "旧二进制程序已自动归档备份至: $backupDir" -ForegroundColor DarkGray
    }
}

$stagingDir = $null
try {
    # 4. 编译服务端源码
    if (-not $SkipBuild -and -not $SyncLocalizationOnly) {
        $stagingDir = Join-Path $env:TEMP ("mir2-deploy-" + [Guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null
        
        Write-Host "正在从 $SourceDirectory 编译服务端 (Release win-x64) ..." -ForegroundColor Cyan
        $projectPath = Join-Path $SourceDirectory 'Server.MirForms\Server.csproj'
        if (-not (Test-Path -LiteralPath $projectPath)) {
            throw "未找到服务端项目工程文件: $projectPath"
        }
        
        & dotnet publish $projectPath -c Release -r win-x64 --self-contained false -o $stagingDir -p:PublishSingleFile=false -v minimal
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish 编译失败，退出码: $LASTEXITCODE"
        }
        Write-Host "编译成功，开始同步二进制文件..." -ForegroundColor Green
        
        $binaryExtensions = @('.dll', '.exe', '.pdb', '.json', '.config')
        $sourceFiles = Get-ChildItem -LiteralPath $stagingDir -File |
            Where-Object { $binaryExtensions -contains $_.Extension }
            
        foreach ($file in $sourceFiles) {
            Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $TargetDirectory $file.Name) -Force
        }
        Write-Host "已同步 $($sourceFiles.Count) 个二进制/依赖文件到 $TargetDirectory" -ForegroundColor Green
    }

    # 5. 同步多语言字典文件
    $sourceLocDir = Join-Path $SourceDirectory 'Localization'
    $targetLocDir = Join-Path $TargetDirectory 'Localization'
    New-Item -ItemType Directory -Path $targetLocDir -Force | Out-Null
    
    if (Test-Path -LiteralPath $sourceLocDir) {
        $locFiles = Get-ChildItem -LiteralPath $sourceLocDir -Filter '*.json' -File
        foreach ($f in $locFiles) {
            Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $targetLocDir $f.Name) -Force
        }
        Write-Host "已同步 $($locFiles.Count) 个显示名称/叙事字典到 $targetLocDir" -ForegroundColor Green
    }
    
    $serverLocDir = Join-Path $SourceDirectory 'Server.MirForms\Localization'
    if (Test-Path -LiteralPath $serverLocDir) {
        $serverLocFiles = Get-ChildItem -LiteralPath $serverLocDir -Filter '*.json' -File
        foreach ($f in $serverLocFiles) {
            Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $targetLocDir $f.Name) -Force
        }
        Write-Host "已同步 $($serverLocFiles.Count) 个服务端界面语言文件到 $targetLocDir" -ForegroundColor Green
    }

    # 6. 同步客户端版本验证校验文件 (ClientVersion)
    if ($SyncClientVersion) {
        $clientCandidates = @(
            'D:\private-server\mir2-client\Client.exe',
            (Join-Path $SourceDirectory 'Build\Client\Release\Client.exe')
        )
        $clientSource = $clientCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if ($clientSource) {
            $clientVerDir = Join-Path $TargetDirectory 'ClientVersion'
            New-Item -ItemType Directory -Path $clientVerDir -Force | Out-Null
            Copy-Item -LiteralPath $clientSource -Destination (Join-Path $clientVerDir 'Client.exe') -Force
            $clientDll = [IO.Path]::ChangeExtension($clientSource, '.dll')
            if (Test-Path -LiteralPath $clientDll) {
                Copy-Item -LiteralPath $clientDll -Destination (Join-Path $clientVerDir 'Client.dll') -Force
            }
            Write-Host "已同步 ClientVersion 校验文件: $clientSource" -ForegroundColor Green
        }
    }
}
finally {
    if ($stagingDir -and (Test-Path -LiteralPath $stagingDir)) {
        Remove-Item -LiteralPath $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# 7. 部署状态与校验摘要
Write-Host "`n=== 部署完成校验 ===" -ForegroundColor Cyan
$deployedBinaries = @('Server.dll', 'Server.Library.dll', 'Shared.dll', 'Server.exe') | ForEach-Object {
    $p = Join-Path $TargetDirectory $_
    if (Test-Path -LiteralPath $p) {
        $item = Get-Item -LiteralPath $p
        $hash = (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash
        [PSCustomObject]@{
            File = $_
            Size = $item.Length
            LastModified = $item.LastWriteTime
            SHA256 = $hash.Substring(0, 16) + '...'
        }
    }
}
$deployedBinaries | Format-Table -AutoSize

# 8. 如果指定了 -Restart，重启服务端
if ($Restart) {
    Write-Host "正在重新启动服务端..." -ForegroundColor Cyan
    if (Test-Path -LiteralPath $manageScript) {
        & pwsh -NoLogo -NoProfile -File $manageScript -Action Start -RuntimeRoot $runtimeRoot
        Start-Sleep -Seconds 2
        & pwsh -NoLogo -NoProfile -File $manageScript -Action Status -RuntimeRoot $runtimeRoot
    } else {
        Start-Process -FilePath $serverExe -WorkingDirectory $TargetDirectory
    }
}

Write-Host "服务端部署完成！" -ForegroundColor Green
