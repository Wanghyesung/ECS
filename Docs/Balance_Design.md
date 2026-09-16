# 밸런스 설계 기록

플레이어/몬스터/무기 수치, 스테이지 구성, 성장 곡선을 맞춘 작업 기록.
기준 씬은 **BattleScene**이며 MainScene도 같은 시작 장비 구성으로 맞춰뒀다.

---

## 0. 먼저 — "한 판"의 구조

`DungeonManager.m_listStage`는 **순서대로 도는 코스가 아니라 로비에서 고르는 난이도 목록**이다.
`StartStage(idx+1)`을 호출하는 코드가 어디에도 없다. 실제 흐름은:

```
로비 SelectStage(캐러셀에서 스테이지 선택)
  → GameSceneManager.LoadStage(idx)   // m_listSceneData[idx] 의 씬 + 풀 로드
  → DungeonManager.StartStage(idx)     // m_listStage[idx] 의 스폰 테이블 예약
  → 잡몹 30마리 전멸 → 그 스테이지의 보스 등장 → 보스 처치 → 로비 복귀
```

**한 판 = 스테이지 1개 + 보스 1마리.** 모든 수치는 이 전제 위에 있다.

---

## 1. 설계 기준

| 축 | 목표 |
|---|---|
| 시작 TTK | 기본 잡몹(Mon0) 2.5초 |
| 후반 TTK | 최상위 잡몹(Mon10) 1~2초 |
| 보스전 | 빌드에 따라 20~60초 |
| 한 판 길이 | 스폰 56~74초 + 정리 = 대략 1분 30초 ~ 2분 |
| 한 판 레벨업 | Stage0 **11회** → Stage9 **27회** (카드 풀 총 38레벨) |
| 플레이어 즉사 | 없음. 회피 없이 계속 맞아도 Stage0 43초 / Stage9 10초를 버틴다 |

---

## 2. 시작 무기 — 기본총(BaseWeapon_0) 1정

기존엔 씬에서 기본총 2정 + 미사일 4기 + 샷건(100펠릿) + 중형탄 2정 …이 처음부터 켜져 있어서
"무기 해금" 카드 대부분이 이미 가진 걸 또 주는 빈 카드였다.

**결정: 기본총 1정만 활성, 나머지 15개 슬롯은 전부 카드로 해금.**

기본총을 고른 이유 — 단발/직선/중간 사거리라 어떤 빌드에도 얹히고, 관통·공격속도·공격력 카드가
전부 이 무기를 대상으로 잡고 있어 초반 카드가 즉시 체감된다.

이렇게 두면 해금 카드의 `MaxLevel`이 씬에 남은 잠긴 슬롯 수와 정확히 일치한다:

| 카드 | 대상 타입 | MaxLevel | 잠긴 슬롯 |
|---|---|---|---|
| SO_FeatAddBullet | Bullet | 5 | 5 (기본총1·중형탄2·속사탄2) |
| SO_FeatAddMissile | Missile | 4 | 4 |
| SO_FeatAddBulletMissile | MissileBullet | 2 | 2 |
| **SO_FeatAddLaser (신규)** | **Laser** | **3** | 3 (지속레이저1·관통빔2) |
| SO_FeatAddShotGun | ShotGun | 1 | 1 |
| SO_FeatureAddDrone | — | 1 | 1 |

---

## 3. 플레이어 (SO_PlayerInfo)

`SOObjectInfo`의 Max* 필드는 **기본값이 아니라 "성장으로 올릴 수 있는 상한"** 이다
(실제 이동속도 기본값은 `PlayerMovement.m_fMoveSpeed = 12`). 이 의미에 맞춰 스케일을 다시 잡았다.

| 필드 | 이전 | 이후 | 의미 |
|---|---|---|---|
| MaxHP | 1000 | 1000 | 시작 체력 |
| MaxSpeed | 3 | **6** | 이동속도 보너스 상한 (12 → 최대 18) |
| MaxAtack | 1000 | **150** | 공격력 **증가율(%)** 상한 = 데미지 최대 2.5배 |
| MaxDefense | 100 | **10** | 피격당 감소량(플랫) 상한 |

