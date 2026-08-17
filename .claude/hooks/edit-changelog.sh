#!/usr/bin/env bash
# ============================================================================
# edit-changelog.sh — TRACKING HOOK (standard profile)
# Appends one line per Edit/Write to a running changelog file:
#   <timestamp> | <file path> | <line-count change>
# Line-count change comes from `git diff --stat` when the file is tracked;
# falls back to a raw line count for brand-new (untracked) files.
# ============================================================================
# Trigger: PostToolUse on Edit|Write
# Exit: 0 always (tracking only, never blocks)
# ============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK_PROFILE_LEVEL="standard"
source "${SCRIPT_DIR}/_lib.sh"

INPUT=$(cat)
# jq isn't installed on this machine (checked: not on PATH) - fall back to sed
# so this hook doesn't silently fail like the jq-dependent hooks in this profile do
FILE_PATH=$(echo "$INPUT" | sed -n 's/.*"file_path"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')

if [ -z "$FILE_PATH" ]; then
    exit 0
fi

CHANGELOG_FILE="${UNITY_HOOK_STATE_DIR}/edit-changelog.txt"
TIMESTAMP=$(date '+%Y-%m-%d %H:%M:%S')

STAT=""
if git rev-parse --show-toplevel >/dev/null 2>&1; then
    STAT=$(git diff --stat -- "$FILE_PATH" 2>/dev/null | tail -1 | sed 's/^ *//')
fi

if [ -z "$STAT" ]; then
    if [ -f "$FILE_PATH" ]; then
        LINES=$(wc -l < "$FILE_PATH" 2>/dev/null | tr -d ' ')
        STAT="new file, ${LINES} lines"
    else
        STAT="(no changes detected)"
    fi
fi

echo "${TIMESTAMP} | ${FILE_PATH} | ${STAT}" >> "$CHANGELOG_FILE"

exit 0
