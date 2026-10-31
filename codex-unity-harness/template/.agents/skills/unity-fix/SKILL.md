---
name: unity-fix
description: "Unity 버그 진단·수정 — 콘솔 오류를 읽고 근본 원인을 추적해 최소 수정 후 MCP로 검증합니다."
---

# $unity-fix — 버그 진단 및 수정

대상: **사용자 요청의 인자**

이 세션이 직접 수행한다 (서브에이전트 없음).

1. **증거 수집** — `read_console`(error/warning + stacktrace), 사용자가 붙여넣은 에러의 파일/줄/유형 파싱, 관련 코드 Grep. 씬 인스턴스가 관련되면 프리팹만 보지 말고 MCP `find_gameobjects`/`manage_components`로 실제 씬을 본다.
2. **근본 원인** — AGENTS.md 규칙: "오류가 있다"로 끝내지 말고 (1) 왜 발생하는지 코드/씬 구조까지 추적 (2) 실제 문제를 구체적으로 짚고 (3) 해결 방법 제시. 흔한 원인 순서: NullReference(미할당/파괴/실행 순서) → Missing Script(파일명≠클래스명) → 직렬화 손실(FormerlySerializedAs) → 물리(레이어/콜라이더) → 빌드(UnityEditor 누출, 플랫폼 define). 증상만 고치지 말고 **모든 호출자를 grep**해서 공유 지점 하나를 고친다.
3. **수정** — 최소한의 타겟 수정. 주변 리팩터링 금지. 고치지 않기로 한 진단은 `.codex/docs/known-issues.md`에 기록(사용자 승인 후 반영).
4. **검증** — `refresh_unity(compile: request)` → `read_console` 에러 0. 직렬화 문제였으면 재설정이 필요한 데이터 경고, 빌드 문제였으면 `$unity-build` 제안.
5. **설명** — 원인과 이 수정이 재발을 막는 이유.
