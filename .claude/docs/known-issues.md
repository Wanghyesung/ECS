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


## 빌드에서만 풀 오브젝트가 안 나옴 (몬스터 공격/히트 이펙트/BulletLine 등) (2026-09-05)

**증상:** 에디터 플레이에서는 풀에서 오브젝트가 정상적으로 나오는데, 빌드에서는 몬스터 스폰만 되고 몬스터 공격/파괴 임팩트/BulletLine/스폰 이펙트 등이 나오지 않음.

**근본 원인 1 — Addressables 에셋 중복으로 인한 SOPoolData 인스턴스 불일치 (주범):**
`ObjectPoolManager`의 모든 딕셔너리는 `SOPoolData` **에셋 참조(reference equality)** 를 키로 쓴다(`m_hashPool` 등). 그런데 이 프로젝트는 참조 경로가 두 갈래로 갈라져 있다.

- 플레이어 빌드 데이터 쪽: `LobyScene`(EditorBuildSettings의 유일한 씬) → `GameSceneManager.m_listSceneData` → `SO_BattleSceneData.PoolDataList` → SOPoolData / `DungeonManager` → `SO_MainStage0~4` → SOPoolData
- Addressables 번들 쪽: `BattleScene`(Addressable) + 몬스터/총알 프리팹(Addressable) → `Weapon.m_SOAttackInfo.PoolPrefab`, `Bullet.m_refHitEffectObj`, `Monster.m_refDeadEffect`, BT/BulletAction SO의 SOPoolData

SOPoolData 에셋들은 Addressable로 등록되어 있지 않은 **암시적 의존성**이라, 빌드 시 (a) LobyScene 의존성으로 플레이어 데이터에 한 벌, (b) Addressables 번들에 또 한 벌 — 총 2벌이 직렬화된다. 런타임에는 서로 다른 ScriptableObject 인스턴스가 되므로, 풀은 (a) 사본으로 등록되고 조회는 (b) 사본으로 들어와 `TryGetValue`가 조용히 실패한다. 에디터 플레이는 Play Mode Script가 `Use Asset Database (fastest)`라 에셋이 딱 한 벌뿐이라 항상 성공한다 — 그래서 에디터에서만 잘 된다.

이 구조가 증상과 정확히 일치한다: 몬스터 스폰은 Loby 쪽(`DungeonManager`/`ObjectSpawner` → SOStage)이라 키가 맞아서 성공하고, 씬/프리팹(번들) 안에서 호출되는 나머지는 전부 실패한다. 앞선 커밋 `a535ee5`(SOStage가 SOPoolData 경유)가 몬스터 스폰만 고쳤던 것도 같은 이유다.

**근본 원인 2 — `SO_BattleSceneData.PoolDataList` 항목 누락:**
빌드가 실제로 로드하는 씬 데이터는 `SO_BattleSceneData`(LobyScene의 `GameSceneManager.m_listSceneData`에 이것 하나만 등록됨) 인데, `SO_MainSceneData`에는 있는 `SO_BulletLine Data.asset` 과 `SO_SpawnData.asset` 이 목록에서 빠져 있다. 즉 이 두 풀은 애초에 생성조차 되지 않아 `GetObject`가 무조건 null을 돌려준다. (`SO_MainSceneData`는 `SO_BeamData`가 두 번 중복 등록되어 있기도 함)

**근본 원인 3 — BulletLine 풀 데이터 미할당:**
`m_refLineDrawer.m_refBulletLinePoolObj`가 총알 프리팹 19개 전부 `{fileID: 0}`(미할당)이다(`Assets/3D/02_Player/Weapon/Bullet/*.prefab`, `Assets/3D/03_Monster/Bullet/*.prefab`). `BulletLineDrawer.SetLine()`이 초입에서 return하므로 예고선은 어떤 경로로도 안 나온다. 디스크 상태 기준이므로 에디터에서 저장 안 된 변경이 있는지 확인 필요.

**부가 요인 — 실패가 조용함:** `ObjectPoolManager.GetObject()`는 키가 등록 안 됐을 때/스택이 비었을 때 로그 없이 `null`을 반환하고, 호출부(`BulletLineDrawer`, `Bullet`, `Laser` 등)도 null이면 그냥 return한다. 그래서 빌드에서 아무 에러 없이 "그냥 안 나오는" 상태가 된다.

**제안하는 수정:**
1. (권장) 풀 키를 참조가 아니라 **값(문자열 ID)** 으로 바꾼다 — `Dictionary<string, ...>`에 `SOPoolData.PrefabRef.AssetGUID`를 키로 사용. 사본이 몇 벌 생기든 같은 키로 수렴하므로 중복 문제에 근본적으로 면역이 된다.
2. 또는 중복 자체를 없앤다 — `SOSceneData`/`SOStage`/`SOPoolData`를 Addressable로 등록하고 `GameSceneManager`/`DungeonManager`가 직접 참조 대신 주소로 로드하게 바꾼다. (LobyScene을 Build Settings에서 빼고 부트스트랩 씬만 남기는 방식도 동일 효과)
3. `SO_BattleSceneData.PoolDataList`에 `SO_BulletLine Data`, `SO_SpawnData` 추가 + `SO_MainSceneData`의 `SO_BeamData` 중복 제거.
4. 총알 프리팹들의 `m_refBulletLinePoolObj` / `m_refVisualMeshFilter` 할당.
5. `GetObject`가 키 미등록일 때 `[Conditional("UNITY_EDITOR")]` 경고를 찍도록 보강(다음에 같은 문제를 즉시 발견하기 위해).

**검증 방법:** Addressables Groups 창 → Play Mode Script를 `Use Existing Build (Packed)`로 바꾸고 LobyScene에서 플레이하면 에디터에서 빌드와 동일한 증상이 재현된다. Tools → Analyze → `Check Duplicate Bundle Dependencies`로 중복 목록도 확인 가능.

아직 코드/에셋은 수정하지 않음 — 사용자 확인 후 적용 예정.