### 공격력을 평탄 가산 → 증가율(%)로 변경

`Player.AddAttack`이 모든 무기 데미지에 값을 **그대로 더하던** 구조라, 한 번에 여러 발이 나가는
무기에서 증가폭이 탄 개수만큼 뻥튀기됐다 (샷건 16펠릿 = 가산치의 16배). `Weapon`이 SO 원본
데미지(`m_iBaseDamage`)를 들고 배율로 재계산하도록 바꿔서 탄 개수와 무관하게 같은 비율로 오른다.

방어력은 플랫 유지 — 잡몹탄(20)엔 최대 50% 감소로 강하게, Mon10 탄(62)엔 16%로 약하게
작용해서 "후반엔 결국 피해야 한다"는 성질이 자연스럽게 나온다.

---

## 4. 무기 (SOAttackInfo + 씬의 탄 개수)

`Cooldown 0.02`(= 프레임 시간보다 짧음)는 **발사 속도가 프레임레이트에 종속**돼서 60fps와 144fps의
DPS가 달라졌다. 모든 쿨다운을 0.15초 이상으로 올려 제거.

| 무기 | Damage | Cooldown | 탄수 | 사거리 | 단일 DPS | 역할 |
|---|---|---|---|---|---|---|
| 기본총 SO_BaseAttackInfo | 12 | 0.15 | 1 | 480 | 80 | 시작 주력 |
| 중형탄 SO_MidAttackInfo | 26 | 0.55 | 2 | 500 | 95 | 느린 점사, 한 발이 무겁다 |
| 속사탄 SO_TemplateBulletInfo | 14 | 0.30 | 2 | 520 | 93 | 연사 |
| 유도탄 SO_PlayerMissileBulletInfo | 16 | 0.45 | 2 | 550 | 71 | 약유도 |
| 미사일 SO_MissileAttackInfo | 55 | 1.20 | 1 | 600 | 46 | 완전유도 + 도착 폭발 |
| 샷건 SO_MiniBulletInfo | 7 | 1.00 | 16 | **200** | 112(전탄) | 근접 화력, 사거리로 제동 |
| 관통빔 SO_PlayerBeamInfo | 24 | 0.50 | 1 | 500 | 48 | 한 줄 5마리 |
| 지속레이저 SO_PlayerLaserInfo | 18/틱 | 4.00 | 1 | 400 | 45 | 관통, 대상 수만큼 증폭 |

유도 무기는 거의 안 빗나가므로 명목 DPS를 낮게, 샷건은 전탄 명중 시 최고 DPS지만 사거리를
200으로 잘라 균형을 맞췄다.

---

## 5. 몬스터

| 몬스터 | HP | Speed | Exp | 사용 탄 (Damage/Cooldown) |
|---|---|---|---|---|
| Mon0 | 200 | 4.0 | 30 | SO_BaseMonBulletInfo2 (20 / 1.5) |
| Mon1 | 300 | 5.0 | 36 | SO_BaseMonBulletInfo (28 / 1.4) |
| Mon3 (T1) | 450 | 4.4 | 42 | SO_BaseMonBulletInfo2 (20 / 1.5) |
| Mon4 (T2) | 600 | 4.8 | 48 | SO_BaseMonBulletInfo2 (20 / 1.5) |
| Mon5 (T3) | 800 | 5.2 | 54 | **SO_MonBulletInfo_T3 (32 / 1.4)** |
| Mon6 (T4) | 1050 | 5.6 | 60 | **SO_MonBulletInfo_T3 (32 / 1.4)** |
| Mon7 (T5) | 1350 | 6.0 | 66 | **SO_MonBulletInfo_T5 (46 / 1.3)** |
| Mon8 (T6) | 1700 | 6.4 | 72 | **SO_MonBulletInfo_T5 (46 / 1.3)** |
| Mon9 (T7) | 2100 | 6.8 | 78 | **SO_MonBulletInfo_T7 (62 / 1.2)** |
| Mon10 (T8) | 2600 | 7.2 | 84 | **SO_MonBulletInfo_T7 (62 / 1.2)** |
| Boss | 45000 | 10 | 200 | 아래 보스 표 |

