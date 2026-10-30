[CmdletBinding()]
param(
    [ValidateRange(5,300)][int]$Seconds = 45,
    [string]$ServerExe = 'D:\private-server\mir2\Server\Server.exe',
    [string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$exe = [IO.Path]::GetFullPath($ServerExe)
$instances = @(Get-CimInstance Win32_Process -Filter "Name='Server.exe'" | Where-Object ExecutablePath -eq $exe)
if ($instances.Count -ne 1) { throw 'Expected exactly one running Crystal instance at the selected path.' }
$process = Get-Process -Id $instances[0].ProcessId
$clock = [Diagnostics.Stopwatch]::StartNew()
$previousTime = $clock.Elapsed.TotalSeconds
$previousCpu = $process.TotalProcessorTime.TotalSeconds
$samples = [Collections.Generic.List[object]]::new()
for ($i = 0; $i -lt $Seconds; $i++) {
    Start-Sleep -Seconds 1
    $process.Refresh()
    if ($process.HasExited) { throw 'Server exited during measurement.' }
    $now = $clock.Elapsed.TotalSeconds
    $cpu = $process.TotalProcessorTime.TotalSeconds
    $samples.Add([pscustomobject]@{
        CPUPercent = 100 * ($cpu - $previousCpu) / ($now - $previousTime) / [Environment]::ProcessorCount
        WorkingSetMB = $process.WorkingSet64 / 1e6
    })
    $previousTime = $now
    $previousCpu = $cpu
}
$cpuStats = $samples | Measure-Object CPUPercent -Average -Minimum -Maximum
$report = [ordered]@{
    CheckedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    Executable = $exe
    ProcessId = $process.Id
    LogicalProcessors = [Environment]::ProcessorCount
    DurationSeconds = $clock.Elapsed.TotalSeconds
    CPUPercent = @{ Average = $cpuStats.Average; Minimum = $cpuStats.Minimum; Maximum = $cpuStats.Maximum }
    WorkingSetMB = ($samples | Measure-Object WorkingSetMB -Average).Average
    Samples = $samples
}
$json = $report | ConvertTo-Json -Depth 5
if ($OutputPath) { [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json, [Text.UTF8Encoding]::new($false)) }
$json
