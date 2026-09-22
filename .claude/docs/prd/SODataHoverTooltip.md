---
status: in-progress
artifact: https://claude.ai/artifact/JTTUnfqeD26oYKcZ39B3p3
---

# SODataHoverTooltip — SlotView 호버 설명창 (Container · Joker 후보/선택 · 장비 슬롯)

2026-09-22 승인·구현. 이전 `.codex/docs/prd/SODataHoverTooltip.md`(2026-09-20)는 autopilot 이 읽지 않는 경로라 미실행 — 이 문서로 대체.

## 1. Overview

- **Purpose:** 마우스가 `SOData` 를 든 UI 위에 올라가면 아이콘 + `Description` 을 그 옆에 띄운다.
- **Scope:** `Container`/`Interface` 가 만드는 모든 `SlotView`(Battle FeatContainer, Joker Select/Pick, 로비 Inven/Stat/Shop, 장비 슬롯). **SlotView 전용** — 레벨업 `RandomFeatureCard` 는 2026-09-22 사용자 결정으로 제외(BaseButtonUI 에 훅을 두면 모든 버튼이 불필요한 상태를 갖게 됨).
- **Tech:** UGUI EventSystem(`IPointerEnter/ExitHandler`), `CanvasGroup`, `RectTransformUtility`. Update 없음, hover 당 힙 할당 0.
- **결정:** hover 감지 = `SlotView` 자체(`OnPointerEnter/Exit` override + `OnDisable`) / 위치 = 슬롯 오른쪽 고정(가장자리면 왼쪽) / 내용 = 기존 `DataDescUI` 재사용 / EditMode 테스트 없음 / 소유자 가드·카메라 변환 없음(같은 Overlay 캔버스 전제, 사용자 결정).

## 2. User Flow

1. 데이터 있는 슬롯에 마우스 진입 → 오른쪽 8px 에 창.
2. 오른쪽이 모자라면 왼쪽으로 뒤집고, 위아래는 Canvas 안으로 clamp.
3. 이탈 / UI SetActive(false) / 빈 데이터로 재바인딩 → 닫힘.
4. 빈 슬롯 → 열리지 않음.

## 3. Functional Requirements

| # | 요구 | Rationale |
|---|---|---|
| F1 | 설명창 인스턴스 없음(2D 씬) 또는 `SOData == null` 이면 예외 없이 무시 | `m_Instance == null` 가드 하나로 씬별 분기 0 |
| F2 | 한 번에 창 하나, 새 대상 진입 시 즉시 교체 | Unity 는 Exit(A)→Enter(B) 순서라 인스턴스 하나로 충분 |
| F3 | `OnDisable` 은 `m_bPointerInside` 인 슬롯만 닫음 | 소유자 필드 없이도 다른 패널 닫힘이 현재 창을 지우지 않게 |
| F4 | 창은 raycast 를 막지 않음 | clamp 로 커서 밑에 깔리면 Enter/Exit 무한 깜빡임 |
| F5 | Update/FixedUpdate/LateUpdate 사용 금지, 코너 버퍼 재사용 | performance.md 황금 규칙 |
| F6 | `SlotView.BindData` 가 hover 중인 슬롯의 데이터를 바꾸면 갱신/닫기 | 드래그 스크롤·정렬·Joker Pick 삭제는 슬롯 오브젝트를 재사용 |
| F7 | SetActive(false) 로 사라지는 UI 는 `OnDisable` 에서 닫음 | `ExecuteEvents` 는 비활성 오브젝트에 PointerExit 를 보내지 않음 |
| NFR | 마우스 우선. Android 길게 누르기는 범위 밖 | 주 타겟 Windows |

## 4. Technical Architecture

코드·STEP·근거 3줄은 artifact 에 있다. 요약:

- `SODataTooltipView` (신규, sealed) — private static `m_Instance`, static `Show(data, sourceRect)` / `Hide()`, `Place()` 가 슬롯 월드 코너를 `m_refCanvasRect.InverseTransformPoint` 로 캔버스 로컬로 옮겨 오른쪽/왼쪽/clamp. `[RequireComponent(CanvasGroup, DataDescUI)]`. 슬롯과 같은 루트 캔버스 아래에 있어야 함.
- `SlotView` (수정) — `m_bPointerInside`, `OnPointerEnter/Exit` override(base 호출 후 Show/Hide), `OnDisable`(hover 중일 때만 Hide), `BindData` 끝 `RefreshTooltip()`.
- `BaseButtonUI` / `RandomFeatureCard` — 변경 없음(845f2a0 의 훅은 되돌림).

