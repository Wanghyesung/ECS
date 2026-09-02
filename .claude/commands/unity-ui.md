---
name: unity-ui
description: "UI 화면을 구축합니다 — 코드를 작성하고 MCP를 통해 시각적 계층 구조를 설정합니다. UGUI Canvas와 UI Toolkit을 모두 지원합니다."
user-invocable: true
args: screen_description
---

# /unity-ui — UI 화면 구축

다음을 기반으로 UI 화면을 구축합니다: **$ARGUMENTS**

## 작업 흐름

`unity-ui-builder` 에이전트를 사용하여 다음을 수행합니다:

1. **UI 시스템 선택** — 프로젝트 컨텍스트에 따라 UGUI(Canvas) 또는 UI Toolkit
2. **레이아웃 계획** — 요소, 계층 구조, 상호작용, 스타일링을 식별합니다
3. **스크립트 작성:**
   - UGUI: `[SerializeField]` Button/Text/Image 참조를 가진 MonoBehaviour
   - UI Toolkit: UXML 문서 + USS 스타일시트 + 컨트롤러 스크립트
4. **시각적 계층 구조 구축** — MCP를 통해:
   - UGUI: `manage_ui` + `manage_gameobject`를 통한 Canvas, 패널, 버튼, 텍스트
   - UI Toolkit: UXML/USS 파일 작성, UIDocument 컴포넌트 부착
5. **상호작용 연결** — 버튼 클릭, 입력 필드, 토글
6. **검증** — `read_console`를 통해

## UGUI 성능 규칙
- 상호작용하지 않는 요소는 Raycast Target을 비활성화합니다
- 정적/동적 콘텐츠를 별도의 Canvas로 분리합니다
- 스크롤 뷰 안에서는 Layout Group 사용을 피합니다

화면 구조, 생성된 스크립트, 테스트 방법을 보고합니다.
