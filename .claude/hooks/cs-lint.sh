#!/usr/bin/env bash
# ============================================================================
# cs-lint.sh — WARNING HOOK (PostToolUse: Edit|Write, *.cs 만)
# 편집된 조각만 보는 휴리스틱이라 경고만 내고 절대 차단하지 않는다.
# 컴파일 진짜 결과는 compile-gate.sh 가 본다.
#
# 출력: 경고가 있으면 stdout 에 hookSpecificOutput.additionalContext JSON.
#       (PostToolUse 에서 exit 0 + stderr 는 Claude 에게 전달되지 않는다)
#
# 스타일 검사(rules/csharp-unity.md)는 "이번에 추가된 줄"만 본다 —
# Edit 는 old_string 에 없던 줄, Write 는 HEAD 버전에 없던 줄.
# 그래야 기존 코드의 옛 스타일 때문에 경고가 쏟아지지 않는다.
# ============================================================================
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${SCRIPT_DIR}/_lib.sh"

INPUT=$(cat)
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')
case "$FILE_PATH" in *.cs) ;; *) exit 0 ;; esac

NEW=$(echo "$INPUT" | jq -r '.tool_input.new_string // .tool_input.content // empty' | tr -d '\r')
OLD=$(echo "$INPUT" | jq -r '.tool_input.old_string // empty' | tr -d '\r')
[ -z "$NEW" ] && exit 0
IS_WRITE=0; echo "$INPUT" | jq -e '.tool_input.content != null' >/dev/null 2>&1 && IS_WRITE=1

