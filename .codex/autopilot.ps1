<#
.SYNOPSIS
Run approved PRDs with codex exec, one fresh context per PRD.
.EXAMPLE
powershell -File .codex/autopilot.ps1 -DryRun
.NOTES
Requires trusted hooks, Git Bash, jq and a running Unity MCP server.
Codex has no --max-budget-usd equivalent; no dollar cap is promised.
#>
param(
    [string[]]$Projects = @((Resolve-Path (Join-Path $PSScriptRoot "..")).Path),
    [string]$SlackWebhook = "",
    [switch]$CreatePullRequest,
    [switch]$DryRun
)
$ErrorActionPreference = "Stop"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$summary = New-Object System.Collections.Generic.List[string]
$failed = $false
function Log([string]$message) { $summary.Add($message); Write-Host $message }
function GitChecked([string[]]$GitArgs) {
    & git @GitArgs
    if ($LASTEXITCODE -ne 0) { throw "git failed: $($GitArgs -join ' ')" }
}
$previousAutopilot = $env:CODEX_AUTOPILOT
$previousCommitHook = $env:DISABLE_HOOK_BLOCK_GIT_COMMIT
try {
    foreach ($project in $Projects) {
        $projectPath = (Resolve-Path -LiteralPath $project).Path
        Push-Location -LiteralPath $projectPath
        try {
            $prds = @(Get-ChildItem -Path ".codex/docs/prd/*.md" -ErrorAction SilentlyContinue |
                Where-Object { (Get-Content -LiteralPath $_.FullName -Raw) -match '(?m)^status:\s*approved\s*$' } |
                Sort-Object Name)
            if ($prds.Count -eq 0) { Log "$projectPath : SKIP, no approved PRDs."; continue }
            if ($DryRun) {
                foreach ($prd in $prds) {
                    Log ('codex exec --sandbox workspace-write -c approval_policy="never" -C "{0}" -- "$unity-autopilot .codex/docs/prd/{1}"' -f $projectPath, $prd.Name)
                }
                continue
            }
            # Protect pre-existing work, including untracked configuration.
            $dirty = @(GitChecked @("status", "--porcelain"))
            if ($dirty.Count -gt 0) { Log "$projectPath : SKIP, commit or stash existing changes first."; continue }
            Get-Command codex -ErrorAction Stop | Out-Null
            $mcpConfig = & codex mcp get UnityMCP --json
            if ($LASTEXITCODE -ne 0) { throw "Cannot read UnityMCP from Codex configuration." }
            $mcpUrl = ($mcpConfig | ConvertFrom-Json).transport.url
            if (-not $mcpUrl) { throw "UnityMCP HTTP URL missing." }
            $body = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"codex-autopilot","version":"1"}}}'
            Invoke-WebRequest -Uri $mcpUrl -Method Post -Body $body -ContentType "application/json" -Headers @{ Accept = "application/json, text/event-stream" } -TimeoutSec 8 -UseBasicParsing | Out-Null
            $branch = "auto/$stamp"
            $base = GitChecked @("rev-parse", "HEAD")
            GitChecked @("checkout", "-b", $branch)
            $logDir = Join-Path $projectPath ".codex/state/autopilot/$stamp"
            New-Item -ItemType Directory -Force -Path $logDir | Out-Null
            $env:CODEX_AUTOPILOT = "1"
            $env:DISABLE_HOOK_BLOCK_GIT_COMMIT = "1"
            foreach ($prd in $prds) {
                $resultPath = Join-Path $logDir ($prd.BaseName + ".result.txt")
                $eventPath = Join-Path $logDir ($prd.BaseName + ".jsonl")
                $prompt = '$unity-autopilot .codex/docs/prd/' + $prd.Name
                Log "START $($prd.BaseName)"
                & codex exec --sandbox workspace-write -c 'approval_policy="never"' -C $projectPath --json -o $resultPath -- $prompt > $eventPath 2> ($eventPath + ".err")
                if ($LASTEXITCODE -ne 0) { throw "Codex failed for $($prd.Name); inspect $eventPath.err" }
                $result = Get-Content -LiteralPath $resultPath -Raw
                Log $result
                if ($result -notmatch '(?m)^AUTOPILOT RESULT:\s*(done|blocked)\s*$') { throw "Missing result for $($prd.Name)." }
                if ((Get-Content -LiteralPath $prd.FullName -Raw) -notmatch '(?m)^status:\s*(done|blocked)\s*$') { throw "PRD result was not saved." }
                if (GitChecked @("status", "--porcelain")) { throw "Uncommitted changes remain; stopping before next PRD." }
            }
            GitChecked @("log", "--oneline", "$base..HEAD")
            if ($CreatePullRequest) {
                Get-Command gh -ErrorAction Stop | Out-Null
                GitChecked @("push", "-u", "origin", $branch)
                $bodyFile = Join-Path $logDir "pr-body.md"
                "Autopilot run $stamp. Logs: .codex/state/autopilot/$stamp/" | Set-Content -LiteralPath $bodyFile -Encoding UTF8
                & gh pr create --title "[Auto] $stamp" --body-file $bodyFile
                if ($LASTEXITCODE -ne 0) { throw "PR creation failed." }
            }
        } catch {
            $failed = $true
            Log ("FAILED {0}: {1}" -f $projectPath, $_.Exception.Message)
        } finally {
            $env:CODEX_AUTOPILOT = $previousAutopilot
            $env:DISABLE_HOOK_BLOCK_GIT_COMMIT = $previousCommitHook
            Pop-Location
        }
    }
} finally {
    $env:CODEX_AUTOPILOT = $previousAutopilot
    $env:DISABLE_HOOK_BLOCK_GIT_COMMIT = $previousCommitHook
}
if (-not $DryRun) {
    $stateDir = Join-Path $PSScriptRoot "state/autopilot"
    New-Item -ItemType Directory -Force -Path $stateDir | Out-Null
    $summary | Set-Content -LiteralPath (Join-Path $stateDir "$stamp-summary.txt") -Encoding UTF8
    if ($SlackWebhook) {
        $payload = @{ text = "Codex autopilot $stamp" + [Environment]::NewLine + ($summary -join [Environment]::NewLine) } | ConvertTo-Json -Compress
        Invoke-RestMethod -Uri $SlackWebhook -Method Post -Body ([Text.Encoding]::UTF8.GetBytes($payload)) -ContentType "application/json" | Out-Null
    }
}
if ($failed) { exit 1 }
