#!/usr/bin/env bash
# ============================================================================
# guard-tdd-red.sh — BLOCKING HOOK (strict profile)
# Enforces Red-before-Green for /unity-tdd-pipeline's TDD stage: while
# session.json's .tdd.phase is "red-pending", new non-test .cs files cannot
# be created. This forces a failing test to exist (and be confirmed failing)
# before any implementation file is written.
#
# Scoped to Write only (new file creation) so Stage 3 scaffolding edits and
# ordinary work outside the pipeline (.tdd.phase left unset) are unaffected.
# ============================================================================
# Trigger: PreToolUse on Write
# Exit:    2 = block, 0 = allow
# ============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK_PROFILE_LEVEL="strict"
source "${SCRIPT_DIR}/_lib.sh"

INPUT=$(cat)

TOOL_NAME=$(echo "$INPUT" | jq -r '.tool_name // empty')

# Only gate Write (new-file creation) — Edit on already-scaffolded stubs is fine.
if [ "$TOOL_NAME" != "Write" ]; then
    exit 0
fi

FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')

case "$FILE_PATH" in
    *.cs) ;;
    *) exit 0 ;;
esac

# Test files are exactly what RED is supposed to produce — always allowed.
case "$FILE_PATH" in
    *Test*|*test*|*/Tests/*|*/Editor/*) exit 0 ;;
esac

if [ ! -f "$UNITY_SESSION_FILE" ]; then
    exit 0
fi

TDD_PHASE=$(unity_state_read "tdd.phase")

if [ "$TDD_PHASE" = "red-pending" ]; then
    unity_track_warning "guard-tdd-red" "blocked implementation write during red-pending: $FILE_PATH"

    echo "" >&2
    echo "  GuardTddRed — TDD RED stage in progress." >&2
    echo "  File: $FILE_PATH" >&2
    echo "" >&2
    echo "  A failing test for this feature has not been confirmed yet" >&2
    echo "  (tdd.phase = \"red-pending\"). Write/run the failing test first," >&2
    echo "  confirm it fails via run_tests, then advance tdd.phase to" >&2
    echo "  \"red-confirmed\" before creating implementation files." >&2
    echo "" >&2
    unity_hook_block "GuardTddRed: confirm a failing test before writing implementation code."
fi

exit 0
