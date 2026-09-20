#!/usr/bin/env bash
# ============================================================================
# _lib.sh — 훅 공용 라이브러리 (source 해서 씀)
#
#   DISABLE_UNITY_HOOKS=1     모든 훅 우회
#   DISABLE_HOOK_<NAME>=1     특정 훅 우회 (파일명 대문자, '-'→'_'. 예: DISABLE_HOOK_BLOCK_GIT_COMMIT=1)
#   UNITY_HOOK_MODE=warn      차단 훅을 경고로 강등 (exit 0)
# ============================================================================

[ "${DISABLE_UNITY_HOOKS:-}" = "1" ] && exit 0

_HOOK_BASENAME="$(basename "${BASH_SOURCE[1]}" .sh)"
_HOOK_ENV_NAME="DISABLE_HOOK_$(echo "$_HOOK_BASENAME" | tr '[:lower:]-' '[:upper:]_')"
[ "${!_HOOK_ENV_NAME:-}" = "1" ] && exit 0

_git_root="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
UNITY_HOOK_STATE_DIR="${UNITY_HOOK_STATE_DIR:-$_git_root/.codex/state}"
mkdir -p "$UNITY_HOOK_STATE_DIR"

# 차단 훅은 exit 2 대신 이 함수를 쓴다 (UNITY_HOOK_MODE=warn 이면 경고 후 통과)
unity_hook_block() {
    if [ "${UNITY_HOOK_MODE:-}" = "warn" ]; then
        echo "WARNING (downgraded from BLOCKED): $1" >&2
        exit 0
    fi
    echo "BLOCKED: $1" >&2
    exit 2
}
