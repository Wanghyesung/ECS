# 밸런스 설계 기록

플레이어/몬스터/무기 수치와 성장 곡선을 한 번에 맞춘 작업 기록. 기준 씬은 **BattleScene**이며
MainScene도 같은 시작 장비 구성으로 맞춰뒀다.

---

## 1. 설계 기준

| 축 | 목표 |
|---|---|
| 시작 TTK | 기본 잡몹(Mon0) 2.5초 |
| 후반 TTK | 최상위 잡몹(Mon10) 1~2초 |
| 보스전 | 빌드에 따라 20~60초 |
| 런 1회 레벨업 | **17회** (카드 풀 총 38레벨의 약 45% → 빌드 분기가 생김) |
| 플레이어 즉사 | 없음. 잡몹탄 35~50대를 맞아야 사망 → 회피가 의미를 갖는 구간 |

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

방어력은 플랫 유지 — 잡몹탄(20)엔 최대 50% 감소로 강하게, 보스 큰 기술(70)엔 14%로 약하게
작용해서 "보스는 결국 피해야 한다"는 성질이 자연스럽게 나온다.

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

| 몬스터 | HP | Speed | Exp | 등장 |
|---|---|---|---|---|
| Mon0 | 200 | 4.0 | 15 | St0 |
| Mon1 | 300 | 5.0 | 20 | St0 |
| Mon3 (T1) | 450 | 4.4 | 25 | St1 |
| Mon4 (T2) | 600 | 4.8 | 30 | St1 |
| Mon5 (T3) | 800 | 5.2 | 35 | St2 |
| Mon6 (T4) | 1050 | 5.6 | 40 | St2 |
| Mon7 (T5) | 1350 | 6.0 | 45 | St3 |
| Mon8 (T6) | 1700 | 6.4 | 50 | St3 |
| Mon9 (T7) | 2100 | 6.8 | 55 | **St4 (신규 투입)** |
| Mon10 (T8) | 2600 | 7.2 | 60 | **St4 (신규 투입)** |
| Boss | 45000 | 10 | 200 | St4 |

- HP는 티어당 약 1.25~1.3배의 등비 곡선. Mon9/Mon10이 **380/420** 이었던 건 3800/4200의
  자리수 오타로 보이며(공격력·방어력은 정상적으로 2400/2600, 240/260으로 이어짐), 곡선에 맞춰 복구.
- Stage4가 Stage3과 완전히 같은 구성(Mon7/Mon8)이라 최종 스테이지가 직전과 구분되지 않았다 → Mon9/Mon10으로 교체.

### 몬스터 공격력

| SO | Damage | Cooldown | 사용처 |
|---|---|---|---|
| SO_BaseMonBulletInfo2 | 20 | 1.5 | Mon0, Mon3~Mon10 |
| SO_BaseMonBulletInfo | 28 | 1.4 | Mon1 |
| SO_BossLaserInfo | 55/틱 | 5.0 | 보스 (HitStep 1초) |
| SO_GBossBallInfo | 70 | 10.0 | 보스 대구체 |
| SO_LBossBallInfo | 30 | 1.2 | 보스 추적구 |
| SO_BossMissileInfo | 50 | 3.0 | 보스 미사일 |
| SO_BossMissileBulletnfo | 25 | 1.0 | 보스 유도탄 |

---

## 6. 경험치 / 레벨업 페이싱

`BattleManager.m_iMaxExp`는 100 고정(레벨별 요구치 곡선 없음)이라 **ExpReward만으로** 페이싱을 잡았다.
이전엔 모든 몬스터가 100이라 **1킬 = 1레벨업**이었고, 한 런에 40장 이상을 뽑아 카드 풀을 전부
소진했다(= 빌드 선택이 없음).

| 스테이지 | 획득 | 누적 | 레벨 |
|---|---|---|---|
| St0 | 140 | 140 | 1 |
| St1 | 220 | 360 | 3 |
| St2 | 300 | 660 | 6 |
| St3 | 380 | 1040 | 10 |
| St4 | 460 | 1500 | 15 |
| 보스 | 200 | 1700 | **17** |

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

## 8. 같이 고친 버그

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

---

## 9. 남은 것

- **Mon3~Mon10이 전부 같은 `SO_BaseMonBulletInfo2`(20 데미지)를 쓴다.** HP와 개체수만 오르고
  후반 몬스터가 더 아프게 때리지는 않는다. 티어별 탄 SO를 나누거나 `SOMonsterInfo.MaxAtack`을
  실제로 데미지에 반영하는 경로가 필요.
- `SOMonsterInfo.MaxAtack` / `MaxDefense`는 `Monster.TakeDamage`가 읽지 않아 현재 **장식용**이다
  (몬스터는 방어력 무시하고 피해를 그대로 받음).
- `SO_BossInfo.asset`은 `MonsterName` / `AttackRange` 등 지금 `SOObjectInfo`에 없는 필드만 들고
  있는 고아 에셋. 보스 프리팹은 `SOMonBossInfo`를 쓴다 — 에셋 삭제는 GUID 참조 때문에 Unity에서 진행할 것.
- `SO_MainSceneData.PoolDataList`에 `SO_BeamData`가 두 번 들어가 있다.
- TestScene 전용 사본(`00_Scene/3D/TestScene/PlayerData/*`)은 이번 밸런싱 범위 밖이라 그대로 뒀다.
