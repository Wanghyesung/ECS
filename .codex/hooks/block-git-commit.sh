#!/usr/bin/env bash
# Ordinary commits stay blocked. The user enabled a narrow workflow_view
# PR exception: explicit command marker + codex/* branch + enabled config.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_lib.sh"
INPUT=$(cat)
COMMAND=$(printf '%s' "$INPUT" | jq -r '.tool_input.command // .tool_input.cmd // empty')
[ -z "$COMMAND" ] && exit 0

# Match only a standalone, normal commit using a message file in local state.
# No command chaining, amend, editor launch, or shell expansion is accepted.
PR_COMMIT='^git -c codex\.workflow=pr-review commit --file "\.codex/state/[A-Za-z0-9_-]+\.txt"$'
if [[ "$COMMAND" =~ $PR_COMMIT ]]; then
    BRANCH=$(git branch --show-current 2>/dev/null || true)
    if [[ "$BRANCH" == codex/?* ]] &&
       jq -e '.enabled == true' "$_git_root/.codex/review-workflow.json" >/dev/null 2>&1; then
        exit 0
    fi
    unity_hook_block "PR workflow commit requires enabled review-workflow.json and a codex/* branch."
fi

if printf '%s' "$COMMAND" | grep -qE 'git([[:space:]]+-c[[:space:]]+[^[:space:]]+)*[[:space:]]+commit([[:space:]]|$)'; then
    unity_hook_block "Ordinary git commit is blocked. For an authorized workflow_view PR, follow .agents/skills/workflow_view/references/pr-review.md. Otherwise provide the commit command for the user to run. Do not disable hooks."
fi
exit 0
