#!/usr/bin/env bash
# ============================================================================
# compile-gate.sh — STOP HOOK (blocking)
# Codex 가 응답을 끝내려는 시점에 .cs 변경이 있으면 Unity MCP 로
# refresh_unity(컴파일 요청) → read_console(error) 를 직접 호출해서
# 컴파일 에러(error CSxxxx)가 있으면 exit 2 로 멈추지 못하게 한다.
# grep 휴리스틱이 아니라 Unity 컴파일러의 실제 결과가 게이트다.
#
#   - MCP 응답 없음(에디터 꺼짐 등) → fail-open (exit 0, 경고만)
#   - 5회 연속 차단 → 무한 루프 방지를 위해 게이트 해제 후 사람 확인 요청
#   - .cs 변경이 없으면 MCP 호출 자체를 생략 (커밋 직후 등)
# ============================================================================
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${SCRIPT_DIR}/_lib.sh"

INPUT=$(cat)
STOP_ACTIVE=$(echo "$INPUT" | jq -r '.stop_hook_active // false')

ROOT="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
cd "$ROOT"

# .cs 변경(수정/추가/미추적) 없으면 통과
[ -z "$(git status --porcelain -- '*.cs' 2>/dev/null)" ] && exit 0

COUNT_FILE="${UNITY_HOOK_STATE_DIR}/compile-gate-count"
COUNT=$(cat "$COUNT_FILE" 2>/dev/null || echo 0)
if [ "$STOP_ACTIVE" = "true" ] && [ "$COUNT" -ge 5 ]; then
    echo "compile-gate: 5회 연속 컴파일 실패 — 게이트를 해제한다. 사람이 확인해야 함." >&2
    echo 0 > "$COUNT_FILE"
    exit 0
fi

URL=${UNITY_MCP_URL:-$(awk '/^\[mcp_servers\.UnityMCP\]/{found=1;next} /^\[/{found=0} found && /^url[[:space:]]*=/{sub(/^[^=]*=[[:space:]]*"/,"");sub(/".*$/,"");print;exit}' .codex/config.toml)}
[ -z "$URL" ] && exit 0
H1='Content-Type: application/json'
H2='Accept: application/json, text/event-stream'

SID=$(curl -s -m 8 -D - -o /dev/null -X POST "$URL" -H "$H1" -H "$H2" \
    -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"compile-gate","version":"1"}}}' \
    | grep -i '^mcp-session-id:' | awk '{print $2}' | tr -d '\r' || true)
if [ -z "$SID" ]; then
    echo "compile-gate: Unity MCP($URL) 응답 없음 — 컴파일 게이트 건너뜀. Unity 에디터가 켜져 있는지 확인." >&2
    exit 0
fi

mcp() { # $1=timeout(s) $2=json body  → 첫 JSON 라인 (SSE 'data:' 프리픽스 제거)
    curl -s -m "$1" -X POST "$URL" -H "$H1" -H "$H2" -H "Mcp-Session-Id: $SID" -d "$2" \
        | sed 's/^data: //' | grep '^{' | head -1 || true
}
curl -s -m 5 -o /dev/null -X POST "$URL" -H "$H1" -H "$H2" -H "Mcp-Session-Id: $SID" \
    -d '{"jsonrpc":"2.0","method":"notifications/initialized"}' || true

mcp 90 '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"refresh_unity","arguments":{"mode":"if_dirty","compile":"request","wait_for_ready":true}}}' >/dev/null

RESP=$(mcp 30 '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"read_console","arguments":{"action":"get","types":["error"],"count":"40","format":"plain"}}}')
TEXT=$(echo "$RESP" | jq -r '.result.content[0].text // empty' 2>/dev/null || true)
LINES=$(echo "$TEXT" | jq -r '.data[]? // empty' 2>/dev/null || echo "$TEXT")
ERRS=$(echo "$LINES" | grep -E 'error CS[0-9]{4}' | sort -u | head -20 || true)

if [ -n "$ERRS" ]; then
    COUNT=$((COUNT + 1))
    echo "$COUNT" > "$COUNT_FILE"
    echo "" >&2
    echo "compile-gate: Unity 컴파일 에러 — 멈추지 말고 고칠 것 (${COUNT}/5)" >&2
    echo "$ERRS" >&2
    exit 2
fi

echo 0 > "$COUNT_FILE"
exit 0
