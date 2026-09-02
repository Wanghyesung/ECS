---
name: unity-audit
description: "전체 프로젝트 상태 점검 — 메타 파일 무결성, 누락된 참조, 어셈블리 정의 그래프, 코드 품질 스캔, 씬 계층 구조 감사."
user-invocable: true
---

# /unity-audit — 전체 프로젝트 상태 점검

Unity 프로젝트에 대한 종합적인 감사를 실행합니다.

## 점검 항목

### 1. 메타 파일 무결성
`.claude/scripts/validate-meta-integrity.sh --all`을 실행합니다:
- 모든 애셋에 `.meta` 파일이 존재하는지
- 고아 `.meta` 파일이 없는지
- 중복된 GUID가 없는지

### 2. 누락된 참조
`.claude/scripts/detect-missing-refs.sh`를 실행합니다:
- 씬/프리팹의 깨진 스크립트 참조
- 누락된 애셋 GUID
- null 직렬화 참조

### 3. 어셈블리 정의 그래프
`.claude/scripts/validate-asmdefs.sh`를 실행합니다:
- 순환 종속성이 없는지
- Editor/Runtime이 제대로 분리되어 있는지
- 모든 스크립트가 asmdef에 포함되어 있는지

### 4. 코드 품질
`.claude/scripts/validate-code-quality.sh`를 실행합니다:
- Update에서의 GetComponent/Camera.main 사용
- 게임플레이 코드 내 LINQ 사용
- 핫 패스에서의 할당(allocation)
- CompareTag 사용
- 프로덕션 코드 내 Debug.Log

### 5. 콘솔 오류
`read_console` MCP를 통해:
- 컴파일 오류
- 런타임 경고
- 사용 중단(deprecation) 알림

## 출력

상태 점검 카드를 제시합니다:
```
Meta Integrity:    PASS / X issues
Missing Refs:      PASS / X broken
Assembly Graph:    PASS / X issues
Code Quality:      PASS / X warnings
Console:           PASS / X errors
```

그런 다음 심각도(critical → warning → info) 순으로 그룹화하여 모든 문제를 파일 위치 및 수정 제안과 함께 나열합니다.
</content>
</invoke>
