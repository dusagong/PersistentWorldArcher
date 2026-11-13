param([Parameter(Mandatory)][string]$Client)
$ErrorActionPreference = 'Stop'
$binary = (Resolve-Path -LiteralPath $Client).Path
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$logs = Join-Path $repo ('Library/Validation/Accounts-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$previous = @{}
foreach ($name in @('PWA_JOIN_CODE','PWA_GAME_PORT','PWA_HEALTH_PORT','PWA_DATA_DIR')) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
$children = [Collections.Generic.List[Diagnostics.Process]]::new()
function ClientCase([string]$name, [string]$mode, [string]$key, [int]$selected, [int]$expected, [switch]$Reject) {
    $log = Join-Path $logs ($name + '.log')
    $flags = @('-batchmode','-nographics','-logFile',('"' + $log + '"'),'-pwa-connect','ws://127.0.0.1:17977',
        '-pwa-code',$env:PWA_JOIN_CODE,'-pwa-character-key',$key,'-pwa-name','AccountCheck','-pwa-account-mode',$mode,'-pwa-class',$selected)
    if ($Reject) { $flags += @('-pwa-probe','-pwa-expect-reject') }
    else { $flags += @('-pwa-account-probe','-pwa-expect-class',$expected) }
    $child = Start-Process -FilePath $binary -ArgumentList $flags -WindowStyle Hidden -PassThru
    $children.Add($child)
    if (-not $child.WaitForExit(30000) -or $child.ExitCode -ne 0) { throw ($name + ' failed. See ' + $log) }
    $marker = if ($Reject) { 'PWA_REJECTION_PROBE_OK' } else { 'PWA_ACCOUNT_OK' }
    if (-not (Select-String -LiteralPath $log -Pattern $marker -Quiet)) { throw ('Missing account marker ' + $name) }
    Write-Output ($name + ' OK')
}
try {
    $env:PWA_JOIN_CODE = [Guid]::NewGuid().ToString('N')
    $env:PWA_GAME_PORT = '17977'; $env:PWA_HEALTH_PORT = '18981'; $env:PWA_DATA_DIR = Join-Path $logs 'data'
    $server = Start-Process -FilePath $binary -ArgumentList @('-batchmode','-nographics','-pwa-server','-logFile',('"' + (Join-Path $logs 'server.log') + '"')) -WindowStyle Hidden -PassThru
    $children.Add($server)
    $ready = $false; $deadline = [DateTime]::UtcNow.AddSeconds(25)
    while ([DateTime]::UtcNow -lt $deadline -and -not $server.HasExited) {
        try { $ready = (Invoke-RestMethod 'http://127.0.0.1:18981/health' -TimeoutSec 1).ready } catch { }
        if ($ready) { break }; Start-Sleep -Milliseconds 250
    }
    if (-not $ready) { throw 'Account test server not ready.' }
    $persistentKey = [Guid]::NewGuid().ToString('N'); $demoKey = [Guid]::NewGuid().ToString('N')
    ClientCase 'CreateWarrior' 'create' $persistentKey 1 1
    $saved = @(Get-ChildItem -LiteralPath (Join-Path $env:PWA_DATA_DIR 'characters-v1') -Filter '*.json')
    if ($saved.Count -ne 1 -or (Get-Content -LiteralPath $saved[0].FullName -Raw | ConvertFrom-Json).skills.selected -ne 1) { throw 'Persistent class was not saved.' }
    ClientCase 'RestoreWarrior' 'restore' $persistentKey 2 1
    ClientCase 'CreateDemoHealer' 'demo' $demoKey 2 2
    ClientCase 'DemoCannotRestoreAfterLogout' 'restore' $demoKey 0 0 -Reject
    ClientCase 'DemoStartsFreshAfterLogout' 'demo' $demoKey 0 0
    ClientCase 'UnknownCodeRejected' 'restore' ([Guid]::NewGuid().ToString('N')) 0 0 -Reject
    if (@(Get-ChildItem -LiteralPath (Join-Path $env:PWA_DATA_DIR 'characters-v1') -Filter '*.json').Count -ne 1) { throw 'Demo data was written to disk.' }
    Write-Output 'PWA_ACCOUNT_LIFECYCLE_OK persistent_restore demo_logout demo_no_disk_save unknown_code_rejected'
    Write-Output ('Logs: ' + $logs)
} finally {
    foreach ($child in $children) { if (-not $child.HasExited) { Stop-Process -Id $child.Id -ErrorAction SilentlyContinue } }
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
}
