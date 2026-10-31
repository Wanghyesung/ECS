# 밸런스 산정 가이드

> 몬스터 HP/공격력/스피드, 플레이어 무기 SO 데이터를 감으로 만지지 않고 계산 근거를 갖고 조정하기 위한 문서. 새 스테이지/몬스터/무기를 추가하거나 밸런스를 다시 잡을 때마다 이 절차를 반복 적용한다.

---

## 1. 데이터 소스 지도

| 항목 | SO 클래스 | 실제 필드 | 에셋 경로 |
|---|---|---|---|
| 몬스터 HP/속도/경험치 | `SOMonsterInfo`(`SOObjectInfo` 상속) | `MaxHP`, `MaxSpeed`, `ExpReward` | `Assets/3D/03_Monster/BaseMonster/Node/MonsterInfo/SOMon*Info.asset` |
| 몬스터 공격력 | ⚠️ 아래 "함정" 참고 | — | 몬스터 프리팹 하위 `Weapon`의 `SOAttackInfo` |
| 웨이브 구성 | `SOStage` | `ListSpawnEntry[].fSpawnTime/MonsterPrefab/vPosition`, `BossPrefab` | `Assets/00_Scene/3D/MainScene/SceneData/Stage/SO_MainStage0~4.asset` |
| 플레이어 무기 스탯 | `SOAttackInfo` | `Damage`, `Cooldown`, `Speed`, `HitCount`, `AliveTime` 등 | `Assets/3D/02_Player/Weapon/SO_*.asset` |
| 무기 활성화 트리거 | `SOFeatureAddWeapon` 등 | `Player.ActiveWweapon()` 호출 | `Assets/3D/02_Player/Feature/SOFeature*.cs` |

### ⚠️ 함정: `SOMonsterInfo.MaxAtack`/`MaxDefense`는 죽은 필드

`Monster.cs`는 `MaxHP`/`MaxSpeed`/`ExpReward`만 읽는다. `MaxAtack`/`MaxDefense`는 인스펙터에 보이지만 코드 어디서도 참조하지 않는다. **몬스터가 실제로 얼마나 아프게 때리는지는 몬스터 프리팹 하위의 `Weapon` 오브젝트가 참조하는 `SOAttackInfo`(BT Action 노드, 예: `SOFireObjectNode.cs`가 발사를 트리거)에서 결정된다.** 몬스터 종류별로 이 SOAttackInfo가 다 다르므로, "몬스터 공격력을 올리고 싶다"면 `SOMonsterInfo`가 아니라 그 몬스터 프리팹 하위 Weapon의 SOAttackInfo를 찾아서 고쳐야 한다. (이번 패스에서는 8종 몬스터 각각의 공격 SOAttackInfo를 전수조사하지 않았음 — 다음에 할 일로 남겨둠.)

---

## 2. 핵심 계산식

```
PlayerDPS  = Σ( 활성 무기.Damage × 활성 무기.HitCount / 활성 무기.Cooldown )   // 현재 켜져 있는 무기만 합산
TTK(몬스터) = 몬스터.MaxHP / PlayerDPS                                        // 몬스터 하나를 죽이는 데 걸리는 시간(초)
MonsterIncomingDPS = 몬스터_공격_SOAttackInfo.Damage / Cooldown              // 위 함정 참고, 몬스터별로 따로 찾아야 함
```

**웨이브 단위 체크**: `SOStage.ListSpawnEntry`의 `fSpawnTime`(예: 2s, 10s, 18s)과 그 시점에 이미 필드에 있는 몬스터들의 TTK를 비교한다. 예를 들어 t=2s에 스폰된 몬스터의 TTK가 8초인데 t=10s에 다음 웨이브가 또 온다면, 화면에 몬스터가 계속 쌓인다 — 의도한 게 아니라면 HP를 낮추거나 PlayerDPS를 올려야 한다.

---

## 3. 등차수열 기울기 도출 절차 (실제 적용 예시)

이번에 실제로 적용한 계산 과정을 그대로 따라 하면 된다.

**Step 1 — 기준 DPS 확정**: MainScene Player의 "시작 시점" 기준 활성 무기를 합산.
- BaseWeapon_0/1: `SO_BaseAttackInfo`(Damage10, Cooldown0.02, HitCount1) → 500 DPS × 2 = 1000
- Missiles×4: `SO_MissileAttackInfo`(Damage20, Cooldown1, HitCount1) → 20 DPS × 4 = 80
- **PlayerDPS ≈ 1080**

**Step 2 — 목표 TTK 앵커 2개 정하기**: 초반(Stage0) 0.5~1초, 후반(Stage3/4) 2~3초로 설정. 웨이브당 몬스터가 두 타입(약한 쪽/강한 쪽)이라 각 타입을 별도 등차수열로 취급.

**Step 3 — HP 역산 (`HP = DPS × TTK`) 후 스테이지 사이 선형 보간**:

| 몬스터 | 스테이지 | 이전 HP | **적용된 새 HP** | 목표 TTK |
|---|---|---|---|---|
| SOMon0Info (BaseMonster) | 0 | 100 | **800** | 0.74s |
| SOMon1Info (BaseMonster1) | 0 | 200 | **1000** | 0.93s |
| SOMon3Info (BaseMonster3) | 1 | 140 | **1450** | 1.34s |
| SOMon4Info (BaseMonster4) | 1 | 180 | **1700** | 1.57s |
| SOMon5Info (BaseMonster5) | 2 | 220 | **2050** | 1.90s |
| SOMon6Info (BaseMonster6) | 2 | 260 | **2450** | 2.27s |
| SOMon7Info (BaseMonster7) | 3·4 | 300 | **2700** | 2.50s |
| SOMon8Info (BaseMonster8) | 3·4 | 340 | **3150** | 2.92s |

(2026-09-02 기준 이미 적용 완료. `SOMonBossInfo`, 미사용 중인 `SOMon9Info`/`SOMon10Info`, 그리고 모든 몬스터의 `MaxSpeed`는 이번엔 손대지 않았다.)

**Speed는 이 공식과 무관한 별도 축이다.** HP/DPS 계산과 달리 "몬스터가 사거리 안에 들어오기까지 걸리는 시간"이라 정량화하기 애매하다 — 과도하게 올리면 총알을 맞기도 전에 붙어서 불합리하게 느껴진다. 이건 공식으로 도출하지 말고 실제 플레이 체감으로 조금씩 조정할 것을 권장.

---

## 4. 실전 워크시트 (플레이하면서 채워 넣을 것)

| 스테이지 | 활성 무기 구성 | 계산된 PlayerDPS | 몬스터 HP | TTK | 실제 체감(빠름/적당/느림) | 조정값 |
|---|---|---|---|---|---|---|
| 0 | | | | | | |
| 1 | | | | | | |
| 2 | | | | | | |
| 3·4 | | | | | | |

플레이 중 레벨업으로 무기가 추가/강화되면 그 시점의 PlayerDPS가 바뀌므로, 웨이브 진입 시점 기준으로 활성 무기 구성을 적어두고 다시 계산해야 정확하다.

---

## 5. 값 적용 방법

- 단일 값 수정: Unity 인스펙터에서 해당 `.asset` 클릭 후 직접 수정.
- 여러 개를 한 번에 스크립트로 적용: `mcp__UnityMCP__manage_scriptable_object`(action=`modify`, `target: {guid: "..."}`, `patches: [{"path": "MaxHP", "value": 800}]`)를 에셋 개수만큼 반복 호출. dry_run으로 먼저 property path 유효성 검증 가능.
