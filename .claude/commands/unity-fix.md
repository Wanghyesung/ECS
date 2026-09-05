---
name: unity-fix
description: "Unity 버그를 진단하고 수정합니다 — 콘솔 오류를 읽고, 일반적인 원인을 확인하고, 타겟 수정을 적용하고, MCP를 통해 검증합니다."
user-invocable: true
args: bug_description
---

# /unity-fix — 버그 진단 및 수정

사용자가 설명한 문제를 수정합니다: **$ARGUMENTS**

## 에이전트 라우팅

- 기본값: `unity-fixer` 에이전트 사용(opus — 심층 조사)
- `$ARGUMENTS`에 `--quick`이 포함된 경우: `unity-fixer-lite` 에이전트 사용(sonnet — 명백한 수정용)
- 에이전트에 전달하기 전에 인자에서 `--quick` 플래그를 제거합니다.

## 워크플로

선택된 픽서 에이전트를 사용하여 다음을 수행합니다:

1. **증거 수집:**
   - `read_console` MCP를 통해 Unity 콘솔에서 오류, 경고, 스택 트레이스를 읽습니다.
   - 오류 메시지 또는 관련 코드에 대해 코드베이스를 검색합니다.
   - 사용자가 오류를 붙여넣은 경우, 파일명, 줄 번호, 오류 유형을 파싱합니다.

2. **진단** — 다음의 일반적인 Unity 원인들을 순서대로 확인합니다:
   - NullReferenceException → 누락된 참조, 파괴된 오브젝트, 실행 순서
   - Missing Script → 파일/클래스 이름 불일치, asmdef 문제
   - 직렬화 데이터 손실 → FormerlySerializedAs 없이 필드 이름 변경
   - 코루틴 중단 → SetActive(false) 또는 Destroy
   - 물리가 작동하지 않음 → 잘못된 레이어, 누락된 콜라이더/리지드바디
   - 빌드 실패 → 런타임에서의 UnityEditor, 플랫폼 정의

3. **수정** — 최소한의 타겟 수정을 적용합니다. 주변 코드는 리팩터링하지 않습니다.

4. **검증:**
   - `read_console`을 통해 콘솔을 확인합니다 — 오류가 사라졌어야 합니다.
   - 직렬화 문제였던 경우, 재구성이 필요할 수 있는 데이터에 대해 경고합니다.
   - 빌드 문제였던 경우, 검증을 위해 `/unity-build` 실행을 제안합니다.

5. 버그의 원인과 이 수정이 재발을 어떻게 방지하는지 **설명**합니다.
</content>
