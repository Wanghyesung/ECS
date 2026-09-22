## Feature
플레이어 차지 샷 무기. `WeaponType`이 필요 없이 입력 없이 자동으로 차지 -> 완충되면 자동 발사. 완충된 총알은 즉발탄보다 크고 빠름. 발사 정책은 `SOWeaponFireBehavior`(추상 SO)로 조합해서 `Weapon`에 꽂는 구조 — `Weapon`/`Player`/`Drone`은 앞으로 새 발사 방식이 추가돼도 다시 바뀌지 않음.

## Files changed
- `Assets/3D/02_Player/Weapon/Weapon.cs` — `eWeaponType.Charge` 추가, `m_SOFireBehavior`/`m_refFireBehavior` 필드, `UpdateWeapon()`(신규 진입점)/`OnDisable()` 추가, `Fire()`를 2-인자/4-인자(배율) 오버로드로 분리, `ComputeAimRotation()` 추출, `FireCircularSector`에 속도 배율 인자 + bool 반환 추가, `FireAndRotate`에 `SizeScale=1f` 명시, `SetCooldown()`이 `OnCooldownScaled` 훅 호출
- `Assets/3D/02_Player/Weapon/SOWeaponFireBehavior.cs` — **신규**, 추상 SO 베이스(`Tick`/`OnInit`/`OnWeaponDisabled`/`OnCooldownScaled`)
- `Assets/3D/02_Player/Weapon/SOChargeShotBehavior.cs` — **신규**, 차지 정책 구현 + `GetChargeRatio` 정적 함수(TDD 대상, 구현 완료)
- `Assets/3D/10_Option/SOAttackInfo.cs` — `tShotInfo`에 `SizeScale` 필드 추가 (`SOAttackInfo`/`AttackInfo`는 변경 없음)
- `Assets/3D/02_Player/Weapon/Bullet.cs` — `m_vBaseScale`/`m_fLastAppliedScale` 필드, `ApplySizeScale()` 추가, `SetAttack()`에서 `UpdateLine()` 이전에 호출
- `Assets/3D/02_Player/Weapon/CircleCollider.cs` — `m_fRuntimeRadiusScale` 필드, `Radius`/`Center`/`RefreshCenter`/`OnDrawGizmos`가 스케일 반영하도록 수정, `SetRadiusScale()` 추가
- `Assets/3D/05_Manager/BoxColliderGrid.cs` — `NeighborColliders`에 `_fQueryRadius` 인자 추가, 링 개수를 `ceil(r/S + 0.5)`로 동적 계산
- `Assets/3D/05_Manager/ColliderManager.cs` — `CheckCrossLayerGrid`가 Circle 반지름을 `NeighborColliders`에 넘기도록 수정
- `Assets/3D/02_Player/Player.cs` — `Fire()` 루프가 `weapon.UpdateWeapon(...)` 단일 호출로 (타입 분기 없음)
- `Assets/3D/02_Player/Drone/Drone.cs` — `Fire()`가 `m_refWeapon.UpdateWeapon(...)` 호출로 통일

## Completion criteria (TDD 대상 API)
`SOChargeShotBehavior.GetChargeRatio(float _fElapsedTime, float _fMaxChargeTime)` — 순수 정적 함수:
- 경과시간 0 → 0
- 경과시간 == MaxChargeTime → 1
- 경과시간 초과 → 1로 클램프
- 경과시간 절반 → 0.5
- MaxChargeTime <= 0 → 0나눗셈 없이 즉시 1

## Test results
5 passed, 0 failed (`Assets/Tests/Editor/ChargeShotBehaviorTests.cs`)

## Scene wiring
BattleScene.unity(`DynamicObject/MainPlayer`)에 다음을 MCP로 배선함:
- `Assets/3D/02_Player/Weapon/SO_ChargeAttackInfo.asset` 생성 — `WeaponType=Charge(7)`, `PoolPrefab`은 기존 `BaseBullet.prefab` 풀 재사용, `Damage=25`/`AttackPower=10`/`Cooldown=0.3`/`Speed=150`/`SpeedOffset=10`/`AliveTime=2.5`/`HitCount=1`
- `Assets/3D/02_Player/Weapon/SO_ChargeShotBehavior.asset` 생성 — `MaxChargeTime=1.5`/`MaxChargeSizeScale=2.5`/`MaxChargeSpeedScale=1.5`
- `MainPlayer/Player/VisualPlayer/BaseWeapon_0`을 복제해 `ChargeWeapon` GameObject 생성(자체 `ShotParticle` 자식 포함), `Weapon.m_SOAttackInfo`=`SO_ChargeAttackInfo`, `Weapon.m_SOFireBehavior`=`SO_ChargeShotBehavior`, `m_iBulletCount=1`(산탄 아님)로 설정
- `Player.m_listWeapon`(15번째 항목)에 `ChargeWeapon` 등록
- Play Mode 4초 실행 확인 — 컴파일/런타임 예외 없음(무관한 기존 이슈 1건 `known-issues.md`에 별도 기록)

## Scope
이 목록의 파일만 검토 대상입니다. 그 외 기존 코드는 건드리지 마세요. 특히 중점적으로 봐야 할 지점:
- `Bullet.SetAttack()`에서 `ApplySizeScale()` 호출 위치(반드시 `UpdateLine()`보다 먼저)가 실제로 지켜졌는지
- `tShotInfo.SizeScale` 기본값(0) 누락 시 기존 무기 총알이 크기 0이 되는 회귀가 없는지(모든 `Weapon.Fire`류 경로에서 명시적으로 세팅되는지)
- `?.` 대신 `== null`/명시적 null 체크를 쓰는 이 프로젝트 규칙이 새 코드에서 지켜졌는지
- `SOWeaponFireBehavior`/`SOChargeShotBehavior`가 런타임 상태(타이머 등)를 갖는 것이 "SO는 데이터만" 규칙에 위배되지 않는지(Instantiate 클론 인스턴스에 한해 허용되는 예외 조항에 해당하는지)
- `BoxColliderGrid.NeighborColliders`/`ColliderManager.CheckCrossLayerGrid` 변경이 기존 운석-총알 충돌 판정을 깨뜨리지 않는지
