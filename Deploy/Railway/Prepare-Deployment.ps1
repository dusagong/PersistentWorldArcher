param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.0.60f1\Editor\Unity.exe',
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$bundleRoot = Join-Path $repoRoot 'Builds/Railway'
if (-not $SkipBuild) {
    $buildLog = Join-Path $repoRoot 'unity-railway-build.log'
    $buildArguments = @('-batchmode', '-nographics', '-quit', '-projectPath', "`"$repoRoot`"", '-buildTarget', 'Linux64', '-standaloneBuildSubtarget', 'Server', '-executeMethod', 'PersistentWorld.Editor.NetworkSandboxBuild.BuildLinux', '-logFile', "`"$buildLog`"")
    $buildProcess = Start-Process -FilePath $UnityEditor -ArgumentList $buildArguments -WindowStyle Hidden -PassThru -Wait
    if ($buildProcess.ExitCode -ne 0) { throw "Unity build failed. See $buildLog" }
}
$serverBinary = Join-Path $bundleRoot 'Server/PwaServer'
if (-not (Test-Path -LiteralPath $serverBinary)) { throw "Linux server build is missing: $serverBinary" }
foreach ($file in @('Dockerfile', 'Caddyfile', 'entrypoint.sh', 'railway.json', '.dockerignore')) {
    $content = [IO.File]::ReadAllText((Join-Path $PSScriptRoot $file)).Replace("`r`n", "`n")
    [IO.File]::WriteAllText((Join-Path $bundleRoot $file), $content, [Text.UTF8Encoding]::new($false))
}
Write-Output "Deployment bundle ready: $bundleRoot"
Write-Output 'From that directory, run: railway up --service pwa-game-server'
