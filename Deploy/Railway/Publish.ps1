param(
    [string]$ProjectId = '47a1c1c3-6eb7-44d1-a4dd-ba1d478437a4',
    [string]$Service = 'pwa-game-server',
    [string]$DeploymentEnvironment = 'production',
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Prepare-Deployment.ps1') -SkipBuild:$SkipBuild
$bundleRoot = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path 'Builds/Railway'
& npx.cmd --yes @railway/cli whoami
if ($LASTEXITCODE -ne 0) { throw 'Run npx @railway/cli login first.' }
# Explicit project/service and upload root prevent deploying unrelated coursework or credentials.
& npx.cmd --yes @railway/cli up $bundleRoot --path-as-root --no-gitignore --project $ProjectId --environment $DeploymentEnvironment --service $Service --detach
if ($LASTEXITCODE -ne 0) { throw 'Railway upload failed.' }
Write-Output 'Upload accepted. Check Railway deployment status and /health before distributing the server address.'
