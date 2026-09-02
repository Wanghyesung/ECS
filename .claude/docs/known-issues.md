# 알려진 문제 (Known Issues)

> Claude Code가 세션 중 발견했지만 아직 고치지 않은 문제를 기록하는 곳. 새 항목은 위에 추가하고, 문제가 해결되면 /PR을 남긴 뒤 지울지 허락을 받을 것.

## 조커카드 성공 시 후보 카드가 설계값(3개)보다 많이 보임 (2026-09-02)

**증상:** 조커카드 도박 성공 시 `SOJokerCard`의 `m_refPickCount` 곡선상 1레벨은 "3개 중 1개 선택"이어야 하는데, 실제로는 6개(혹은 그 이상)의 후보 슬롯이 보임.

**근본 원인:** SO 데이터(`SOJokerCard_0.asset`)의 곡선 자체는 정상이다 — `m_refCandidateCount.Evaluate(1) == 3`, `m_refPickCount.Evaluate(1) == 1`로 확인됨. 문제는 [JokerCardManager.cs](Assets/3D/05_Manager/JokerCardManager.cs)의 `ApplySuccess()`에 있음:

```csharp
m_listPendingFeature = FeatureManager.m_Instance.RequestFeature(m_SOJokerCard.GetCandidateCount(m_iLevel), m_arrTierMultiplierBuffer);
for (int i = 0; i < m_listPendingFeature.Count; ++i)
    m_refSelectContainer.AddData(m_listPendingFeature[i], 1);

int iPickCount = GetCurrentPickCount();
m_refPickContainer.Resize(iPickCount, eDataType.Features); // <- Pick 컨테이너는 사이즈를 맞춤
```

`m_refPickContainer`(내가 고를 컨테이너)는 `Resize()`로 그때그때 크기를 맞추지만, `m_refSelectContainer`(후보 컨테이너)는 크기 조정도, 이전 내용 초기화(`ClearData()`)도 하지 않는다. `Container.AddData()`는 비어있는 슬롯을 찾아 채우기만 하고 기존 슬롯을 지우지 않으므로(`Container.cs:273-308`), 다음 두 가지가 겹치면 후보가 누적된다:

1. `BattleScene.unity`(fileID 624567973)에서 select 컨테이너의 그리드가 4x3=12칸으로 고정되어 있어(`m_iSlotColCount: 4`, `m_iSlotRowCount: 3`), `GetCandidateCount()` 값과 무관하게 항상 그 크기의 그리드가 표시됨 — 빈 슬롯도 아이콘만 꺼질 뿐 배경 박스(`SlotView.BindData`)는 그대로 보임.
2. `PickData()`(현금화/확정)를 부르기 전까지는 `m_refSelectContainer`가 비워지지 않으므로(`ClearData()`는 오직 `PickData()`에서만 호출됨), 확정하지 않은 상태에서 조커 도박 결과가 한 번 더 반영되면 이전 회차 후보 위에 새 회차 후보가 그대로 추가된다.

**제안하는 수정:** `ApplySuccess()`에서 `m_refPickContainer.Resize(...)`와 대칭으로, 후보를 채우기 전에 `m_refSelectContainer.Resize(m_SOJokerCard.GetCandidateCount(m_iLevel), eDataType.Features)`(또는 최소한 `ClearData()`)를 먼저 호출해서 컨테이너를 매 회차 후보 수에 맞게 리셋해야 함. 아직 코드는 수정하지 않음 — 사용자 확인 후 적용 예정.

