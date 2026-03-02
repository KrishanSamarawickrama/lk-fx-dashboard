<#
.SYNOPSIS
    Build and push the LK FX Dashboard Docker image to Docker Hub.

.PARAMETER Tag
    Image tag. Defaults to 'latest'.

.PARAMETER NoPush
    Build only, skip pushing to Docker Hub.

.EXAMPLE
    .\build-and-push.ps1
    .\build-and-push.ps1 -Tag "1.0.0"
    .\build-and-push.ps1 -NoPush
#>
param(
    [string]$Tag,
    [switch]$NoPush
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Load .env if present
$envFile = Join-Path $PSScriptRoot ".env"
if (Test-Path $envFile) {
    Get-Content $envFile | ForEach-Object {
        if ($_ -match '^\s*([^#][^=]+)=(.*)$') {
            [Environment]::SetEnvironmentVariable($Matches[1].Trim(), $Matches[2].Trim(), "Process")
        }
    }
}

$image    = if ($env:DOCKER_IMAGE) { $env:DOCKER_IMAGE } else { "youruser/lk-fx-dashboard" }
$imageTag = if ($Tag) { $Tag } elseif ($env:DOCKER_TAG) { $env:DOCKER_TAG } else { "latest" }
$fullName = "${image}:${imageTag}"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

Write-Host "Building $fullName ..." -ForegroundColor Cyan
docker build -t $fullName -f (Join-Path $PSScriptRoot "Dockerfile") $repoRoot
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed." -ForegroundColor Red
    exit 1
}
Write-Host "Build succeeded." -ForegroundColor Green

if ($NoPush) {
    Write-Host "Skipping push (-NoPush)." -ForegroundColor Yellow
    exit 0
}

Write-Host "Pushing $fullName ..." -ForegroundColor Cyan
docker push $fullName
if ($LASTEXITCODE -ne 0) {
    Write-Host "Push failed. Run 'docker login' first." -ForegroundColor Red
    exit 1
}
Write-Host "Pushed $fullName" -ForegroundColor Green
