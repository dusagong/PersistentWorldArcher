param([string]$Endpoint = '', [string]$Client = 'Builds/CooperativeWindows/PwaCoop.exe')
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$binary = if ([IO.Path]::IsPathRooted($Client)) { $Client } else { Join-Path $repoRoot $Client }
$logs = Join-Path $repoRoot ('Library/Validation/Network-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $logs -Force | Out-Null
if (-not (Test-Path -LiteralPath $binary)) { throw 'Build the Windows test client first.' }
$server = $null
$children = [Collections.Generic.List[Diagnostics.Process]]::new()
$previousCode = $env:PWA_JOIN_CODE
$previousLimit = $env:PWA_MAX_PLAYERS
function Start-Probe([string]$name, [string[]]$extra) {
    $log = Join-Path $logs ($name + '.log')
    $child = Start-Process -FilePath $binary -ArgumentList (@('-batchmode', '-nographics', '-logFile', "`"$log`"", '-pwa-account-mode', 'demo') + $extra) -WindowStyle Hidden -PassThru
    $children.Add($child)
    return $child
}
try {
    if ([string]::IsNullOrEmpty($Endpoint)) {
        $env:PWA_JOIN_CODE = [Guid]::NewGuid().ToString('N')
        $env:PWA_MAX_PLAYERS = '2'
        $server = Start-Probe 'server' @('-pwa-server','-pwa-network-validation')
        $Endpoint = 'ws://127.0.0.1:7777'
        $deadline = (Get-Date).AddSeconds(30)
        $healthy = $false
        while ((Get-Date) -lt $deadline -and -not $server.HasExited) {
            try { $health = Invoke-RestMethod 'http://127.0.0.1:8081/health' -TimeoutSec 1; $healthy = $health.ready } catch { }
            if ($healthy) { break }
            Start-Sleep -Milliseconds 300
        }
        if (-not $healthy) { throw 'Local server readiness failed.' }
    } elseif ([string]::IsNullOrEmpty($env:PWA_JOIN_CODE)) { throw 'Set PWA_JOIN_CODE before testing a deployed server.' }
    $first = Start-Probe 'client-one' @('-pwa-connect', $Endpoint, '-pwa-code', $env:PWA_JOIN_CODE, '-pwa-probe', '-pwa-test-move', 'right')
    $second = Start-Probe 'client-two' @('-pwa-connect', $Endpoint, '-pwa-code', $env:PWA_JOIN_CODE, '-pwa-probe', '-pwa-test-move', 'up')
    foreach ($child in @($first, $second)) {
        if (-not $child.WaitForExit(45000)) { throw 'Network client timed out.' }
        if ($child.ExitCode -ne 0) { throw "Network client failed: $($child.ExitCode)" }
    }
    foreach ($name in @('client-one', 'client-two')) {
        if (-not (Select-String -Path (Join-Path $logs ($name + '.log')) -Pattern 'PWA_TWO_CLIENTS_PROBE_OK' -Quiet)) { throw "$name did not verify replication." }
    }
    $rejected = Start-Probe 'client-rejected' @('-pwa-connect', $Endpoint, '-pwa-code', 'incorrect-private-code', '-pwa-probe', '-pwa-expect-reject')
    if (-not $rejected.WaitForExit(30000) -or $rejected.ExitCode -ne 0 -or
        -not (Select-String -Path (Join-Path $logs 'client-rejected.log') -Pattern 'PWA_REJECTION_PROBE_OK' -Quiet)) { throw 'Invalid-code rejection failed.' }
    Write-Output 'PWA_NETWORK_SMOKE_OK: two separate moving clients replicated; invalid code rejected.'
    Write-Output "Logs: $logs"
} finally {
    foreach ($child in $children) { if (-not $child.HasExited) { Stop-Process -Id $child.Id -ErrorAction SilentlyContinue } }
    $env:PWA_JOIN_CODE = $previousCode
    $env:PWA_MAX_PLAYERS = $previousLimit
}
