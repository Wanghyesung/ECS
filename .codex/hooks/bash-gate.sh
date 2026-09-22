#!/usr/bin/env bash
# ============================================================================
# bash-gate.sh — BLOCKING HOOK (PreToolUse: Bash)
# 되돌릴 수 없는 명령은 즉시 차단한다. 재시도 통과 없음.
# 무인(autopilot) 실행에서 실질적인 유일한 안전장치이므로
# "1회 거부 → 사실 제시 → 재시도 통과" 같은 2단계 게이트는 쓰지 않는다
# (에이전트는 어차피 재시도하므로 보호 효과가 0이다).
# ============================================================================
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${SCRIPT_DIR}/_lib.sh"

COMMAND=$(cat | jq -r '.tool_input.command // empty')
[ -z "$COMMAND" ] && exit 0

REASON=""
if echo "$COMMAND" | grep -qE 'git\s+reset\s+--hard'; then
    REASON="git reset --hard — 커밋 안 된 씬/프리팹/코드가 복구 불가로 사라진다"
elif echo "$COMMAND" | grep -qE 'git\s+clean\s+-[A-Za-z]*[fFxX]'; then
    REASON="git clean — 추적 안 된 에셋/.meta 가 삭제된다"
elif echo "$COMMAND" | grep -qE 'git\s+push\s+.*(--force|-f)(\s|$)'; then
    REASON="force push — 원격 히스토리를 덮어쓴다"
elif echo "$COMMAND" | grep -qE 'git\s+(checkout|restore|switch)\s+.*(--\s+\.|\s\.\s*$|--force|-f\b)'; then
    REASON="git checkout/restore -- . 또는 --force — 작업 트리 전체를 되돌린다"
elif echo "$COMMAND" | grep -qE '(rm\s+-[A-Za-z]*[rR]|Remove-Item\s+.*-Recurse|rmdir\s+/s)[^|;&]*\b(Assets|Library|ProjectSettings|Packages)\b'; then
    REASON="Assets/Library/ProjectSettings/Packages 재귀 삭제"
elif echo "$COMMAND" | grep -qE '\b(rm|del|Remove-Item|mv|rename)\b[^|;&]*\.meta\b'; then
    REASON=".meta 삭제/이동 — GUID 참조가 전부 깨진다. 에셋 삭제는 Unity(MCP manage_asset)로"
elif echo "$COMMAND" | grep -qE '(>|>>|\brm\b|\bmv\b|\bcp\b)[^|;&]*ProjectSettings/[A-Za-z]+\.asset'; then
    REASON="ProjectSettings/*.asset 직접 수정 — manage_build/manage_physics/manage_graphics MCP 도구를 쓸 것"
elif echo "$COMMAND" | grep -qE '(>|>>|\brm\b|truncate)[^|;&]*Packages/(manifest|packages-lock)\.json'; then
    REASON="Packages/manifest.json 직접 수정 — manage_packages MCP 도구를 쓸 것"
elif echo "$COMMAND" | grep -qE 'git\s+add[^|;&]*(ProjectSettings/|Packages/(manifest|packages-lock)\.json)'; then
    REASON="ProjectSettings/·Packages 매니페스트 스테이징 — 사용자가 직접 확인 후 커밋"
fi

[ -z "$REASON" ] && exit 0

echo "" >&2
echo "  Command: $COMMAND" >&2
echo "  Risk:    $REASON" >&2
echo "  이 명령은 이 훅이 무조건 차단한다. 필요하면 사용자가 직접 터미널에서 실행한다." >&2
unity_hook_block "$REASON"
