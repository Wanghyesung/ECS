param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'template'
$target = (Resolve-Path -LiteralPath $ProjectPath).Path
if (-not (Test-Path -LiteralPath (Join-Path $target 'Assets') -PathType Container) -or
    -not (Test-Path -LiteralPath (Join-Path $target 'ProjectSettings') -PathType Container)) {
    throw "Unity project root required: $target"
}
if (-not (Test-Path -LiteralPath (Join-Path $target '.git'))) {
    throw "Git repository root required for hooks: $target"
}

$copied = 0
$skipped = 0
Get-ChildItem -LiteralPath $source -File -Recurse -Force | ForEach-Object {
    $relative = $_.FullName.Substring($source.Length).TrimStart('\', '/')
    $destination = Join-Path $target $relative
    if ((Test-Path -LiteralPath $destination) -and -not $Force) {
        Write-Host "SKIP $relative"
        $skipped++
        return
    }
    $parent = Split-Path -Parent $destination
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
    Write-Host "COPY $relative"
    $copied++
}
Write-Host "Installed $copied files; skipped $skipped existing files."
Write-Host 'Next: edit AGENTS.md and .codex/config.toml for this project, then trust project config/hooks in Codex.'
