#!/usr/bin/env bash
# ============================================================================
# block-git-commit.sh — BLOCKING HOOK
# Unconditionally blocks `git commit` (including --amend) run via the Bash
# tool. Added by user request: the agent must never commit on its own
# initiative — every commit needs the user's explicit go-ahead, given by the
# user actually running it themselves or by deliberately disabling this hook.
# ============================================================================
# Trigger: PreToolUse on Bash
# Exit: 2 = block, 0 = allow
# ============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK_PROFILE_LEVEL="minimal"
source "${SCRIPT_DIR}/_lib.sh"

INPUT=$(cat)

COMMAND=$(echo "$INPUT" | jq -r '.tool_input.command // empty')

if [ -z "$COMMAND" ]; then
    exit 0
fi

if echo "$COMMAND" | grep -qE 'git\s+commit(\s|$)'; then
    MSG="git commit is blocked unconditionally by this hook."
    echo "" >&2
    echo "  Command: $COMMAND" >&2
    echo "" >&2
    echo "  This repo blocks 'git commit' (and --amend) from the Bash tool" >&2
    echo "  unconditionally, by user request — no retry, no in-chat override." >&2
    echo "" >&2
    echo "  Tell the user this hook blocked the commit. To actually commit:" >&2
    echo "    - the user runs 'git commit' themselves in their own terminal, or" >&2
    echo "    - the user (not you) sets in .claude/settings.local.json:" >&2
    echo "        \"env\": { \"DISABLE_HOOK_BLOCK_GIT_COMMIT\": \"1\" }" >&2
    echo "" >&2
    echo "  Do not edit settings.local.json yourself to get past this." >&2
    unity_hook_block "$MSG"
fi

exit 0
