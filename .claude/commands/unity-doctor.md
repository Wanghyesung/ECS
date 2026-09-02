---
name: unity-doctor
description: "진단 상태 점검 — MCP 연결, .claude/ 무결성, Unity 프로젝트 구조, 훅 등록 여부를 확인합니다."
user-invocable: true
---

# /unity-doctor — 진단 상태 점검

everything-claude-unity 설치와 Unity 프로젝트에 대한 종합적인 진단 점검을 실행합니다. 각 점검 항목을 **PASS**, **WARNING**, **ERROR**로 보고하고 실행 가능한 수정 방법을 함께 제시합니다.

## 점검 1: Unity MCP 서버 연결

1. MCP를 통해 `project_info`를 호출하여 Unity 버전과 프로젝트 상태를 가져옵니다.
2. 성공한 경우: Unity 버전, 플랫폼, 플레이 모드 상태를 보고 → **PASS**
3. 실패한 경우: 오류를 보고 → **ERROR**와 함께 다음을 제안합니다:
   - Unity에 unity-mcp 패키지가 설치되어 있는가?
   - Unity 에디터가 실행 중이고 프로젝트가 열려 있는가?
   - MCP 서버가 예상된 포트에서 실행 중인가?
   - `.claude/settings.json` → `mcpServers.unityMCP.url`을 확인하세요.

## 점검 2: .claude/ 디렉토리 무결성

1. 예상되는 디렉토리가 존재하는지 확인합니다: `commands/`, `agents/`, `hooks/`, `skills/`, `rules/`
2. `hooks/`의 각 훅에 대해: 파일이 존재하고 실행 가능한지(`-x` 권한) 확인합니다.
3. `commands/`의 각 커맨드에 대해: `name`과 `description`이 포함된 YAML 프론트매터가 있는지 확인합니다.
4. `agents/`의 각 에이전트에 대해: `name`, `description`, `model`, `tools`가 포함된 YAML 프론트매터가 있는지 확인합니다.
5. `.claude/VERSION` 파일이 존재하는지 확인하고 버전 번호를 보고합니다.
6. 개수를 보고합니다: X개 커맨드, Y개 에이전트, Z개 훅, W개 스킬, V개 규칙
7. 누락된 디렉토리가 있으면 → **ERROR**; 유효하지 않은 프론트매터가 있으면 → **WARNING**; 모두 정상이면 → **PASS**

## 점검 3: 훅 등록 완전성

1. `.claude/settings.json`을 읽습니다.
2. `hooks/` 디렉토리의 모든 `.sh` 파일(`_lib.sh` 제외)에 대해:
   - settings.json의 `PreToolUse` 또는 `PostToolUse` 중 하나에 등록되어 있는지 확인합니다.
   - 등록되지 않은 훅을 보고 → **WARNING**
3. settings.json의 각 훅 경로에 대해:
   - 참조된 파일이 존재하는지 확인합니다.
   - 누락된 파일을 보고 → **ERROR**
4. 차단 훅(`block-*.sh`)이 `PreToolUse`에, 경고 훅(`warn-*.sh`, `validate-*.sh`, `suggest-*.sh`)이 `PostToolUse`에 있는지 확인합니다.
5. 모두 올바르면 → **PASS**

## 점검 4: Unity 프로젝트 구조

1. `Assets/` 디렉토리가 있는지 확인 → 없으면 **ERROR**
2. `ProjectSettings/` 디렉토리가 있는지 확인 → 없으면 **ERROR**
3. `Packages/manifest.json`이 있는지 확인 → 없으면 **WARNING**
4. 프로젝트 루트에 `CLAUDE.md`가 있는지 확인 → 없으면 **WARNING**, `/unity-init` 제안
5. `Assets/`에서 `.asmdef` 파일을 검색 → 없으면 **WARNING**
6. 테스트 어셈블리 정의(`*Tests*.asmdef`)를 검색 → 없으면 **WARNING**, `/unity-test` 제안
7. 모두 존재하면 → **PASS**

## 점검 5: 스킬/패키지 정합성

1. `Packages/manifest.json`을 읽어 설치된 Unity 패키지를 감지합니다.
2. `.claude/skills/`의 사용 가능한 스킬과 대조합니다:

| 패키지 | 예상 스킬 |
|---------|---------------|
| `com.unity.inputsystem` | `systems/input-system` |
| `com.unity.addressables` | `systems/addressables` |
| `com.unity.cinemachine` | `systems/cinemachine` |
| `com.unity.render-pipelines.universal` | `systems/urp-pipeline` |
| `com.unity.textmeshpro` | `third-party/textmeshpro` |
| `com.unity.timeline` | — (아직 스킬 없음) |

3. `Assets/`에 있는 서드파티 패키지도 확인합니다:
   - `DOTween` → `third-party/dotween`
   - `UniTask` → `third-party/unitask`
   - `VContainer` → `third-party/vcontainer`
   - `Odin` → `third-party/odin-inspector`

4. 일치하는 스킬이 없는 패키지를 보고 → **WARNING**(역량 공백)
5. 모두 정합하면 → **PASS**

## 출력 형식

요약 보고서를 제시합니다:

```
=== Unity Doctor Report ===

MCP Server:        PASS  (Unity 2022.3.20f1, StandaloneWindows64)
.claude/ Integrity: PASS  (17 commands, 14 agents, 9 hooks, 35 skills, 5 rules)
Hook Registration:  PASS  (all hooks registered correctly)
Project Structure:  WARNING — no test assembly definitions found
Skill Alignment:    WARNING — DOTween detected but no matching skill loaded

Overall: 2 warnings, 0 errors
```

WARNING 또는 ERROR 항목마다 해당 줄 바로 뒤에 실행 가능한 수정 방법을 포함합니다.
</content>
