#!/usr/bin/env bash
# ============================================================================
# guard-plan-phase.sh — BLOCKING HOOK (strict profile)
# Second line of defense for /unity-tdd-pipeline's Plan -> Edit transition.
# Agent-level tool restriction (planning stage uses read-only agents) is the
# primary guard; this catches the case where the main thread itself tries to
# Edit/Write before the plan has been approved (workflow_phase left at "Plan"
# in session.json until Stage 3 explicitly flips it to "Execute").
# ============================================================================
# Trigger: PreToolUse on Edit|Write
# Exit:    2 = block, 0 = allow
# ============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK_PROFILE_LEVEL="strict"
source "${SCRIPT_DIR}/_lib.sh"

INPUT=$(cat)

# Only meaningful if a session state file exists — projects/sessions that
# never touch workflow_phase are unaffected (value reads as empty -> allow).
if [ ! -f "$UNITY_SESSION_FILE" ]; then
    exit 0
fi

WORKFLOW_PHASE=$(unity_state_read "workflow_phase")

if [ "$WORKFLOW_PHASE" = "Plan" ]; then
    FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')

    unity_track_warning "guard-plan-phase" "blocked edit during Plan phase: $FILE_PATH"

    echo "" >&2
    echo "  GuardPlanPhase — still in the Plan stage of /unity-tdd-pipeline." >&2
    [ -n "$FILE_PATH" ] && echo "  File: $FILE_PATH" >&2
    echo "" >&2
    echo "  The plan has not been approved yet (workflow_phase = \"Plan\")." >&2
    echo "  Finish planning, call ExitPlanMode, then set workflow_phase to" >&2
    echo "  \"Execute\" before making file changes." >&2
    echo "" >&2
    unity_hook_block "GuardPlanPhase: plan not yet approved (workflow_phase = Plan)."
fi

exit 0
