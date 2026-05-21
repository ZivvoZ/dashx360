param(
    [string]$ProjectDir = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputDir = (Join-Path $PSScriptRoot 'publish')
)

$ErrorActionPreference = 'Stop'

$projectFile = Join-Path $ProjectDir 'XboxMetroLauncher.csproj'

if (-not (Test-Path -LiteralPath $projectFile)) {
    throw "Could not find project file at $projectFile"
}

if (Test-Path -LiteralPath $OutputDir) {
    Remove-Item -LiteralPath $OutputDir -Recurse -Force
}

dotnet publish $projectFile `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host "Published XboxMetroLauncher to $OutputDir"
