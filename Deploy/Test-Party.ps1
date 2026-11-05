param([switch]$Capture, [switch]$LinuxServer)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$binary = Join-Path $repoRoot 'Builds/CooperativeWindows/PwaCoop.exe'
$logs = Join-Path $repoRoot ('Library/Validation/Party-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$children = [Collections.Generic.List[Diagnostics.Process]]::new()
$previousCode = $env:PWA_JOIN_CODE
$previousLimit = $env:PWA_MAX_PLAYERS
$container = $null
$serverWait = $null
function Start-PartyProcess([string]$name, [string[]]$extra) {
    $log = Join-Path $logs ($name + '.log')
    $startFlags = @('-logFile', "`"$log`"")
    if (-not ($Capture -and $name -in @('Leader','Member'))) { $startFlags += @('-batchmode', '-nographics') }
    else { $extra += @('-pwa-capture', "`"$(Join-Path $logs ($name + '.png'))`"", '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0') }
    $child = Start-Process -FilePath $binary -ArgumentList ($startFlags + $extra) -WindowStyle Hidden -PassThru
    $children.Add($child)
    return $child
}
try {
    $env:PWA_JOIN_CODE = [Guid]::NewGuid().ToString('N'); $env:PWA_MAX_PLAYERS = '4'
    $endpoint = 'ws://127.0.0.1:7777'; $healthEndpoint = 'http://127.0.0.1:8081/health'
    if ($LinuxServer) {
        $container = 'pwa-party-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
        docker run -d --name $container --user 10001:10001 -p 127.0.0.1:18080:8080 -e PWA_JOIN_CODE --entrypoint /bin/sh pwa-cooperative-test:local -c 'caddy run --config /app/Caddyfile --adapter caddyfile & exec /app/Server/PwaServer -batchmode -nographics -logFile - -pwa-server -pwa-party-validation' | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Linux test container failed to start.' }
        $serverWait = Join-Path $logs 'container-exit.txt'
        $server = Start-Process -FilePath 'docker.exe' -ArgumentList @('wait', $container) -WindowStyle Hidden -PassThru -RedirectStandardOutput $serverWait
        $children.Add($server)
        $endpoint = 'ws://127.0.0.1:18080'; $healthEndpoint = 'http://127.0.0.1:18080/health'
    } else { $server = Start-PartyProcess 'server' @('-pwa-server', '-pwa-party-validation') }
    $deadline = (Get-Date).AddSeconds(30); $ready = $false
    while ((Get-Date) -lt $deadline -and -not $server.HasExited) {
        try { $health = Invoke-RestMethod $healthEndpoint -TimeoutSec 1; $ready = $health.ready } catch { }
        if ($ready) { break }; Start-Sleep -Milliseconds 300
    }
    if (-not $ready) { throw 'Party server not ready.' }
    foreach ($role in @('Leader', 'Member', 'Outsider', 'Healer')) {
        Start-PartyProcess $role @('-pwa-connect',$endpoint,'-pwa-code',$env:PWA_JOIN_CODE,'-pwa-name',$role,'-pwa-probe','-pwa-party-probe') | Out-Null
    }
    if (-not $server.WaitForExit(100000) -or $server.ExitCode -ne 0) { throw 'Party server validation failed.' }
    if ($LinuxServer) {
        docker logs $container 2>&1 | Set-Content -Path (Join-Path $logs 'server.log') -Encoding utf8
        if ((Get-Content $serverWait -Raw).Trim() -ne '0') { throw 'Linux server validation failed.' }
    }
    foreach ($child in $children) {
        if (-not $child.WaitForExit(10000) -or $child.ExitCode -ne 0) { throw 'Party client validation failed.' }
    }
    if (-not (Select-String -Path (Join-Path $logs 'server.log') -Pattern 'PWA_PARTY_INTEGRATION_OK' -Quiet)) { throw 'Missing party validation marker.' }
    foreach ($role in @('Leader', 'Member', 'Outsider', 'Healer')) {
        if (-not (Select-String -Path (Join-Path $logs ($role + '.log')) -Pattern 'PWA_PARTY_CLIENT_OK' -Quiet)) { throw "$role did not finish." }
    }
    Write-Output 'PWA_FOUR_CLIENT_PARTY_OK'
    Write-Output "Logs: $logs"
} finally {
    if ($container) {
        docker logs $container 2>&1 | Set-Content -Path (Join-Path $logs 'server.log') -Encoding utf8
        docker rm -f $container | Out-Null
    }
    foreach ($child in $children) { if (-not $child.HasExited) { Stop-Process -Id $child.Id -ErrorAction SilentlyContinue } }
    $env:PWA_JOIN_CODE = $previousCode; $env:PWA_MAX_PLAYERS = $previousLimit
}
