#!/usr/bin/env bash
# ============================================================================
# toast-alert.sh — STOP HOOK (standard profile)
# Shows a Windows balloon/toast notification for ~5 seconds when Claude
# finishes responding, so the user notices without needing the window
# forced to the foreground (e.g. while alt-tabbed into Unity).
#
# Windows-only (System.Windows.Forms.NotifyIcon, built into .NET — no
# external module required). The PowerShell process is launched detached
# so this hook returns immediately; the balloon lingers on its own.
# ============================================================================
# Trigger: Stop
# Exit: 0 always (best-effort — never blocks the session)
# ============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK_PROFILE_LEVEL="standard"
source "${SCRIPT_DIR}/_lib.sh"

# Windows-only — no-op elsewhere
PS_BIN=""
if command -v powershell.exe >/dev/null 2>&1; then
    PS_BIN="powershell.exe"
elif command -v powershell >/dev/null 2>&1; then
    PS_BIN="powershell"
else
    exit 0
fi

TITLE="Claude Code"
MESSAGE="응답이 완료됐습니다"

nohup "$PS_BIN" -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command '
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$notify = New-Object System.Windows.Forms.NotifyIcon
$notify.Icon = [System.Drawing.SystemIcons]::Information
$notify.Visible = $true
$notify.BalloonTipTitle = "'"${TITLE}"'"
$notify.BalloonTipText = "'"${MESSAGE}"'"
$notify.BalloonTipIcon = [System.Windows.Forms.ToolTipIcon]::Info
$notify.ShowBalloonTip(5000)

Start-Sleep -Seconds 5
$notify.Visible = $false
$notify.Dispose()
' > /dev/null 2>&1 < /dev/null &
disown

exit 0
