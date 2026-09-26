#!/usr/bin/env bash
# Portable default: ordinary commits stay blocked.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_lib.sh"
INPUT=$(cat)
COMMAND=$(printf '%s' "$INPUT" | jq -r '.tool_input.command // .tool_input.cmd // empty')
[ -z "$COMMAND" ] && exit 0

if printf '%s' "$COMMAND" | grep -qE 'git([[:space:]]+-c[[:space:]]+[^[:space:]]+)*[[:space:]]+commit([[:space:]]|$)'; then
    unity_hook_block "Ordinary git commit is blocked. Provide the command to the user. Approved autopilot sets DISABLE_HOOK_BLOCK_GIT_COMMIT=1."
fi
exit 0