```mermaid
sequenceDiagram
    participant M as 마우스 / EventSystem
    participant S as SlotView
    participant T as SODataTooltipView
    participant D as DataDescUI
    M->>S: OnPointerEnter(e)
    S->>S: m_bPointerInside = true → RefreshTooltip()
    S->>T: Show(m_SOTargetSO, rect)
    T->>D: Show(data)
    T->>T: Place() 코너→Canvas 로컬, 뒤집기, clamp
    S->>S: BindData 끝 → RefreshTooltip() (null 이면 Hide)
    M->>S: OnPointerExit / OnDisable(hover 중일 때만)
    S->>T: Hide()
```

```mermaid
stateDiagram-v2
    [*] --> Hidden : Awake
    Hidden --> Shown : Show(A)
    Shown --> Shown : Show(B) 즉시 교체 (Unity 는 Exit(A) 후 Enter(B))
    Shown --> Hidden : Hide() Exit / hover 중 OnDisable / null 재바인딩
```

## 5. Assumptions & Constraints

- BattleScene 의 CardCreator · FeatWindow · JokerContainers 와 LobyScene 의 컨테이너 4개가 각각 `PopupCanvas` / `PopUpCanvas`(둘 다 Overlay) 아래 → 씬당 프리팹 1개, 팝업 캔버스 마지막 자식.
- `SlotView` 는 `OnDisable` 을 새로 가짐(private). 상위 `BaseButtonUI` 에 `OnDisable` 없음.
- 슬롯과 툴팁이 **같은 루트 캔버스**(Overlay) 아래라는 전제 — 다른 캔버스/ScreenSpaceCamera 에 두면 위치가 어긋남.
- 씬/프리팹은 YAML 직접 편집 금지 — UnityMCP 또는 에디터.

## 6. Implementation Plan

| 마일스톤 | 내용 | DoD |
|---|---|---|
| M1 코드 | `.cs` 2개 — `SODataTooltipView`(신규) · `SlotView`(수정) | 컴파일 에러 0 |
| M2 Unity 배선 | `Assets/3D/11_UI/Item/SODataTooltip.prefab` 생성, BattleScene `PopupCanvas` · LobyScene `PopUpCanvas` 마지막 자식으로 배치 | 두 씬에서 hover 시 창 표시 |
| M3 검증 | 아래 Acceptance 전부 + `unity-reviewer` | `status: done` |

## 7. Acceptance Criteria (BDD)

```
Given FeatWindow 열림, 데이터 든 SlotView   When 마우스 진입              Then 슬롯 오른쪽 8px 에 아이콘 + Description
Given 빈 SlotView                           When 마우스 진입              Then 창 없음
Given Joker 후보 슬롯 위에 창                When 클릭해 Pick 으로 이동    Then 재정렬된 데이터로 갱신 또는 닫힘, 잔상 없음
Given 창 떠 있음                             When CardCreator.Close()/PickData()  Then 창 닫힘
Given 화면 오른쪽 끝 슬롯                    When 마우스 진입              Then 왼쪽으로 뒤집혀 캔버스 안
Given Container 드래그 스크롤 중             When 행이 바뀜                Then 커서 아래 슬롯의 새 데이터로 갱신
```

## 8. Prefab Spec (M2)

```
SODataTooltip   RectTransform(pivot 0,1 · width 320) · Image(Sci-Fi 배경 icon_btn, raycastTarget OFF)
                CanvasGroup · DataDescUI · SODataTooltipView
                VerticalLayoutGroup(padding 16, spacing 8) · ContentSizeFitter(Vertical = Preferred)
├─ ItemSlot     Image(슬롯 프레임, raycastTarget OFF, preserveAspect) · LayoutElement(min/preferred 250×250)
│  └─ Icon      Image · raycastTarget OFF · preserveAspect · 200×200 중앙
└─ Desc         TextMeshProUGUI(zekton free SDF, 20) · raycastTarget OFF · Wrapping On · LayoutElement(minHeight 500)
DataDescUI      m_refImage → Icon · m_refDescTex → Desc · m_refOriginSprite 비움
→ 높이 ≈ 16+250+8+500+16 = 790 (2026-09-22 사용자 요청: 슬롯 250 정사각 + 설명 500)
```

## 9. Out of Scope

Android 길게 누르기 · 커서 추적 · 페이드 연출 · SO 이름/희귀도 필드 · BattleScene_2/MainScene/2D 씬 배치 · `guard-editor-runtime.sh` 백슬래시 경로 수정(별도).

## Changelog

- 2026-09-22 — 승인, M1 코드 + M2 프리팹·두 씬 배치 완료(845f2a0, b4fb9c8). LobyScene 플레이 모드에서 Enter/Exit/OnDisable/빈 슬롯/교체 검증.
- 2026-09-22 — 범위 축소: SlotView 전용으로 이동, BaseButtonUI·RandomFeatureCard 훅 제거, 소유자 가드·카메라 변환 제거(사용자 결정). 프리팹 크기 250/500 으로 확대 예정(M2 잔여).
