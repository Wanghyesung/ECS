#!/usr/bin/env bash
# Adapt Codex apply_patch/Bash payloads to existing Unity hook scripts.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel)"
command -v jq >/dev/null || { echo "Codex Unity hooks require jq on Git Bash PATH." >&2; exit 1; }
INPUT=$(cat)
MODE="${1:?pre-edit, post-edit, bash, or stop}"
run_hook() { printf '%s' "$1" | bash "$SCRIPT_DIR/$2.sh"; }
case "$MODE" in
    pre-edit|post-edit)
        EDITS=$(printf '%s' "$INPUT" | jq -c '
            if .tool_input.file_path then [.tool_input]
            else (.tool_input.command // .tool_input.patch // "") | split("\n") |
                reduce .[] as $line ([];
                    if ($line | test("^\\*\\*\\* (Add|Update|Delete) File: ")) then
                        . + [{file_path: ($line | sub("^\\*\\*\\* (Add|Update|Delete) File: "; "")), new_string: "", old_string: ""}]
                    elif ($line | startswith("*** Move to: ")) then
                        . + [{file_path: ($line | ltrimstr("*** Move to: ")), new_string: "", old_string: ""}]
                    elif length == 0 then .
                    elif ($line | startswith("+")) then .[-1].new_string += ($line[1:] + "\n")
                    elif ($line | startswith("-")) then .[-1].old_string += ($line[1:] + "\n")
                    elif ($line | startswith(" ")) then
                        .[-1].new_string += ($line[1:] + "\n") | .[-1].old_string += ($line[1:] + "\n")
                    else . end)
            end | .[] | .file_path |= gsub("\\\\"; "/") | {tool_input: .}')
        while IFS= read -r edit; do
            [ -z "$edit" ] && continue
            if [ "$MODE" = "pre-edit" ]; then
                for hook in block-scene-edit block-meta-edit guard-editor-runtime guard-known-issues-edit; do
                    run_hook "$edit" "$hook"
                done
            else
                run_hook "$edit" cs-lint
            fi
        done <<< "$EDITS"
        ;;
    bash)
        INPUT=$(printf '%s' "$INPUT" | jq '.tool_input.command //= .tool_input.cmd')
        run_hook "$INPUT" block-git-commit
        run_hook "$INPUT" bash-gate
        ;;
    stop)
        run_hook "$INPUT" compile-gate
        run_hook "$INPUT" toast-alert
        ;;
    *) echo "Unknown hook mode: $MODE" >&2; exit 1 ;;
esac
