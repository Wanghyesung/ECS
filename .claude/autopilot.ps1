<#
.SYNOPSIS
  밤새 무인 실행 드라이버. 프로젝트마다 승인된 PRD(status: approved)를 순서대로
  `claude -p "/unity-autopilot <prd>"` 로 실행한다. PRD 1개 = 프로세스 1개 = 새 컨텍스트.

.USAGE
  powershell -ExecutionPolicy Bypass -File .claude\autopilot.ps1                       # 이 프로젝트만
  powershell -ExecutionPolicy Bypass -File .claude\autopilot.ps1 -DryRun               # 뭘 할지만 출력
  ... -Projects "C:\...\ECS","C:\...\Other" -BudgetUsd 8                                # 여러 프로젝트 병렬
  ... -SlackWebhook "https://hooks.slack.com/services/XXX"                             # 끝나면 알림

.NOTES
  권한 모델 (무인 실행에서 권한 프롬프트 = 정지이므로 프롬프트가 나올 수 없게 만든다):
    --permission-mode acceptEdits     파일 편집은 자동 허용 (씬/프리팹/.meta 는 훅이 차단)
    --allowedTools                    Unity MCP 전부 + git 의 add/commit/stash/status/diff/log/branch 만.
                                      목록 밖의 Bash 는 프롬프트 없이 그냥 거부되고, Claude 는 계속 진행한다.
  커밋은 이 스크립트가 만든 auto/<stamp> 브랜치에서만. block-git-commit 훅은 이 프로세스 안에서만
  DISABLE_HOOK_BLOCK_GIT_COMMIT=1 로 풀린다 — 대화형 세션은 그대로 차단.
  시작 전 작업 트리가 깨끗해야 한다. 커밋 안 된 변경이 있으면 그 프로젝트는 건너뛴다 (당신 작업 보호).
  PRD당 --max-budget-usd 로 예산 상한. 로그: .claude\state\autopilot\<stamp>\
#>
param(
    [string[]]$Projects = @((Resolve-Path (Join-Path $PSScriptRoot "..")).Path),
    [double]$BudgetUsd = 5.0,
    [string]$SlackWebhook = "",
    [switch]$DryRun
)

$stamp = Get-Date -Format "yyyyMMdd-HHmm"

$allowedTools = @(
    "mcp__UnityMCP",
    "Bash(git add:*)", "Bash(git commit:*)", "Bash(git stash:*)",
    "Bash(git status:*)", "Bash(git diff:*)", "Bash(git log:*)", "Bash(git branch:*)", "Bash(git rev-parse:*)"
) -join ","