- HP는 티어당 약 1.25~1.3배의 등비 곡선. Mon9/Mon10이 **380/420** 이었던 건 3800/4200의
  자리수 오타로 보이며(공격력·방어력은 정상적으로 2400/2600, 240/260으로 이어짐), 곡선에 맞춰 복구.
- 예전엔 Mon3~Mon10이 **전부 같은 탄(20뎀)** 을 써서 후반 몬스터가 더 아프게 때리지 않았다.
  티어 탄 SO 3종(T3/T5/T7)을 새로 만들어 프리팹에 배분 — 뒤 스테이지일수록 실제로 위협적이다.
  탄 프리팹(PoolPrefab)은 기존 것을 그대로 공유하므로 풀이 늘지 않는다.

### 보스 공격력

| SO | Damage | Cooldown |
|---|---|---|
| SO_BossLaserInfo | 55/틱 | 5.0 (HitStep 1초) |
| SO_GBossBallInfo | 70 | 10.0 |
| SO_LBossBallInfo | 30 | 1.2 |
| SO_BossMissileInfo | 50 | 3.0 |
| SO_BossMissileBulletnfo | 25 | 1.0 |

---

## 6. 스테이지 — 10개, 각 10웨이브

기존엔 스테이지가 5개뿐이었고 하나당 **3웨이브(2/10/18초) × 8마리**라 한 판이 너무 짧았다
(한 판 3레벨업). 10개 스테이지 × **10웨이브 × 3마리 = 30마리**로 재구성.

| 스테이지 | 몬스터 | 웨이브 간격 | 스폰 구간 | 총HP | 레벨업 | 회피 없을 때 생존 |
|---|---|---|---|---|---|---|
| Stage0 | Mon0 / Mon1 | 8초 | 2~74초 | 7,500 | 11 | 43초 |
| Stage1 | Mon1 / Mon3 | 8초 | 2~74초 | 11,250 | 13 | 43초 |
| Stage2 | Mon3 / Mon4 | 8초 | 2~74초 | 15,750 | 15 | 62초 |
| Stage3 | Mon4 / Mon5 | 7초 | 2~65초 | 21,000 | 17 | 37초 |
| Stage4 | Mon5 / Mon6 | 7초 | 2~65초 | 27,750 | 19 | 27초 |
| Stage5 | Mon6 / Mon7 | 7초 | 2~65초 | 36,000 | 20 | 19초 |
| Stage6 | Mon7 / Mon8 | 7초 | 2~65초 | 45,750 | 22 | 15초 |
| Stage7 | Mon8 / Mon9 | 6초 | 2~56초 | 57,000 | 24 | 12초 |
| Stage8 | Mon9 / Mon10 | 6초 | 2~56초 | 70,500 | 26 | 10초 |
| Stage9 | Mon10 단일 | 6초 | 2~56초 | 78,000 | 27 | 10초 |

"생존"은 동시 8마리가 계속 쏘고 30%만 맞는다고 가정했을 때(방어 10) 1000HP가 버티는 시간.
실제로는 회피로 훨씬 길어진다. 초반은 가만히 있어도 죽지 않고, 후반은 회피가 필수가 되는 곡선.

### 스폰 위치 — 맵 전체 랜덤

맵은 **구체**다 (BattleScene `MapBoundary`: 중심 `(0, 0, 938.2)`, scale 600 → 반지름 300).
`MapBoundaryConstraint`가 이 구 안으로 플레이어를 클램프한다.

스폰 지점은 고정 좌표 8개 대신 **구 내부 랜덤**으로 생성했다:

- 중심에서 반지름 **90~255** 사이 (경계에서 45 이상 안쪽 — 경계에 낀 채 스폰되는 것 방지)
- 구면 균등 분포 (각도를 그냥 랜덤하면 극에 몰린다) + 반지름은 부피 균등
- 첫 2웨이브는 플레이어 시작 지점 `(214.1, 154, 855)` 에서 **130 이상** 떨어뜨림 — 시작하자마자
  코앞에 스폰되는 것 방지
