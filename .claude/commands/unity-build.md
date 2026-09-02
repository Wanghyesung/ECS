---
name: unity-build
description: "MCP를 통해 Unity 빌드를 구성하고 트리거합니다 — 플랫폼 설정, 씬, 플레이어 설정을 처리하고 빌드 진행 상황을 모니터링합니다."
user-invocable: true
args: platform
---

# /unity-build — 프로젝트 빌드

지정된 플랫폼에 대한 빌드를 구성하고 트리거합니다.

## 대상

사용자가 플랫폼을 지정한 경우: **$ARGUMENTS**용으로 빌드합니다.
플랫폼이 지정되지 않은 경우: 어떤 플랫폼으로 빌드할지 사용자에게 묻습니다.

## 워크플로

`unity-build-runner` 에이전트를 사용하여 다음을 수행합니다:

### 1단계: 빌드 전 점검

1. `read_console`을 통해 **콘솔을 확인**합니다 — 컴파일 오류가 있으면 중단합니다.
2. `project_info` 리소스를 통해 **프로젝트 정보를 확인**합니다 — 현재 플랫폼과 Unity 버전.
3. **빌드 씬을 검증**합니다 — 빌드 목록에 있는 모든 씬이 존재하는지 확인합니다.
4. **코드 품질 점검을 실행**합니다 — `UnityEditor` 네임스페이스 누출이 있는지 확인합니다.

### 2단계: 플랫폼 구성

필요한 경우 `manage_build`를 통해 플랫폼을 전환합니다:
- 플레이어 설정(회사명, 제품명, 버전, 번들 ID) 설정
- 플랫폼별 설정 구성:
  - **Android**: API 레벨, IL2CPP, ARM64, 키스토어, AAB 포맷
  - **iOS**: 서명, 번들 ID, 최소 버전, 대상 기기

### 3단계: 빌드

```
manage_build action:"build" → 빌드 트리거
read_console → 진행 상황 모니터링 및 오류 포착
```

### 4단계: 보고

- 빌드 결과: SUCCESS 또는 FAILURE
- 빌드 크기(로그에서 확인 가능한 경우)
- 빌드 로그의 경고
- 실패한 경우: 오류 상세 내용 및 제안된 수정 방법
- 다음 단계: 빌드 출력을 찾을 수 있는 위치

## 일반적인 빌드 수정 방법

| 오류 | 수정 방법 |
|-------|-----|
| `UnityEditor` 네임스페이스 | `#if UNITY_EDITOR` 가드 추가 |
| 타입/어셈블리 누락 | `.asmdef` 참조 확인 |
| 스트리핑으로 코드가 제거됨 | `link.xml`에 항목 추가 |
| 빌드 용량이 너무 큼 | `/unity-optimize` 또는 `analyze-build-size.sh` 실행 |
</content>
</invoke>
<parameter name="file_path">/mnt/user-data/outputs/ECS/.claude/commands/unity-doctor.md