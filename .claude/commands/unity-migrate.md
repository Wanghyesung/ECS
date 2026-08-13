---
name: unity-migrate
description: "Unity 버전 또는 렌더 파이프라인 마이그레이션을 계획하고 실행합니다 — deprecated API, 패키지 호환성을 식별하고 마이그레이션 단계를 실행합니다."
user-invocable: true
args: migration_target
---

# /unity-migrate — 마이그레이션 어시스턴트

다음으로의 마이그레이션을 계획하고 실행합니다: **$ARGUMENTS**

## 워크플로우

`unity-migrator` 에이전트를 사용하여 다음을 수행합니다:

### 1단계: 현재 상태 평가
```
project_info resource → current Unity version, platform, packages
manage_packages action:"list" → all installed packages with versions
```

코드베이스를 스캔하여 다음을 찾습니다:
- Deprecated API 사용 (알려진 deprecated 메서드를 Grep으로 검색)
- 업데이트가 필요할 수 있는 플랫폼별 코드
- 오래된 include를 사용하는 셰이더 코드

### 2단계: 마이그레이션 계획 작성

단계별 계획을 제시합니다:
1. **백업** — git 브랜치 생성
2. **패키지 업데이트** — 어떤 패키지의 버전을 올려야 하는지
3. **API 변경** — 필요한 구체적인 코드 변경 사항 (기존 → 신규)
4. **셰이더 변경** — 렌더 파이프라인 마이그레이션인 경우
5. **머티리얼 변환** — 렌더 파이프라인 마이그레이션인 경우
6. **테스트** — 마이그레이션 후 무엇을 테스트해야 하는지

### 3단계: 실행 (사용자 승인 필요)

각 단계에 대해:
1. 변경을 적용합니다
2. `read_console`을 통해 콘솔을 확인합니다
3. 진행하기 전에 오류를 수정합니다
4. 진행 상황을 보고합니다

### 4단계: 검증

- `run_tests` MCP를 통해 모든 테스트를 실행합니다
- 콘솔에서 경고를 확인합니다
- 빌드가 여전히 성공하는지 확인합니다

## 일반적인 마이그레이션
- Unity 2021 → 2022 LTS
- Unity 2022 → Unity 6
- Built-in → URP
- Legacy Input → Input System
- Coroutines → UniTask
