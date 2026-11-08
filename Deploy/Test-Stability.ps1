param(
    [Parameter(Mandatory)][string]$Endpoint,
    [Parameter(Mandatory)][string]$Client,
    [ValidateRange(30, 1800)][int]$Seconds = 300,
    [switch]$Party
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrEmpty($env:PWA_JOIN_CODE)) { throw 'Set the test server PWA_JOIN_CODE.' }
$binary = (Resolve-Path -LiteralPath $Client).Path
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$logs = Join-Path $repo ('Library/Validation/Stability-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $logs -Force | Out-Null
Write-Output "Logs: $logs"
$children = [Collections.Generic.List[Diagnostics.Process]]::new()
$marker = if ($Party) { 'PWA_PARTY_CLIENT_OK' } else { 'PWA_SOAK_OK' }
$deadline = [DateTime]::UtcNow.AddSeconds($Seconds + 40)
try {
    foreach ($role in @('Leader', 'Member', 'Outsider', 'Healer')) {
        $log = Join-Path $logs "$role.log"
        $key = [Guid]::NewGuid().ToString('N')
        $argsForClient = @('-batchmode', '-nographics', '-logFile', "`"$log`"", '-pwa-connect', $Endpoint,
            '-pwa-code', $env:PWA_JOIN_CODE, '-pwa-name', $role, '-pwa-character-key', $key)
        if ($Party) { $argsForClient += @('-pwa-probe', '-pwa-party-probe') }
        else { $argsForClient += @('-pwa-soak-seconds', $Seconds) }
        $children.Add((Start-Process -FilePath $binary -ArgumentList $argsForClient -WindowStyle Hidden -PassThru))
        Start-Sleep -Milliseconds 500
    }
    while (($children | Where-Object { -not $_.HasExited }) -and [DateTime]::UtcNow -lt $deadline) {
        if ($children | Where-Object { $_.HasExited -and $_.ExitCode -ne 0 }) { break }
        Start-Sleep -Seconds 1
    }
    $results = @(Get-ChildItem -LiteralPath $logs -Filter '*.log' | Select-String -Pattern $marker)
    $results | ForEach-Object { $_.Line }
    if ($results.Count -ne 4 -or ($children | Where-Object { -not $_.HasExited -or $_.ExitCode -ne 0 })) {
        Get-ChildItem -LiteralPath $logs -Filter '*.log' | Select-String -Pattern 'PWA_CLIENT_|PWA_TLS_ERROR|PWA_TRANSPORT_DISCONNECT|PWA_WEBSOCKET_CLOSE|FAILED|Game frame rejected'
        throw 'Four-client validation failed.'
    }
} finally {
    foreach ($child in $children) {
        if (-not $child.HasExited) { Stop-Process -Id $child.Id -ErrorAction SilentlyContinue }
    }
}
