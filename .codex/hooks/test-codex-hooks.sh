#!/usr/bin/env bash
# Synthetic payload checks only: no git mutation, Unity edit, or network call.
set -euo pipefail
ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"
TEST_DIR=$(mktemp -d)
trap 'rm -f "$TEST_DIR/out" "$TEST_DIR/err"; rmdir "$TEST_DIR"' EXIT
export UNITY_HOOK_STATE_DIR="$TEST_DIR"
check() {
    local expected="$1" mode="$2" payload="$3" actual=0
    printf '%s' "$payload" | bash .codex/hooks/codex-hook.sh "$mode" >"$TEST_DIR/out" 2>"$TEST_DIR/err" || actual=$?
    if [ "$actual" -ne "$expected" ]; then
        cat "$TEST_DIR/out" "$TEST_DIR/err" >&2
        echo "Expected $expected, got $actual: $payload" >&2
        exit 1
    fi
}
patch() { jq -nc --arg command "$1" '{tool_name:"apply_patch",tool_input:{command:$command}}'; }
check 0 pre-edit "$(patch $'*** Begin Patch\n*** Add File: README.md\n+hello\n*** End Patch')"
check 2 pre-edit "$(patch $'*** Begin Patch\n*** Update File: Assets/Test.unity\n@@\n-old\n+new\n*** End Patch')"
check 2 pre-edit "$(patch $'*** Begin Patch\n*** Add File: README.md\n+ok\n*** Delete File: Assets/Test.prefab\n*** End Patch')"
check 2 pre-edit "$(patch $'*** Begin Patch\n*** Update File: Assets/Test.cs\n*** Move to: Assets/Test.meta\n@@\n-old\n+new\n*** End Patch')"
check 2 pre-edit '{"tool_input":{"file_path":"C:\\Project\\Assets\\Test.meta","content":"bad"}}'
check 2 pre-edit "$(patch $'*** Begin Patch\n*** Add File: Assets/Test.cs\n+using UnityEditor;\n*** End Patch')"
check 0 pre-edit "$(patch $'*** Begin Patch\n*** Add File: Assets/Editor/Test.cs\n+using UnityEditor;\n*** End Patch')"
check 0 pre-edit "$(patch $'*** Begin Patch\n*** Add File: Assets/Test.cs\n+#if UNITY_EDITOR\n+using UnityEditor;\n+#endif\n*** End Patch')"
check 2 bash '{"tool_input":{"command":"git commit -m example"}}'
check 2 bash '{"tool_input":{"cmd":"git reset --hard"}}'
check 0 bash '{"tool_input":{"command":"git status --short"}}'
check 0 pre-edit "$(patch $'*** Begin Patch\n*** Update File: .codex/docs/known-issues.md\n@@\n-old\n+new\n*** End Patch')"
jq -e '.hookSpecificOutput.hookEventName == "PreToolUse" and (.hookSpecificOutput.additionalContext | length > 0)' "$TEST_DIR/out" >/dev/null
check 0 post-edit "$(patch $'*** Begin Patch\n*** Add File: Assets/Test.cs\n+async void Run() {}\n*** End Patch')"
grep -q 'async void' "$TEST_DIR/err"
# Mock only branch lookup: no checkout, staging, or commit is executed.
git() {
    if [ "$*" = "branch --show-current" ]; then
        printf '%s\n' "$MOCK_BRANCH"
    else
        command git "$@"
    fi
}
export -f git
export MOCK_BRANCH="codex/hook-test"
check 0 bash '{"tool_input":{"command":"git -c codex.workflow=pr-review commit --file \".codex/state/commit-message.txt\""}}'
check 2 bash '{"tool_input":{"command":"git commit -m example"}}'
check 2 bash '{"tool_input":{"command":"git -c codex.workflow=pr-review commit --amend --file \".codex/state/commit-message.txt\""}}'
check 2 bash '{"tool_input":{"command":"git -c codex.workflow=pr-review commit --file \".codex/state/commit-message.txt\"; git status"}}'
export MOCK_BRANCH="main"
check 2 bash '{"tool_input":{"command":"git -c codex.workflow=pr-review commit --file \".codex/state/commit-message.txt\""}}'
export MOCK_BRANCH="codex/hook-test"
jq() {
    if [ "${1:-}" = "-e" ] && [ "${2:-}" = ".enabled == true" ]; then
        return 1
    else
        command jq "$@"
    fi
}
export -f jq
check 2 bash '{"tool_input":{"command":"git -c codex.workflow=pr-review commit --file \".codex/state/commit-message.txt\""}}'
echo "PASS: 19 Codex hook payload checks"
