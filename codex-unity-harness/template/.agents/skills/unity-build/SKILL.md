---
name: unity-build
description: "MCP manage_build로 Unity 빌드를 트리거하고 결과를 보고합니다. 플랫폼 미지정 시 Windows."
---

# $unity-build — 프로젝트 빌드

플랫폼: **사용자 요청의 인자** (없으면 Windows — 주 타겟. 부 타겟 Android)

이 세션이 직접 수행한다.

1. **빌드 전 점검** — `refresh_unity(compile: request)` → `read_console(types: error)` 에러 있으면 중단. `mcpforunity://project/info`로 현재 플랫폼 확인. 빌드 씬 목록(`EditorBuildSettings`) 존재 확인.
2. **빌드** — `manage_build`로 플랫폼 전환(필요 시) + 빌드. Addressables 컨텐츠 빌드가 먼저 필요하면 그것부터.
3. **보고** — SUCCESS/FAILURE, 경고, 출력 경로. 실패 시 원인·수정 제안.

| 흔한 오류 | 수정 |
|---|---|
| `UnityEditor` 네임스페이스 | `#if UNITY_EDITOR` 가드 |
| 타입/어셈블리 누락 | `.asmdef` 참조 |
| 스트리핑으로 코드 제거 | `link.xml` |
| 빌드에서만 풀 오브젝트 안 나옴 | `known-issues.md`의 Addressables 중복 항목 참고 |