$perProject = {
    param($proj, $stamp, $BudgetUsd, $allowedTools, $DryRun)
    $out = New-Object System.Collections.Generic.List[string]
    function Log($m) { $line = "[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $m; $out.Add($line); Write-Host "$(Split-Path $proj -Leaf): $line" }

    Set-Location $proj
    $logDir = Join-Path $proj ".claude\state\autopilot\$stamp"
    New-Item -ItemType Directory -Force $logDir | Out-Null

    # 1. 작업 트리 — 당신의 미커밋 작업이 있으면 건드리지 않는다.
    #    .claude/ (PRD, 규칙 등 문서)는 제외한다 — 낮에 Claude 가 PRD 를 써 두는 것만으로
    #    밤 실행이 통째로 SKIP 되면 "예약만 걸어두면 알아서" 가 성립하지 않기 때문.
    #    보호 대상은 Assets/ProjectSettings/Packages 즉 실제 게임 작업물이다.
    $dirty = git status --porcelain -- Assets ProjectSettings Packages
    if ($dirty) {
        Log "SKIP: Assets/ 에 커밋 안 된 변경이 있음. 자기 전에 커밋/스태시 해둘 것."
        $dirty | Select-Object -First 5 | ForEach-Object { Log "    $_" }
        return ,$out
    }

    # 2. Unity MCP 살아있는지
    $settings = Get-Content (Join-Path $proj ".claude\settings.json") -Raw | ConvertFrom-Json
    $mcpUrl = ($settings.mcpServers.PSObject.Properties | Select-Object -First 1).Value.url
    try {
        $body = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"autopilot","version":"1"}}}'
        $r = Invoke-WebRequest -Uri $mcpUrl -Method Post -Body $body -ContentType "application/json" -Headers @{ Accept = "application/json, text/event-stream" } -TimeoutSec 8 -UseBasicParsing
        if (-not $r.Headers["mcp-session-id"]) { throw "no session" }
    } catch { Log "SKIP: Unity MCP($mcpUrl) 응답 없음 — Unity 에디터 + MCP 서버가 떠 있어야 함."; return ,$out }

    # 3. 승인된 PRD
    $prds = @(Get-ChildItem (Join-Path $proj ".claude\docs\prd\*.md") -ErrorAction SilentlyContinue |
        Where-Object { (Get-Content $_.FullName -Raw) -match '(?m)^status:\s*approved\s*$' } | Sort-Object Name)
    if ($prds.Count -eq 0) { Log "SKIP: status: approved 인 PRD 없음."; return ,$out }
    Log ("PRD {0}개: {1}" -f $prds.Count, (($prds | ForEach-Object { $_.BaseName }) -join ", "))

    # 4. 전용 브랜치
    $base = git rev-parse --short HEAD
    $branch = "auto/$stamp"
    if (-not $DryRun) { git checkout -q -b $branch; Log "branch $branch (base $base)" }

    # 5. PRD 하나 = claude -p 하나 (새 컨텍스트)
    $env:DISABLE_HOOK_BLOCK_GIT_COMMIT = "1"
    $env:CLAUDE_AUTOPILOT = "1"
    foreach ($prd in $prds) {
        $rel = ".claude/docs/prd/" + $prd.Name
        $log = Join-Path $logDir ($prd.BaseName + ".log")
        Log "START $($prd.BaseName)"
        if ($DryRun) { Log "  (dry-run) claude -p `"/unity-autopilot $rel`" --permission-mode acceptEdits --allowedTools ... --max-budget-usd $BudgetUsd"; continue }
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $args = @("-p", "`"/unity-autopilot $rel`"",
                  "--permission-mode", "acceptEdits",
                  "--allowedTools", "`"$allowedTools`"",
                  "--max-budget-usd", "$BudgetUsd",
                  "--output-format", "text")
        $p = Start-Process -FilePath "claude" -ArgumentList $args -NoNewWindow -Wait -PassThru `
            -RedirectStandardOutput $log -RedirectStandardError ($log + ".err")
        $sw.Stop()
        $result = (Select-String -Path $log -Pattern '^AUTOPILOT RESULT:' | Select-Object -Last 1).Line
        if (-not $result) { $result = "AUTOPILOT RESULT: unknown (exit $($p.ExitCode))" }
        Log ("END   {0} — {1} ({2:n0}s)" -f $prd.BaseName, $result, $sw.Elapsed.TotalSeconds)
    }
    Remove-Item Env:DISABLE_HOOK_BLOCK_GIT_COMMIT -ErrorAction SilentlyContinue
    Remove-Item Env:CLAUDE_AUTOPILOT -ErrorAction SilentlyContinue

    # 6. 요약 (+ gh 있으면 PR)
    if (-not $DryRun) {
        Log "commits:"
        git log --oneline "$base..HEAD" | ForEach-Object { Log "  $_" }
        $stash = git stash list | Select-String "autopilot blocked"
        if ($stash) { Log "stash(blocked): $($stash.Count)개 — git stash list 확인" }
        if (Get-Command gh -ErrorAction SilentlyContinue) {
            git push -q -u origin $branch
            $pr = gh pr create --fill --title "[Auto] $stamp" --body "autopilot run $stamp. 로그: .claude/state/autopilot/$stamp/" 2>&1
            Log "PR: $pr"
        } else { Log "gh 없음 — PR 미생성 (winget install GitHub.cli 후 gh auth login)" }
    }
    return ,$out
}

# 프로젝트마다 병렬 Job
$jobs = foreach ($p in $Projects) {
    $full = (Resolve-Path $p).Path
    Start-Job -Name (Split-Path $full -Leaf) -ScriptBlock $perProject -ArgumentList $full, $stamp, $BudgetUsd, $allowedTools, [bool]$DryRun
}
Write-Host "autopilot $stamp — $($Projects.Count)개 프로젝트 실행 중... (Ctrl+C 로 중단)"
$jobs | Wait-Job | Out-Null

$summary = New-Object System.Collections.Generic.List[string]
foreach ($j in $jobs) {
    $lines = Receive-Job $j
    $summary.Add("== $($j.Name) ==")
    foreach ($l in $lines) { $summary.Add([string]$l) }
}
$jobs | Remove-Job
$text = $summary -join "`n"
Write-Host "`n$text"
$summaryPath = Join-Path $PSScriptRoot "state\autopilot\$stamp-summary.txt"
New-Item -ItemType Directory -Force (Split-Path $summaryPath) | Out-Null
$text | Out-File $summaryPath -Encoding utf8

if ($SlackWebhook) {
    $payload = @{ text = "autopilot $stamp 종료`n" + $text } | ConvertTo-Json -Compress
    try { Invoke-RestMethod -Uri $SlackWebhook -Method Post -Body $payload -ContentType "application/json" | Out-Null } catch { Write-Host "Slack 알림 실패: $_" }
}