FP_UNIX="${FILE_PATH//\\//}"
IS_EDITOR=0; case "$FP_UNIX" in */Editor/*|*/editor/*) IS_EDITOR=1 ;; esac
IS_TEST=0;   case "$FP_UNIX" in *Test*|*/Tests/*) IS_TEST=1 ;; esac

# --- 새 파일 여부 + Write 의 비교 기준(HEAD 버전) ---
ROOT="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
IS_NEW_FILE=0
REL=$(cd "$ROOT" && git -c core.quotepath=false ls-files --full-name --error-unmatch -- "$FP_UNIX" 2>/dev/null || true)
[ -z "$REL" ] && IS_NEW_FILE=1
if [ "$IS_WRITE" -eq 1 ] && [ "$IS_NEW_FILE" -eq 0 ]; then
    OLD=$(cd "$ROOT" && git show "HEAD:$REL" 2>/dev/null | tr -d '\r' || true)
fi

ADDED=$(awk 'NR==FNR { seen[$0]=1; next } !($0 in seen)' <(printf '%s\n' "$OLD") <(printf '%s\n' "$NEW"))

W=""
warn() { W="${W}  - $1"$'\n'; }

# --- 직렬화: [SerializeField] 필드 이름 변경 시 FormerlySerializedAs 누락 (Edit 만) ---
if [ "$IS_WRITE" -eq 0 ] && [ -n "$OLD" ]; then
    OLD_FIELDS=$(echo "$OLD" | grep -oE '\[SerializeField\][^;=]*\s(\w+)\s*[;=]' | grep -oE '\w+\s*[;=]$' | tr -d ' ;=' || true)
    NEW_FIELDS=$(echo "$NEW" | grep -oE '\[SerializeField\][^;=]*\s(\w+)\s*[;=]' | grep -oE '\w+\s*[;=]$' | tr -d ' ;=' || true)
    for f in $OLD_FIELDS; do
        if ! echo "$NEW_FIELDS" | grep -qx "$f" && ! echo "$NEW" | grep -q "FormerlySerializedAs(\"$f\")"; then
            warn "[SerializeField] '$f' 이름 변경에 [FormerlySerializedAs(\"$f\")] 없음 → 모든 씬/프리팹/SO 값이 기본값으로 리셋됨 (rules/serialization.md)"
        fi
    done
fi

# --- 파일명 == MonoBehaviour/ScriptableObject 클래스명 ---
BASENAME=$(basename "$FP_UNIX" .cs)
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
    echo "$NEW" | grep -qE '\.tag\s*==\s*"'          && warn '.tag == "x" → CompareTag("x"), 가능하면 layermask'
    echo "$NEW" | grep -qE '\?\.(enabled|transform|gameObject|name|tag|GetComponent|SetActive)' && warn 'Unity 오브젝트에 ?. → 파괴 감지 우회. if (x != null) 로 (rules/unity-specifics.md)'
    echo "$NEW" | grep -qE '\b(StartCoroutine|IEnumerator|yield\s+return)\b' && [ "$IS_TEST" -eq 0 ] && warn "코루틴 → UniTask (프로젝트 필수 규칙)"
    echo "$NEW" | grep -qE 'new\s+WaitForSeconds\s*\('  && warn "new WaitForSeconds → UniTask.Delay"
    echo "$NEW" | grep -qE '\basync\s+void\b'            && warn "async void → async UniTaskVoid"
    echo "$NEW" | grep -qE '\b(SendMessage|BroadcastMessage)\s*\(' && warn "SendMessage/BroadcastMessage → 직접 참조 또는 R3 Subject"
    echo "$NEW" | grep -qE '\bevent\s+(System\.)?(Action|EventHandler|Func)\b' && warn "C# event → R3 Subject<T> 소유 + Observable<T> 노출 (rules/architecture.md, 2026-09-15 결정)"
    echo "$NEW" | grep -qE '\bInput\.(GetKey|GetKeyDown|GetKeyUp|GetAxis|GetAxisRaw|GetButton|GetButtonDown|GetMouseButton|mousePosition)\b' && warn "레거시 Input API → InputManager(New Input System) 경유"
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

# --- 새 파일: 헤더 블록 · sealed (rules/csharp-unity.md §6) ---
if [ "$IS_NEW_FILE" -eq 1 ]; then
    echo "$NEW" | grep -qE '^/\*///' || warn "헤더 블록 없음 → using 아래에 '/*/// 제목 / 기능 : ... *///' (csharp-unity.md §6)"
    if echo "$NEW" | grep -E '^[[:space:]]*(\[[^]]*\][[:space:]]*)*public[[:space:]]+class[[:space:]]' | grep -qvE '\b(abstract|static|sealed|partial)\b'; then
        warn "새 타입은 기본 sealed — 상속용 베이스가 아니면 'public sealed class' (csharp-unity.md §6)"
    fi
fi

