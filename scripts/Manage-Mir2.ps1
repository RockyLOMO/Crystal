[CmdletBinding()]
param(
    [ValidateSet('Start','Stop','Status','Backup')][string]$Action = 'Status',
    [string]$RuntimeRoot = 'D:\private-server\mir2',
    [switch]$ShowWindow
)
$ErrorActionPreference = 'Stop'
$RuntimeRoot = [IO.Path]::GetFullPath($RuntimeRoot)
$serverDirectory = Join-Path $RuntimeRoot 'Server'
$serverExe = Join-Path $serverDirectory 'Server.exe'
$serverProcess = @(Get-CimInstance Win32_Process -Filter "Name='Server.exe'" |
    Where-Object { $_.ExecutablePath -eq $serverExe })
if ($serverProcess.Count -gt 1) { throw 'Multiple server processes found; inspect before continuing.' }

function Stop-Crystal {
    if (!$serverProcess) { return }
    # The GUI may run either on the current desktop or on the interactive Default desktop.
    # Send WM_CLOSE only to the exact executable's top-level Windows Forms window.
    if (!('Mir2WindowLifecycle' -as [type])) {
        Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
public static class Mir2WindowLifecycle {
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);
    [DllImport("user32.dll")] static extern bool SetThreadDesktop(IntPtr hDesktop);
    [DllImport("user32.dll")] static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumWindowsProc lpfn, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr hDesktop);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder name, int count);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wparam, IntPtr lparam);
    public static int Close(uint processId) {
        int sent = 0;
        EnumWindowsProc cb = (hwnd, p) => {
            uint owner; GetWindowThreadProcessId(hwnd, out owner);
            if (owner != processId) return true;
            var name = new StringBuilder(256); GetClassName(hwnd, name, name.Capacity);
            if (name.ToString().StartsWith("WindowsForms10.Window") && PostMessage(hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero)) sent++;
            return true;
        };
        EnumWindows(cb, IntPtr.Zero);
        if (sent == 0) {
            var worker = new Thread(() => {
                IntPtr hDesk = OpenDesktop("Default", 0, false, 0x01FF);
                if (hDesk != IntPtr.Zero) {
                    try {
                        SetThreadDesktop(hDesk);
                        EnumDesktopWindows(hDesk, cb, IntPtr.Zero);
                    } finally {
                        CloseDesktop(hDesk);
                    }
                }
            });
            worker.Start();
            worker.Join(5000);
        }
        return sent;
    }
}
'@
    }
    $process = Get-Process -Id $serverProcess[0].ProcessId
    $sent = [Mir2WindowLifecycle]::Close([uint32]$process.Id)
    if ($sent -eq 0) { throw 'No server window accepted WM_CLOSE; process was preserved.' }
    if (!$process.WaitForExit(60000)) { throw 'Graceful shutdown is still pending; process was preserved.' }
}

switch ($Action) {
    'Start' {
        if ($serverProcess) { Write-Output "Already running: $($serverProcess[0].ProcessId)"; break }
        if (!(Test-Path -LiteralPath $serverExe)) { throw "Missing runtime: $serverExe" }
        $showVal = [uint16]$(if ($ShowWindow) { 1 } else { 0 })
        $si = New-CimInstance -ClassName Win32_ProcessStartup -Property @{ ShowWindow = $showVal } -ClientOnly
        $res = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{
            CommandLine = "`"$serverExe`""
            CurrentDirectory = $serverDirectory
            ProcessStartupInformation = $si
        }
        if ($res.ReturnValue -ne 0) { throw "Win32_Process.Create failed with code $($res.ReturnValue)" }
        Write-Output "Started Crystal: $($res.ProcessId)"
    }
    'Stop' { Stop-Crystal; Write-Output 'Crystal stopped gracefully.' }
    'Status' {
        $listener = @(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue |
            Where-Object { $serverProcess -and $_.OwningProcess -eq $serverProcess[0].ProcessId })
        [pscustomobject]@{
            Runtime = $serverDirectory
            Running = [bool]$serverProcess
            ProcessId = if ($serverProcess) { $serverProcess[0].ProcessId } else { $null }
            Listeners = @($listener | Select-Object LocalAddress,LocalPort)
        } | ConvertTo-Json -Depth 4
    }
    'Backup' {
        if ($serverProcess) { throw 'Stop Crystal gracefully before taking a consistent backup.' }
        $backup = Join-Path $RuntimeRoot ('backups\' + [DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss') + 'Z')
        if (Test-Path -LiteralPath $backup) { throw 'Backup destination already exists.' }
        New-Item -ItemType Directory -Path $backup | Out-Null
        foreach ($name in @('Configs','Envir','Guilds','Conquests','Archive','Server.MirDB','Server.MirADB','Server.MirHDB','Server.MirGDB')) {
            $path = Join-Path $serverDirectory $name
            if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination (Join-Path $backup $name) -Recurse }
        }
        Write-Output "Consistent backup: $backup"
    }
}
