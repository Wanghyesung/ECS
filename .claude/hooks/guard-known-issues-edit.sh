#!/usr/bin/env bash
# ============================================================================
# guard-known-issues-edit.sh — PERMISSION-GATE HOOK
# Forces an explicit user approval prompt before Claude edits or writes
# .claude/docs/known-issues.md, even under acceptEdits/auto mode.
# (User request: known-issues.md entries should never be applied silently.)
# ============================================================================
# Trigger: PreToolUse on Edit|Write
# Output: hookSpecificOutput.permissionDecision = "ask" when the target file
#         is known-issues.md; otherwise silent passthrough (exit 0, no output).
# ============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK_PROFILE_LEVEL="minimal"
source "${SCRIPT_DIR}/_lib.sh"

INPUT=$(cat)
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')

if [ -z "$FILE_PATH" ]; then
    exit 0
fi

# Normalize Windows backslashes to forward slashes for matching
NORMALIZED_PATH="${FILE_PATH//\\//}"

case "$NORMALIZED_PATH" in
    */.claude/docs/known-issues.md)
        jq -n '{
            hookSpecificOutput: {
                hookEventName: "PreToolUse",
                permissionDecision: "ask",
                permissionDecisionReason: "known-issues.md 반영 전 사용자 승인 필요 (사용자 요청으로 설정된 훅)"
            }
        }'
        exit 0
        ;;
    *)
        exit 0
        ;;
esac