- 생성은 고정 시드(`20260916`)라 다시 돌려도 같은 배치가 나온다

### 경험치

`BattleManager.m_iMaxExp`는 100 고정(레벨별 요구치 곡선 없음)이라 **ExpReward만으로** 페이싱을 잡았다.
이전엔 모든 몬스터가 100이라 **1킬 = 1레벨업**이었다.
스테이지당 잡몹 30마리 × (티어별 30~84) + 보스 200 → 위 표의 11~27레벨업.

---

## 7. 카드 수치

| 카드 | 등급 | 레벨당 효과 | MaxLevel |
|---|---|---|---|
| SO_FeatureUpAttack | Rare | 공격력 +18% (MaxAtack 150의 12%) | 5 |
| SO_AttackBulletSpeedUp | Rare | Bullet 쿨다운 -6% | 5 |
| SO_FeatureUpDefense10 | Common | 방어 +3 (MaxDefense 10의 30%) | 3 |
| SO_FeatureUpSpeed | Uncommon | 이동속도 +0.9 (MaxSpeed 6의 15%) | 3 |
| SO_FeatUpHP0.2 | Common | MaxHP 25% 회복 | 무제한 |
| SO_FeatureUpBulletSpeed20 | Uncommon | 탄속 +20 | 3 |

### 신규 — SO_FeatAddLaser (레이저 스킬)

- `eFeatureID.LaserUp` (신규, 16) / `SOFeatureAddWeapon` / 대상 `eWeaponType.Laser`
- **등급 Rare(중간등급)**, Repeatable, MaxLevel 3, Weight 1
- 1레벨 → LaserWeapon(지속 레이저), 2~3레벨 → BeamWeapon0/1(관통 빔)

`SO_PlayerLaserInfo`가 **Beam 프리팹(SO_BeamData)** 을 가리키고 있어서 지속형 설정
(HitStep / TelegraphDuration)이 전부 무시되고 `Speed: 0` 때문에 빔이 제자리에 굳어 있었다.
실제 `Laser` 컴포넌트를 가진 `SO_PlayerLaser Data`로 연결하고, 프리팹 사거리도 20 → 400으로
올렸다(맵 거리가 수백 단위인데 20이면 닿지 않음). 플레이어 무기라 예고선(Telegraph)은 0으로 제거.

---

## 8. 오브젝트 풀 — 스테이지별 분리

`ObjectPoolManager.GetObject`는 스택이 비면 **null을 반환하고 풀은 커지지 않는다**.
`SOPoolData.Max`는 주석대로 미사용이고 **실제 용량은 `PreLoad`** 다.

웨이브를 3회 → 10회로 늘리면서 두 가지를 같이 처리했다.

**(1) 풀을 스테이지별로 쪼갬.** 예전엔 `SO_BattleSceneData` 하나에 몬스터 10종이 전부 들어 있어
어떤 스테이지를 골라도 10종을 다 프리로드했다. `GameSceneManager.m_listSceneData`가 원래
"스테이지별로 어떤 씬 + 어떤 풀을 쓰는지" 관리하라고 있는 필드라, 스테이지마다
`SO_BattleSceneData_Stage0~9`를 만들어 **공통 풀 28종 + 그 스테이지가 쓰는 몬스터 2종**만 담았다.

몬스터 `PreLoad`는 전부 16으로. 스테이지당 실제 프리로드는 **몬스터 32마리 + 보스 2** —
기존 52마리보다 오히려 가볍다.

**(2) 풀이 비어도 판이 안 멈추게.** 30마리 중 16마리가 동시에 살아있으면 나머지 스폰은 풀 고갈로
실패한다. 예전 코드는 이때 조용히 스킵했고, `DungeonManager`는 **예약 개수**를 미리 세어두고
처치 때만 깎았기 때문에 그 수가 영영 0이 안 돼서 **보스가 안 나오고 판이 소프트락**됐다
(`Docs/TODO.md` 3번). 둘 다 고쳤다 — §9 참고.

