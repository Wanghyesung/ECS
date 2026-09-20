#!/usr/bin/env bash
# Codex PreToolUse does not support permissionDecision=ask.
# The user-approval requirement remains in AGENTS.md; remind the agent.
set -euo pipefail
INPUT=$(cat)
FILE_PATH=$(printf '%s' "$INPUT" | jq -r '.tool_input.file_path // empty' | tr '\\\\' '/')
case "$FILE_PATH" in
    .codex/docs/known-issues.md|*/.codex/docs/known-issues.md)
        jq -n '{hookSpecificOutput: {hookEventName: "PreToolUse", additionalContext: "known-issues.md 변경은 기존 사용자 승인 범위인지 확인한다. 승인이 없으면 변경안을 먼저 보여주고 승인을 받는다. 이 훅은 자동 승인 UI를 만들거나 실행을 차단하지 않는다."}}'
        ;;
esac
