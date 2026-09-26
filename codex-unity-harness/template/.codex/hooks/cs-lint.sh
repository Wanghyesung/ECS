#!/usr/bin/env bash
# ============================================================================
# cs-lint.sh — WARNING HOOK (PostToolUse: Edit|Write, *.cs 만)
# 예전 quality-gate / warn-serialization / warn-filename / warn-platform-defines
# 4개를 프로세스 1개로 병합. 편집된 조각(new_string/content)만 보는 휴리스틱이라
# 경고만 내고 절대 차단하지 않는다. 컴파일 진짜 결과는 compile-gate.sh 가 본다.
# ============================================================================
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${SCRIPT_DIR}/_lib.sh"

INPUT=$(cat)
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')
case "$FILE_PATH" in *.cs) ;; *) exit 0 ;; esac

NEW=$(echo "$INPUT" | jq -r '.tool_input.new_string // .tool_input.content // empty')
OLD=$(echo "$INPUT" | jq -r '.tool_input.old_string // empty')
[ -z "$NEW" ] && exit 0

IS_EDITOR=0; case "$FILE_PATH" in */Editor/*|*/editor/*) IS_EDITOR=1 ;; esac
IS_TEST=0;   case "$FILE_PATH" in *Test*|*/Tests/*) IS_TEST=1 ;; esac
W=""
warn() { W="${W}  - $1\n"; }

# --- 직렬화: [SerializeField] 필드 이름 변경 시 FormerlySerializedAs 누락 (Edit 만) ---
if [ -n "$OLD" ]; then
    OLD_FIELDS=$(echo "$OLD" | grep -oE '\[SerializeField\][^;=]*\s(\w+)\s*[;=]' | grep -oE '\w+\s*[;=]$' | tr -d ' ;=' || true)
    NEW_FIELDS=$(echo "$NEW" | grep -oE '\[SerializeField\][^;=]*\s(\w+)\s*[;=]' | grep -oE '\w+\s*[;=]$' | tr -d ' ;=' || true)
    for f in $OLD_FIELDS; do
        if ! echo "$NEW_FIELDS" | grep -qx "$f" && ! echo "$NEW" | grep -q "FormerlySerializedAs(\"$f\")"; then
            warn "[SerializeField] '$f' 이름 변경에 [FormerlySerializedAs(\"$f\")] 없음 → 모든 씬/프리팹/SO 값이 기본값으로 리셋됨 (rules/serialization.md)"
        fi
    done
fi

# --- 파일명 == MonoBehaviour/ScriptableObject 클래스명 ---
BASENAME=$(basename "$FILE_PATH" .cs)
if echo "$NEW" | grep -qE ':\s*(MonoBehaviour|ScriptableObject|NetworkBehaviour|StateMachineBehaviour)\b' \
   && ! echo "$NEW" | grep -qE "(class|struct)\s+$BASENAME\b"; then
    CLS=$(echo "$NEW" | grep -oE 'class\s+\w+\s*:\s*(MonoBehaviour|ScriptableObject)' | head -1 | awk '{print $2}')
    [ -n "$CLS" ] && warn "파일명 '$BASENAME.cs' ≠ 클래스명 '$CLS' → Unity가 컴포넌트로 인식 못 함"
fi

# --- 런타임 코드 품질 (Editor/ 제외) ---
if [ "$IS_EDITOR" -eq 0 ]; then
    if echo "$NEW" | grep -qE 'void\s+(Update|FixedUpdate|LateUpdate)\s*\(' \
       && echo "$NEW" | grep -qE '\b(GetComponent|TryGetComponent|FindObjectOfType|FindObjectsOfType|FindAnyObjectByType|Camera\.main)\b'; then
        warn "Update 계열 근처에 GetComponent/FindObjectOfType/Camera.main → Awake()에서 캐싱 (rules/performance.md)"
    fi
    if [ "$IS_TEST" -eq 0 ] && echo "$NEW" | grep -qE '\.(Where|Select|Any|All|First|FirstOrDefault|OrderBy|GroupBy|ToList|ToArray|ToDictionary)\s*\('; then
        warn "게임플레이 코드에 LINQ → for 루프로 (GC alloc)"
    fi
    echo "$NEW" | grep -qE '\.tag\s*==\s*"'          && warn '.tag == "x" → CompareTag("x")'
    echo "$NEW" | grep -qE '\?\.(enabled|transform|gameObject|name|tag|GetComponent|SetActive)' && warn 'Unity 오브젝트에 ?. → 파괴 감지 우회. if (x != null) 로 (rules/unity-specifics.md)'
    echo "$NEW" | grep -qE '\basync\s+void\b'            && warn "async void 사용은 예외 처리와 Unity 생명주기를 확인"
    echo "$NEW" | grep -qE '\b(SendMessage|BroadcastMessage)\s*\(' && warn "SendMessage/BroadcastMessage → 직접 참조 또는 event Action<T>"
    echo "$NEW" | grep -qE '\bInput\.(GetKey|GetKeyDown|GetKeyUp|GetAxis|GetAxisRaw|GetButton|GetButtonDown|GetMouseButton|mousePosition)\b' && warn "레거시 Input API 사용: 설치된 입력 패키지 및 프로젝트 규칙 확인"
    if echo "$NEW" | grep -qE '\.material\s*[.=]' && ! echo "$NEW" | grep -qE '\.sharedMaterial'; then
        warn ".material 은 머티리얼을 복제해 배칭을 깨뜨림 → .sharedMaterial 또는 MaterialPropertyBlock"
    fi
    if echo "$NEW" | grep -qE 'Debug\.(Log|LogWarning|LogError)\s*\(' \
       && ! echo "$NEW" | grep -qE '#if\s+(UNITY_EDITOR|DEBUG|DEVELOPMENT_BUILD)|\[Conditional\s*\('; then
        warn "Debug.Log 가 프로덕션에 남음 → #if UNITY_EDITOR 또는 [Conditional(\"UNITY_EDITOR\")] 래퍼"
    fi
    if echo "$NEW" | grep -qE '(using\s+UnityEditor|UnityEditor\.)' && ! echo "$NEW" | grep -qE '#if\s+UNITY_EDITOR'; then
        warn "UnityEditor 사용에 #if UNITY_EDITOR 가드 없음 → 빌드 실패 (guard-editor-runtime 이 차단했어야 함)"
    fi
fi

# --- 플랫폼 define 에 #else 없음 ---
PD='UNITY_ANDROID|UNITY_IOS|UNITY_WEBGL|UNITY_STANDALONE_WIN|UNITY_STANDALONE_OSX|UNITY_STANDALONE_LINUX'
IF_N=$(echo "$NEW" | grep -cE "#if\s+($PD)" || true)
if [ "$IF_N" -gt 0 ]; then
    ELSE_N=$(echo "$NEW" | grep -cE '#(else|elif)' || true)
    [ "$IF_N" -gt "$ELSE_N" ] && warn "플랫폼 #if 에 #else 폴백 없음 → 다른 플랫폼에서 코드가 조용히 빠짐"
fi

if [ -n "$W" ]; then
    echo "" >&2
    echo "cs-lint: $FILE_PATH" >&2
    echo -e "$W" >&2
fi
exit 0
