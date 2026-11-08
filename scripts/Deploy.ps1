<#
.SYNOPSIS
    Mir2 双端一键部署脚本 (Server / Client)

.DESCRIPTION
    入参规则：
      1        : 仅部署 Server
      2        : 仅部署 Client
      1|2 或 3 : 同时部署 Server 和 Client (默认值 3)

    进程检查：
      部署前自动检查 Server / Client 运行中进程；
      优先发送 WM_CLOSE 执行非强制优雅退出；
      若超过 10 秒仍未退出，则执行强制终止 (/F)。
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Target = "3",

    [string]$SourceDirectory = "D:\projs\mir2",
    [string]$ServerTargetDirectory = "D:\private-server\mir2\Server",
    [string]$ClientTargetDirectory = "D:\private-server\mir2-client"
)

$ErrorActionPreference = 'Stop'

# 1. 解析入参
$targetClean = ($Target -replace '["'']', '').Trim()

$deployServer = $false
$deployClient = $false

switch -Regex ($targetClean) {
    '^1$' {
        $deployServer = $true
        $deployClient = $false
    }
    '^2$' {
        $deployServer = $false
        $deployClient = $true
    }
    '^(3|1\|2|1,2|1;2|all|both)?$' {
        $deployServer = $true
        $deployClient = $true
    }
    default {
        Write-Host "错误: 无效入参 '$Target'。" -ForegroundColor Red
        Write-Host "用法:" -ForegroundColor Yellow
        Write-Host "  .\Deploy.ps1 1        # 仅部署 Server"
        Write-Host "  .\Deploy.ps1 2        # 仅部署 Client"
        Write-Host "  .\Deploy.ps1 '1|2'    # 同时部署 Server 与 Client"
        Write-Host "  .\Deploy.ps1 3        # 同时部署 Server 与 Client (默认)"
        exit 1
    }
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   Mir2 一键部署脚本" -ForegroundColor Cyan
Write-Host "   目标: $(if ($deployServer -and $deployClient) { 'Server + Client (双端)' } elseif ($deployServer) { 'Server (服务端)' } else { 'Client (客户端)' })" -ForegroundColor Cyan
Write-Host "   源码: $SourceDirectory" -ForegroundColor DarkGray
Write-Host "   时间: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor DarkGray
Write-Host "==========================================" -ForegroundColor Cyan

# 2. 进程优雅关闭与超时强杀函数 (最长等待 10 秒)
function Stop-MirProcess {
    param(
        [string]$ProcessName,
        [int]$TimeoutSeconds = 10
    )

    $procs = @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue)
    if ($procs.Count -eq 0) {
        Write-Host "[$ProcessName] 未检测到运行中进程。" -ForegroundColor DarkGray
        return
    }

    foreach ($proc in $procs) {
        $pidNum = $proc.Id
        Write-Host "[$ProcessName] 检测到运行中进程 (PID: $pidNum)，正在尝试非强制优雅关闭..." -ForegroundColor Yellow

        # 1. 尝试发送非强制窗口关闭信号 (WM_CLOSE)
        try {
            $null = $proc.CloseMainWindow()
        } catch { }

        # 通过 taskkill (无 /F) 请求正常关闭
        try {
            $null = & taskkill.exe /PID $pidNum 2>&1
        } catch { }

        # 若为 Server 且脚本存在，额外调用 Manage-Mir2.ps1 -Action Stop 跨桌面投递 WM_CLOSE
        if ($ProcessName -match 'Server' -and (Test-Path -LiteralPath (Join-Path $SourceDirectory 'scripts\Manage-Mir2.ps1'))) {
            try {
                $manageScript = Join-Path $SourceDirectory 'scripts\Manage-Mir2.ps1'
                $runtimeRoot = Split-Path $ServerTargetDirectory -Parent
                & pwsh -NoLogo -NoProfile -File $manageScript -Action Stop -RuntimeRoot $runtimeRoot 2>&1 | Out-Null
            } catch { }
        }

        # 2. 等待最多 10 秒
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $exited = $false
        while ($sw.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
            Start-Sleep -Milliseconds 500
            $check = Get-Process -Id $pidNum -ErrorAction SilentlyContinue
            if (-not $check -or $check.HasExited) {
                $exited = $true
                break
            }
        }

        # 3. 超过 10 秒仍未退出，执行强制关闭 (/F)
        if ($exited) {
            Write-Host "[$ProcessName] 进程 (PID: $pidNum) 已优雅退出 (耗时 $($sw.Elapsed.TotalSeconds.ToString('F1')) 秒)。" -ForegroundColor Green
        } else {
            Write-Host "[$ProcessName] 进程 (PID: $pidNum) 超过 $TimeoutSeconds 秒未退出，执行强制关闭 (/F)..." -ForegroundColor Red
            try {
                Stop-Process -Id $pidNum -Force -ErrorAction SilentlyContinue
            } catch { }
            try {
                $null = & taskkill.exe /F /PID $pidNum 2>&1
            } catch { }
            Start-Sleep -Seconds 1
            $check = Get-Process -Id $pidNum -ErrorAction SilentlyContinue
            if ($check -and -not $check.HasExited) {
                throw "[$ProcessName] 强制终止进程 (PID: $pidNum) 失败！"
            }
            Write-Host "[$ProcessName] 进程 (PID: $pidNum) 已强制终止。" -ForegroundColor Yellow
        }
    }
}