# --- 스타일: 이번에 추가된 줄만 (rules/csharp-unity.md) ---
STYLE=$(printf '%s\n' "$ADDED" | awk '
BEGIN { SQ = sprintf("%c", 39) }
function trim(s) { sub(/^[ \t]+/, "", s); sub(/[ \t]+$/, "", s); return s }
function expected(t,    b) {
    b = t; gsub(/[ \t]/, "", b)
    if (b ~ /\[\]$/) return "arr"
    if (b ~ /^(List|NativeList)</) return "list"
    if (b ~ /^(Dictionary|HashSet)</) return "hash"
    if (b ~ /^Queue</) return "que"
    if (b ~ /^NativeArray</) return "arr"
    if (b ~ /^Subject</) return "subject"
    if (b ~ /^ReactiveProperty</) return "rp"
    if (b ~ /^PriorityQueue</) return "PQ"
    if (b == "float") return "f"
    if (b == "int") return "i"
    if (b == "bool") return "b"
    if (b == "long") return "l"
    if (b == "double") return "d"
    if (b == "string") return "str"
    if (b ~ /^Vector[234](Int)?$/) return "v"
    if (b == "Quaternion") return "q"
    if (b == "IDisposable") return "disposable"
    if (b == "DisposableBag") return "bag"
    if (b == "CancellationTokenSource") return "cts"
    if (b == "CancellationToken" || b == "Color" || b == "LayerMask" || b == "JobHandle" || b == "Rect" || b == "Bounds" || b == "Ray") return "t"
    if (b ~ /^e[A-Z]/) return "e"
    if (b ~ /^t[A-Z]/) return "t|ref"
    if (b ~ /^SO[A-Z]/) return "SO|ref"
    if (b == "Transform" || b == "GameObject" || b == "RectTransform" || b == "Rigidbody" || b == "Image") return "ref"
    return ""
}
function hasprefix(n, pl,    k, parts, p, c, j) {
    k = split(pl, parts, "|")
    for (j = 1; j <= k; ++j) {
        p = parts[j]
        if (n == p) return 1
        if (substr(n, 1, length(p)) != p) continue
        c = substr(n, length(p) + 1, 1)
        if (c ~ /[A-Z0-9]/) return 1
    }
    return 0
}
function out(msg) { if (!(msg in done)) { done[msg] = 1; print "  - " msg } }
{
    line = $0
    sub(/\/\/.*$/, "", line)
    if (line ~ /^[ \t]*$/) next

    if (line ~ /^목적[ \t]*:/) out("헤더 본문 라벨은 \"기능 :\" (\"목적 :\" 아님)")

    # 필드: private/protected 선언, 메서드·프로퍼티·const·event 제외
    if (line ~ /^[ \t]*(\[[^]]*\][ \t]*)*(private|protected)[ \t]/ && line !~ /=>/ && line !~ /[ \t](const|event)[ \t]/) {
        decl = line
        eq = index(decl, "="); if (eq > 0) decl = substr(decl, 1, eq - 1)
        sc = index(decl, ";"); if (sc > 0) decl = substr(decl, 1, sc - 1)
        if (decl !~ /[({]/) {
            isStaticRo = (decl ~ /static/ && decl ~ /readonly/)
            while (decl ~ /^[ \t]*\[[^]]*\]/) sub(/^[ \t]*\[[^]]*\][ \t]*/, "", decl)
            gsub(/(^|[ \t])(private|protected|internal|public|static|readonly|volatile)[ \t]/, " ", decl)
            gsub(/(^|[ \t])(private|protected|internal|public|static|readonly|volatile)[ \t]/, " ", decl)
            decl = trim(decl)
            nm = decl; sub(/^.*[ \t]/, "", nm)
            ty = decl; sub(/[ \t]+[A-Za-z_][A-Za-z0-9_]*$/, "", ty)
            gdecl = ty; gsub(/<[^<>]*>/, "<>", gdecl); gsub(/<[^<>]*>/, "<>", gdecl)
            if (nm != "" && ty != "" && ty != nm && gdecl !~ /,/) {
                if (nm !~ /^m_/) {
                    if (!(isStaticRo && nm ~ /^[A-Z]/)) out("필드 " SQ nm SQ " → m_ + 타입 접두사 (예: m_fSpeed, m_refTarget)")
                } else if (nm != "m_Instance") {
                    p = expected(gdecl)
                    if (p != "" && !hasprefix(substr(nm, 3), p)) { q = p; sub(/\|.*/, "", q); out("필드 " SQ nm SQ " (" ty ") → m_" q "... 접두사") }
                    if (gdecl == "Transform" && nm !~ /Tr$/) out("Transform 필드 " SQ nm SQ " → 이름 끝을 Tr 로 (예: m_refFireTr)")
                }
            }
        }
    }

    # 매개변수: 메서드/생성자 시그니처 (한 줄 선언만)
    if (line ~ /^[ \t]*(\[[^]]*\][ \t]*)*(public|private|protected|internal)[ \t]/ && line ~ /\(/) {
        head = substr(line, 1, index(line, "(") - 1)
        if (head !~ /=/) {
            rest = substr(line, index(line, "(") + 1)
            cl = index(rest, ")")
            if (cl > 0) {
                prm = substr(rest, 1, cl - 1)
                gsub(/<[^<>]*>/, "<>", prm); gsub(/<[^<>]*>/, "<>", prm)
                np = split(prm, ps, ",")
                for (k = 1; k <= np; ++k) {
                    pr = ps[k]
                    sub(/=.*$/, "", pr)
                    pr = trim(pr)
                    while (pr ~ /^\[[^]]*\]/) sub(/^\[[^]]*\][ \t]*/, "", pr)
                    sub(/^(this|ref|out|in|params)[ \t]+/, "", pr)
                    if (pr == "") continue
                    pn = pr; sub(/^.*[ \t]/, "", pn)
                    pt = pr; sub(/[ \t]+[A-Za-z_@][A-Za-z0-9_]*$/, "", pt)
                    if (pt == pn || pt == "") continue
                    if (pt ~ /^(Collider|Collider2D|Collision|Collision2D|PointerEventData|BaseEventData|AxisEventData)$/) continue
                    if (pn !~ /^_/) { out("매개변수 " SQ pn SQ " → _ + 타입 접두사 (예: _fSpeed, _refBB)"); continue }
                    p = expected(pt)
                    if (p != "" && !hasprefix(substr(pn, 2), p)) {
                        q = p; sub(/\|.*/, "", q)
                        if (pt == "CancellationToken") out("CancellationToken 매개변수 " SQ pn SQ " → _tToken")
                        else out("매개변수 " SQ pn SQ " (" pt ") → _" q "... 접두사")
                    }
                }
            }
        }
    }

    if (line ~ /![A-Za-z_][A-Za-z0-9_.]*(\[[^]]*\])?[ \t]*(\)|&&|\|\|)/)
        out("bool 부정은 " SQ "== false" SQ " 로 명시 (" SQ "!" SQ " 는 TryGetValue/ContainsKey 같은 메서드 호출에만)")
    if (line ~ /;[ \t]*[A-Za-z_][A-Za-z0-9_]*\+\+[ \t]*\)/)
        out("for 증가는 전위 " SQ "++i" SQ)
    if (line ~ /^[ \t]*(else[ \t]+)?(if|for|foreach|while)[ \t]*\(.*\)[ \t]*[A-Za-z_].*;[ \t]*$/ && line !~ /\{/)
        out("한 줄 if/for 본문은 다음 줄에 들여쓴다 (조건과 같은 줄 금지): " trim(line))
    if (line ~ /^[ \t]*else[ \t]+[A-Za-z_].*;[ \t]*$/ && line !~ /^[ \t]*else[ \t]+if[ \t(]/)
        out("else 본문은 다음 줄에 들여쓴다: " trim(line))
    if (line ~ /^[ \t]*(else[ \t]+)?(if|else|for|foreach|while)([ \t(].*)?\{[^{}]*;[ \t]*\}[ \t]*$/)
        out("한 줄 블록 { ...; } 금지 → 중괄호를 여러 줄로: " trim(line))
    if (line ~ /static[ \t]+[A-Za-z_][A-Za-z0-9_<>]*[ \t]+m_Instance[ \t]*\{/)
        out("싱글톤은 " SQ "public static T m_Instance = null;" SQ " 필드 형태 (architecture.md)")
    if (line ~ /CreateAssetMenu[ \t]*\(/ && (line !~ /fileName[ \t]*=[ \t]*"SO_/ || line !~ /menuName[ \t]*=[ \t]*"Game\//))
        out("[CreateAssetMenu(fileName = \"SO_<이름>\", menuName = \"Game/<분류>/<이름>\")] 형식")
}' | head -30)
[ -n "$STYLE" ] && W="${W}${STYLE}"$'\n'

if [ -n "$W" ]; then
    MSG="cs-lint 경고 ($FP_UNIX) — 규칙: .claude/rules/csharp-unity.md 외"$'\n'"$W"
    jq -n --arg ctx "$MSG" '{hookSpecificOutput: {hookEventName: "PostToolUse", additionalContext: $ctx}}'
fi
exit 0
