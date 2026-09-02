#!/usr/bin/env bash
# ============================================================================
# toast-alert.sh — STOP HOOK (standard profile)
# Forces the terminal window running this Claude Code session to the
# foreground when Claude finishes responding, so the user notices even
# while alt-tabbed into Unity (replaces the old passive balloon toast).
#
# Windows-only (user32.dll SetForegroundWindow via P/Invoke, built into
# .NET — no external module required). Walks up the process ancestry from
# this script's own PID to find the nearest ancestor with a real window
# (the terminal hosting the session — Windows Terminal, cmd, VS Code, ...),
# so it focuses the exact window this session is running in, not just any
# Claude Code window. The PowerShell process is launched detached so this
# hook returns immediately.
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

nohup "$PS_BIN" -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command '
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class FocusWin32 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
"@

function Get-ParentProcessId($procId) {
    try {
        (Get-CimInstance Win32_Process -Filter "ProcessId=$procId" -ErrorAction Stop).ParentProcessId
    } catch { $null }
}

$targetHwnd = [IntPtr]::Zero
$currentId = $PID
for ($i = 0; $i -lt 20; $i++) {
    $parentId = Get-ParentProcessId $currentId
    if (-not $parentId) { break }
    try {
        $proc = Get-Process -Id $parentId -ErrorAction Stop
        if ($proc.MainWindowHandle -ne [IntPtr]::Zero) {
            $targetHwnd = $proc.MainWindowHandle
            break
        }
    } catch {}
    $currentId = $parentId
}

if ($targetHwnd -ne [IntPtr]::Zero) {
    # Tap Alt to reset Windows foreground-lock so SetForegroundWindow is honored
    [FocusWin32]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
    [FocusWin32]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)

    if ([FocusWin32]::IsIconic($targetHwnd)) {
        [FocusWin32]::ShowWindow($targetHwnd, 9) # SW_RESTORE
    }
    [FocusWin32]::SetForegroundWindow($targetHwnd) | Out-Null
}
' > /dev/null 2>&1 < /dev/null &
disown

exit 0