---

## 9. 같이 고친 버그

| 위치 | 증상 |
|---|---|
| `SOFeatureUpAttack.Apply` | SO의 `m_fAttackRatio` 대신 획득 레벨을 넘겨서 1레벨에 `UpAttackRatio(1.0)` = 공격력 상한 즉시 도달 |
| `SOFeatureUPHP.Apply` | 위와 동일. 회복량이 `MaxHP × 획득횟수` |
| `Player.UpAttackRatio` | 공격력 증가율을 **MaxHP** 기준으로 계산 (MaxAtack이어야 함) |
| `Player.AddAttack` / `UpBulletSpeed` | **활성 무기에만** 반영 → 공격력 카드를 먼저 먹고 나중에 해금한 무기는 그때까지 쌓인 보너스를 영영 못 받음 |
| `Player.AddDefense` | 하한 없음 → 조커 실패로 몰수되면 방어력이 음수가 되어 피해가 증가 |
| `SO_FeatureUpDefense10` | `m_fDefenseRatio: 0` — 아무 효과 없는 카드 |
| `SO_MiniBulletInfo` | WeaponType이 Bullet인데 샷건 카드는 ShotGun을 찾음 → 샷건 카드가 아예 발동 안 됨 |
| `Drone.Update` | `if (m_fFireAngle < fAngleDiff) Fire()` — 부등호가 반대라 **조준이 빗나가는 동안에만** 발사 |
| Drone `m_fRotateSpeed` | 10°/s로는 허용 오차(10°) 안에 들어오지 못함 → 180으로 |
| `ObjectSpawner.SpawnObject` | 풀 고갈 시 예약을 버림 → 그 몬스터가 영영 안 나옴. **재예약(1초 뒤 재시도)** 으로 변경 |
| `DungeonManager` | 남은 몬스터를 **예약 수**로 세어서, 스폰이 하나만 밀려도 보스가 안 나오고 판이 멈춤. **실제 스폰/사망 기준**으로 변경 |
| `DungeonManager.MonsterDead` | 같은 프레임에 여럿이 죽으면(핵폭탄 등) `SpawnBossWhenCameraFree`가 여러 번 호출돼 **보스가 중복 스폰**. 요청 플래그(`m_bBossRequested`) 추가 |

---

## 10. 남은 것

- `SOMonsterInfo.MaxAtack` / `MaxDefense`는 `Monster.TakeDamage`가 읽지 않아 현재 **장식용**이다
  (몬스터는 방어력 무시하고 피해를 그대로 받음).
- `SOMonBossInfo`가 단일 SO라 **보스는 10개 스테이지 전부 같은 45000 HP**다. 스테이지별로
  보스를 다르게 하려면 SO를 나누고 `SOStage`에 보스 정보 참조를 추가해야 한다.
- `BaseMonster*.prefab`에 `Weapon` 컴포넌트가 2개(`Weapon1`/`Weapon2`)인데 `m_listSpawn`에는
  `Weapon1`만 물려 있다. `Weapon2`는 `Init()`도 안 되고 발사도 안 되는 미사용 상태 —
  의도한 것인지 확인 필요. (탄 SO 교체는 혼동을 막으려고 둘 다 해뒀다)
- `SO_BossInfo.asset`은 `MonsterName` / `AttackRange` 등 지금 `SOObjectInfo`에 없는 필드만 들고
  있는 고아 에셋. 보스 프리팹은 `SOMonBossInfo`를 쓴다 — 에셋 삭제는 GUID 참조 때문에 Unity에서 진행할 것.
- `SO_MainSceneData.PoolDataList`에 `SO_BeamData`가 두 번 들어가 있다. (BattleScene 쪽은 이번에 정리됨)
- MainScene은 시작 장비 구성만 BattleScene과 맞춰뒀고, 스테이지/풀 분리는 BattleScene 기준으로만 했다.
- TestScene 전용 사본(`00_Scene/3D/TestScene/PlayerData/*`)은 이번 범위 밖이라 그대로 뒀다.