# 3. 部署前检查并停止目标进程
Write-Host "`n>>> 1. 检查并关闭运行中进程 (10s 优雅超时自动强杀)..." -ForegroundColor Cyan
if ($deployServer) {
    Stop-MirProcess -ProcessName "Server" -TimeoutSeconds 10
}
if ($deployClient) {
    Stop-MirProcess -ProcessName "Client" -TimeoutSeconds 10
}

# 4. 部署 Server
if ($deployServer) {
    Write-Host "`n>>> 2. 编译并部署 Server..." -ForegroundColor Cyan
    if (-not (Test-Path -LiteralPath $ServerTargetDirectory)) {
        New-Item -ItemType Directory -Path $ServerTargetDirectory -Force | Out-Null
    }

    $serverStaging = Join-Path $env:TEMP ("mir2-server-deploy-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $serverStaging -Force | Out-Null

    try {
        $serverProj = Join-Path $SourceDirectory 'Server.MirForms\Server.csproj'
        if (-not (Test-Path -LiteralPath $serverProj)) {
            throw "未找到服务端项目工程: $serverProj"
        }

        Write-Host "正在编译 Server (Release win-x64) ..." -ForegroundColor DarkGray
        & dotnet publish $serverProj -c Release -r win-x64 --self-contained false -o $serverStaging -p:PublishSingleFile=false -v minimal
        if ($LASTEXITCODE -ne 0) { throw "Server 编译失败 (退出码 $LASTEXITCODE)" }

        # 同步二进制文件
        $binaryExtensions = @('.dll', '.exe', '.pdb', '.json', '.config')
        $sourceFiles = Get-ChildItem -LiteralPath $serverStaging -File | Where-Object { $binaryExtensions -contains $_.Extension }
        foreach ($f in $sourceFiles) {
            Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $ServerTargetDirectory $f.Name) -Force
        }
        Write-Host "已同步 $($sourceFiles.Count) 个 Server 二进制及依赖文件至 $ServerTargetDirectory" -ForegroundColor Green

        # 同步多语言字典
        $tgtLoc = Join-Path $ServerTargetDirectory 'Localization'
        New-Item -ItemType Directory -Path $tgtLoc -Force | Out-Null
        $srcLoc = Join-Path $SourceDirectory 'Localization'
        if (Test-Path -LiteralPath $srcLoc) {
            Get-ChildItem -LiteralPath $srcLoc -Filter '*.json' -File | ForEach-Object {
                Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $tgtLoc $_.Name) -Force
            }
        }
        $srcServerLoc = Join-Path $SourceDirectory 'Server.MirForms\Localization'
        if (Test-Path -LiteralPath $srcServerLoc) {
            Get-ChildItem -LiteralPath $srcServerLoc -Filter '*.json' -File | ForEach-Object {
                Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $tgtLoc $_.Name) -Force
            }
        }
        Write-Host "已同步 Server 多语言字典文件。" -ForegroundColor Green

        # 若不部署 Client，同步现有 Client.exe/Client.dll 到 Server/ClientVersion
        if (-not $deployClient) {
            $clientVerDir = Join-Path $ServerTargetDirectory 'ClientVersion'
            New-Item -ItemType Directory -Path $clientVerDir -Force | Out-Null
            $clientExe = Join-Path $ClientTargetDirectory 'Client.exe'
            $clientDll = Join-Path $ClientTargetDirectory 'Client.dll'
            if (Test-Path -LiteralPath $clientExe) {
                Copy-Item -LiteralPath $clientExe -Destination (Join-Path $clientVerDir 'Client.exe') -Force
            }
            if (Test-Path -LiteralPath $clientDll) {
                Copy-Item -LiteralPath $clientDll -Destination (Join-Path $clientVerDir 'Client.dll') -Force
            }
            Write-Host "已同步 ClientVersion 校验文件。" -ForegroundColor Green
        }
    }
    finally {
        if (Test-Path -LiteralPath $serverStaging) {
            Remove-Item -LiteralPath $serverStaging -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

# 5. 部署 Client
if ($deployClient) {
    Write-Host "`n>>> 3. 编译并部署 Client..." -ForegroundColor Cyan
    if (-not (Test-Path -LiteralPath $ClientTargetDirectory)) {
        New-Item -ItemType Directory -Path $ClientTargetDirectory -Force | Out-Null
    }

    $clientStaging = Join-Path $env:TEMP ("mir2-client-deploy-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $clientStaging -Force | Out-Null

    try {
        $clientProj = Join-Path $SourceDirectory 'Client\Client.csproj'
        if (-not (Test-Path -LiteralPath $clientProj)) {
            throw "未找到客户端项目工程: $clientProj"
        }

        Write-Host "正在编译 Client (Release win-x64, self-contained) ..." -ForegroundColor DarkGray
        & dotnet publish $clientProj -c Release -r win-x64 --self-contained true -o $clientStaging -p:PublishSingleFile=false -v minimal
        if ($LASTEXITCODE -ne 0) { throw "Client 编译失败 (退出码 $LASTEXITCODE)" }

        # 同步客户端程序与运行库（保留已有 INI 配置文件）
        $preserveConfigs = @('Mir2Config.ini', 'Mir2Test.ini', 'KeyBinds.ini')
        $clientFiles = Get-ChildItem -LiteralPath $clientStaging -Recurse -File
        $syncedCount = 0

        foreach ($f in $clientFiles) {
            $relPath = [IO.Path]::GetRelativePath($clientStaging, $f.FullName)
            $destPath = Join-Path $ClientTargetDirectory $relPath

            if ($preserveConfigs -contains $f.Name -and (Test-Path -LiteralPath $destPath)) {
                continue # 保留玩家已有的配置文件
            }

            $destDir = Split-Path $destPath -Parent
            if (-not (Test-Path -LiteralPath $destDir)) {
                New-Item -ItemType Directory -Path $destDir -Force | Out-Null
            }

            Copy-Item -LiteralPath $f.FullName -Destination $destPath -Force
            $syncedCount++
        }
        Write-Host "已同步 $syncedCount 个 Client 文件至 $ClientTargetDirectory (保留现有配置文件)" -ForegroundColor Green

        # 同步客户端版本验证校验文件 (Server/ClientVersion)
        $clientVerDir = Join-Path $ServerTargetDirectory 'ClientVersion'
        New-Item -ItemType Directory -Path $clientVerDir -Force | Out-Null
        $clientExePath = Join-Path $ClientTargetDirectory 'Client.exe'
        $clientDllPath = Join-Path $ClientTargetDirectory 'Client.dll'
        if (Test-Path -LiteralPath $clientExePath) {
            Copy-Item -LiteralPath $clientExePath -Destination (Join-Path $clientVerDir 'Client.exe') -Force
        }
        if (Test-Path -LiteralPath $clientDllPath) {
            Copy-Item -LiteralPath $clientDllPath -Destination (Join-Path $clientVerDir 'Client.dll') -Force
        }
        Write-Host "已同步 ClientVersion 握手校验文件至 Server。" -ForegroundColor Green
    }
    finally {
        if (Test-Path -LiteralPath $clientStaging) {
            Remove-Item -LiteralPath $clientStaging -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Write-Host "`n==========================================" -ForegroundColor Green
Write-Host "   部署完成！" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
