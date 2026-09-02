# 알려진 문제 (Known Issues)

> Claude Code가 세션 중 발견했지만 아직 고치지 않은 문제를 기록하는 곳. 새 항목은 위에 추가하고, 문제가 해결되면 /PR을 남긴 뒤 지울지 허락을 받을 것.

## DontDestroyOnLoad가 5개 매니저에서 조용히 실패함 — "DonDestoryObjects" 자식이라 루트가 아님 (2026-09-02)

**증상:** 빌드 로그(`Player.log`)에 씬 전환마다 `DontDestroyOnLoad only works for root GameObjects or components on root GameObjects.` 경고가 `BattleManager`(BattleManager.cs:42), `InputManager`(InputManager.cs:46), `ObjectPoolManager`(ObjectPoolManager.cs:66), `CameraManager`(CameraManager.cs:35), `FeatureManager`(FeatureManager.cs:43) 5개에서 반복해서 찍힘. `EquipController`(EquipController.cs:37)도 UI 버튼 클릭 경로에서 한 번 더 같은 경고를 냄.

**근본 원인:** TestScene.unity/MainScene.unity 모두 `DonDestoryObjects`라는 부모 GameObject 하나 밑에 PoolManager/InputManager/CameraManager/FeatManager/BattleManager 등 9개 매니저가 자식으로 매달려 있음. 그런데 각 매니저는 자기 `Awake()`에서 `DontDestroyOnLoad(gameObject)`를 **자기 자신**한테 호출함 — `DontDestroyOnLoad`는 씬 루트 오브젝트에만 동작하므로, 자식인 이 매니저들에게는 조용히 무시됨.

**영향:** Single 모드 씬 전환마다(`GameSceneManager.LoadSceneAsync`가 `Addressables.LoadSceneAsync(..., LoadSceneMode.Single)` 사용) 이 매니저들이 실제로는 파괴되고, 새 씬의 새 인스턴스가 다시 `m_Instance`를 잡는 식으로 "우연히" 굴러가고 있음. 지금 당장 크래시는 안 나지만, 씬 전환마다 불필요한 파괴/재생성 비용이 들고, 나중에 어딘가 이 인스턴스를 캐싱해두는 코드가 생기면 그 즉시 끊어진 참조 버그로 터질 잠재적 위험이 있음.

**제안하는 수정:** `DonDestoryObjects` 부모 오브젝트 자체를 루트로 두고 그 부모 하나에만 `DontDestroyOnLoad`를 걸거나(자식들은 자동으로 같이 유지됨), 각 매니저의 `Awake()`에서 자기 자신이 아니라 `transform.root.gameObject`에 `DontDestroyOnLoad`를 호출하도록 수정. 아직 코드는 수정하지 않음 — 사용자 확인 후 적용 예정.

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

