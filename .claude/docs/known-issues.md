# 알려진 문제 (Known Issues)

> Claude Code가 세션 중 발견했지만 아직 고치지 않은 문제를 기록하는 곳. 새 항목은 위에 추가하고, 고치면 해당 항목에 `[해결됨]`을 표시하고 커밋/PR을 남긴 뒤 지우지 말고 남겨둘 것 (재발 방지 기록).

## `ColliderManager.m_fMaxBulletRange = 50`이 차지 샷/일반 총알의 실제 사거리보다 훨씬 짧음 (2026-08-21, 플레이어 차지 샷 무기 `unity-optimizer` 점검 중 발견)

**근본 원인:** `BattleScene.unity`에 직렬화된 `ColliderManager.m_fMaxBulletRange = 50`은 `UpdateBoxGrids()`가 플레이어로부터 이 거리보다 먼 Box 콜라이더(운석 등)를 그리드 판정 대상에서 제외하는 컬링 반경이다. 그런데 실제 총알 사거리(`Speed × AliveTime`)를 계산해보면:
- 차지 샷 완충탄: `(150±10) × MaxChargeSpeedScale(1.5) × AliveTime(2.5) ≈ 525~600`
- 기존 베이스 무기(`SO_BaseAttackInfo`)도 이미 `240 × 2 = 480`

즉 **차지 샷이 아니어도 기존 무기 사거리가 컬링 반경의 약 10배**다.

**실제 영향:** 플레이어로부터 50 유닛보다 먼 운석은 총알이 시각적으로 명백히 관통해도 Box-Circle 판정 자체가 안 일어난다(그리드에서 아예 빠져 있으므로). 차지 샷은 사거리가 더 길어서 이 사각지대에 노출되는 범위가 더 넓다. 몬스터(Circle-Circle, 브루트포스 판정이라 이 컬링과 무관)에는 영향 없음 — 운석 등 Box 콜라이더 대상에만 해당.

**이번 기능(차지 샷 무기)과의 관계:** 차지 샷이 만든 문제는 아님(기존 베이스 무기도 이미 컬링 반경을 크게 초과) — 다만 사거리를 1.5배 더 늘려 기존 사각지대를 더 넓힘.

**아직 고치지 않은 이유:** 씬 전역 튜닝 값이라 차지 샷 기능 스코프 밖. `m_fMaxBulletRange`가 의도적으로 짧게 잡힌 이유(성능 우선 트레이드오프인지, 단순 설정 누락인지)를 기획 의도 확인 없이 임의로 바꾸지 않음.

**제안하는 해결책:** `m_fMaxBulletRange`를 실제 최대 사거리(현재 기준 약 600) + 최대 Box `BoundingRadius`(약 42) 이상으로 올리는 것을 검토. `unity-optimizer` 실측 결과, 씬의 운석 분포(129개) 기준으로 이렇게 올려도 그리드 셀당 평균 밀도가 낮아(셀당 약 0.13개) 프레임 비용 증가는 미미할 것으로 예상됨(총알 1발당 `CheckPair` 약 3~4회 수준).

## 씬에 미리 배치된 일부 몬스터가 죽을 때 `ObjectPool.PushObject`에서 `ArgumentNullException`(key null) 발생 (2026-08-21, 플레이어 차지 샷 무기 Play Mode 검증 중 발견)

**근본 원인:** `ObjectPool.PushObject(GameObject)`(`Assets/3D/05_Manager/Pool/ObjectPool.cs:232`)는 `m_hashPool.TryGetValue(refPoolObj.PoolKey, ...)`로 반납할 큐를 찾는데, `PoolObject.PoolKey`(`Assets/3D/05_Manager/Pool/PoolObject.cs:22`)는 `m_refOriginalPoolObj`를 그대로 반환하는 게터다. `m_refOriginalPoolObj`는 오직 `ObjectPool`의 프리로드 경로(`LoadPoolAsync` 내부 루프, `ObjectPool.cs:163` `refInstancePoolObj.SetOriginalPoolObj(refPrefabPoolObj)`)에서만 채워진다 — 즉 **런타임에 `ObjectPool.GetObject()`로 꺼낸 인스턴스만 `PoolKey`가 채워져 있고, 에디터에서 씬에 미리 배치해둔(preplaced) 몬스터 인스턴스는 이 경로를 거치지 않아 `PoolKey`가 계속 `null`이다.** `Dictionary.TryGetValue(null, ...)`는 `ArgumentNullException`을 던진다.

**실제 영향:** Play Mode에서 미사일(`Missiles`)이 이런 씬 배치 몬스터를 죽이면 `Monster.Dead()` → `ObjectPool.PushObject(gameObject)`에서 예외가 발생한다(재현 스택: `ColliderManager.LateUpdate → CheckOverlaps → CheckCrossLayer → CheckPair → BaseCollider.OnEnterCollider → Missiles.Attack → Bullet.Attack → Monster.TakeDamage → Monster.Dead`). 예외가 나는 시점은 이미 `ChangeState(eEntityState.Dead)`와 `OnMonsterDied` 이벤트 호출 *이후*라 게임 로직상 죽음 처리 자체는 진행되지만, 오브젝트가 풀에 반납되지 않고 `SetActive(false)`도 안 된 채로 예외가 전파되어 `ColliderManager.CheckOverlaps()`의 나머지 쌍 검사가 그 프레임에 중단될 수 있다.

**이번 기능(차지 샷 무기)과의 관계:** 무관함 확인 완료. 스택 트레이스가 지나는 `Missiles.Attack`/`Bullet.Attack`/`Monster.Dead`/`ObjectPool.PushObject` 어디도 이번 세션에서 수정한 파일이 아니고, `ColliderManager.CheckCrossLayer`(브루트포스 경로)는 이번에 손댄 `CheckCrossLayerGrid`(Box 그리드 경로)와 다른 메서드다. 씬에 미리 배치된 몬스터가 원래부터 갖고 있던 구조적 문제이며, 차지 무기가 아니어도 기존 어떤 무기로 죽여도 재현될 것으로 보인다.

**아직 고치지 않은 이유:** 이번 세션 작업 범위(차지 샷 무기 추가) 밖의 기존 몬스터 스폰 구조 문제.

**제안하는 해결책:** `ObjectPool.PushObject`에 `refPoolObj.PoolKey == null` 가드를 추가해 예외 대신 조용히 반납을 건너뛰거나(반창고), 더 근본적으로는 씬에 미리 배치되는 몬스터도 `Awake`/`Start` 시점에 자기 프리팹을 `PoolKey`로 등록하도록 `Monster`/`PoolObject` 초기화 경로를 보완. 또는 애초에 몬스터를 씬에 미리 배치하지 않고 전부 `ObjectPool.GetObject()`로 스폰하는 정책으로 통일.

## 여러 매니저 싱글톤이 `DontDestroyOnLoad only works for root GameObjects` 에러를 매 Play 진입마다 던짐 (2026-08-19, 운석 Box-Circle 충돌 판정 작업 중 Play Mode 검증하다 발견)

**근본 원인:** `BattleManager.cs:40`, `InputManager.cs:46`, `ObjectPool.cs:58`, `CameraManager.cs:29`, `FeatureManager.cs:43`, `ContainerManager.cs:20` — 이 6개 싱글톤 매니저가 전부 `Awake()`에서 `DontDestroyOnLoad(gameObject)`(또는 `this`)를 호출하는데, `DontDestroyOnLoad`는 루트(부모 없는) GameObject에만 적용 가능하다는 Unity 제약이 있다. 이 매니저들의 GameObject가 BattleScene 안에서 어떤 부모(오브젝트 구조 정리용 컨테이너 등) 아래 자식으로 배치돼 있어서 매번 이 에러가 난다.

**실제 영향:** Play Mode 진입/씬 로드마다 콘솔에 에러 6개가 찍힌다. `DontDestroyOnLoad` 자체는 조용히 무시되고(예외로 죽지 않음) 해당 오브젝트는 그냥 씬 전환 시 파괴되는데, 이 매니저들이 원래 "씬 넘어가도 살아남아야 하는" 부트스트랩 매니저로 설계됐다면(`architecture.md`의 싱글톤 가이드 참고) 실제로 씬 전환을 하는 순간 사라져서 `NullReferenceException`으로 이어질 잠재 위험이 있다. 이번 세션 검증 범위(BattleScene 단일 씬, 씬 전환 없음)에서는 크래시로 이어지지 않아서 발견만 하고 넘어감.

**아직 고치지 않은 이유:** 운석 충돌 판정 작업(`ColliderManager`/`BaseCollider` 등) 범위 밖의 기존 씬 구조 문제 — 이번 세션에서 건드린 어떤 코드와도 무관하게(내가 만든 `BaseCollider`/`ObbCollider`는 `DontDestroyOnLoad`를 아예 호출하지 않음) 원래부터 있던 것으로 확인됨.

**제안하는 해결책:** 6개 매니저 GameObject를 BattleScene에서 부모 없는 루트로 옮기거나(단순), `DontDestroyOnLoad(transform.root.gameObject)`로 호출부를 바꿔 루트 오브젝트 기준으로 동작하게 하거나(각 매니저가 정말 루트여야 하는지 씬 구조 의도 확인 필요), 애초에 부트스트랩 씬 패턴(`architecture.md` "씬 구성" 참고)으로 이 매니저들을 옮기는 방향도 검토.

## `ChargeWeapon.Tick()`의 상태 전이(edge) 로직에 대한 테스트 공백 (2026-08-18, `unity-verifier` REFACTOR 패스에서 발견)

**근본 원인:** 이번 TDD 사이클은 순수 함수(`CalcChargeRatio`, `CalcScaledStat`, `CircleCollider.SetRadiusMultiplier`)만 테스트 대상으로 삼았음(계획서에서 의도적으로 스코프를 좁힘 — 이 프로젝트에 테스트 인프라가 전혀 없어서 첫 TDD 대상을 씬 없이 도는 로직으로 한정). `ChargeWeapon.Tick()`의 rising/falling edge 판정, 쿨다운 게이팅, `OnDisable` 리셋 같은 상태 머신 자체는 테스트가 없음.

**실제 영향:** 실제로 `unity-verifier`가 이 공백 때문에 놓칠 뻔한 회귀를 하나 찾아냈음 — `Player.Update()`가 `Fire()`와 달리 `ChargeWeapon`의 `activeSelf`를 확인하지 않아 무기가 꺼진 동안에도 계속 충전/발사될 수 있었던 버그(이번 세션에서 수정 완료). PlayMode 테스트가 있었다면 이 종류의 회귀를 자동으로 잡았을 것.

**아직 고치지 않은 이유:** PlayMode 테스트는 씬 진입/프레임 진행이 필요해 EditMode보다 작성 비용이 크고, 이번 세션의 1차 TDD 파이프라인 시연 범위를 넘어섬.

**제안하는 해결책:** `ChargeWeapon.Tick()`을 대상으로 `[UnityTest]` PlayMode 테스트 추가 — (1) 누르고 있는 동안 비활성화되면 충전량이 리셋되는지, (2) `activeSelf == false`인 동안 `Tick`이 호출돼도 충전/발사가 안 일어나는지, (3) `m_fReleaseCooldown` 내 재발사가 막히는지.

## `Player.prefab`이 BattleScene 실사용 Player와 어긋난 구버전으로 방치됨 (2026-08-18, 차지 공격 씬 배선 중 발견)

**근본 원인:** `unity-scene-builder`가 `ChargeWeapon`을 씬에 배선하려고 확인한 결과, `Assets/3D/02_Player/Prefab/Player.prefab`은 `Weapon` 2개만 갖고 있는데 실제 `BattleScene.unity`의 `DynamicObject/MainPlayer/Player`는 `Weapon` 15개를 갖고 있고 그 프리팹의 인스턴스가 아닌 것으로 보임(연결이 끊긴 별도 개체). 추가로 `DynamicObject/MainPlayer/Cach`라는 두 번째 `Player` 컴포넌트가 씬에 비활성 상태로 존재함(`activeInHierarchy == false`라 지금은 `Awake()`가 안 돎).

**실제 영향:** 지금은 크래시가 안 나지만(이번 세션에서 `Player.Awake()`/`Update()`의 `m_refChargeWeapon` 호출에 null 가드를 추가해 방어함), `Player.prefab`을 실제로 인스턴스화하거나 `Cach` 오브젝트가 활성화되는 순간 `m_refChargeWeapon`뿐 아니라 다른 미할당 참조로도 문제가 생길 잠재적 소지가 있음. 또한 `Player.prefab`이 실제 플레이 가능한 씬과 동기화가 안 돼 있어, 이후 누군가 "프리팹 기준으로" 플레이어를 수정하면 씬의 실제 동작과 어긋날 위험이 있음.

**아직 고치지 않은 이유:** 이번 세션 범위(차지 공격 기능 추가) 밖 — 왜 두 구조가 갈라졌는지(의도된 실험용 백업인지, 방치된 잔재인지) 기획 의도 확인이 필요함.

**제안하는 해결책:** `Player.prefab`을 씬의 실제 `MainPlayer` 구성으로 갱신하거나, 더 이상 안 쓰는 게 맞다면 삭제 검토. `Cach` 오브젝트도 용도 확인 후 정리.

## 씬 전환 시 `BulletMoveManager`의 `TransformAccessArray` 슬롯이 누수됨 (2026-08-18, `unity-optimizer`가 차지 공격 계획 검토 중 발견)

**근본 원인:** `Bullet.Awake()`(`Assets/3D/02_Player/Weapon/Bullet.cs:70`)가 `BulletMoveManager.RegisterPermanent(this)`(`Assets/3D/02_Player/Weapon/BulletMoveManager.cs:60`)로 총알을 `TransformAccessArray`(초기 capacity 5120)에 등록하는데, `RegisterPermanent`에는 대응하는 해제(등록 취소) API가 없음. 반면 `ObjectPool.ClearPool()`(`ObjectPool.cs:171-188`)은 씬 전환 시 풀에 있던 오브젝트를 실제로 `Destroy()`한다.

**실제 영향:** 씬 전환마다 파괴된 총알의 Transform 슬롯이 `TransformAccessArray`에 그대로 남아 누적됨. 당장 크래시는 아니지만, 씬을 여러 번 오갈수록 Job이 도는 배열이 점점 죽은 슬롯으로 채워짐 — 장기 세션(오래 플레이하며 씬 반복 이동)에서 메모리 누수 및 Job 스케줄링 비용 증가로 이어질 수 있음.

**아직 고치지 않은 이유:** 이 세션의 작업 범위(차지 공격 기능 추가) 밖의 기존 이슈. 차지 공격이 새 총알 풀 종류를 하나 더 추가하면 누수 속도가 약간 빨라지긴 하지만, 원인 자체는 기존 `Bullet`/`BulletMoveManager`/`ObjectPool` 구조에 있음.

**제안하는 해결책:** `BulletMoveManager`에 `Deregister(int _iIndex)` 같은 해제 API를 추가하고, `ObjectPool.ClearPool()`이 총알류 오브젝트를 파괴하기 직전에 호출하도록 연결.

## [해결됨] `jq`가 시스템 PATH에 없어 대부분의 훅이 조용히 no-op됨 (2026-08-18)

**근본 원인:** `.claude/hooks/_lib.sh`와 거의 모든 훅 스크립트(`gateguard.sh`, `quality-gate.sh`, `session-save.sh`, `session-restore.sh`, `stop-validate.sh`, `guard-project-config.sh`, 신규 `guard-plan-phase.sh`/`guard-tdd-red.sh` 등)가 `jq`를 필수로 사용하는데, 이 머신에는 `jq`가 WinGet으로 설치되어 있음(`jq-1.8.2`, 경로: `C:\Users\user\AppData\Local\Microsoft\WinGet\Packages\jqlang.jq_Microsoft.Winget.Source_8wekyb3d8bbwe\jq.exe`)에도 **PATH에는 잡혀있지 않음**(WinGet Links 심볼릭 링크가 생성되지 않은 것으로 보임 — `%LOCALAPPDATA%\Microsoft\WinGet\Links\`가 비어 있음). Git Bash `$PATH`와 PowerShell `Get-Command jq` 양쪽에서 모두 확인되지 않아 Git-Bash 한정 문제가 아니라 시스템 PATH 자체의 문제로 보임.

**실제 영향:** `jq`를 호출하는 훅 스크립트는 `set -euo pipefail` 때문에 `jq` 호출 시점에 "command not found"로 즉시 종료(exit 127)됨. PreToolUse 차단 훅(`gateguard.sh` 등)은 이 경우 차단하지 못하고 그냥 통과되며, 상태 기록/경고 훅들도 아무 것도 기록하지 않음 — 즉 지금까지 이 프로젝트의 훅 기반 안전장치 대부분이 사실상 비활성 상태였을 가능성이 높음. (직접 전체 경로로 `jq`를 지정하면 로직 자체는 정상 동작하는 것을 신규 훅 2개로 확인함.)

**아직 고치지 않은 이유:** PATH 수정은 이 저장소 밖의 사용자 개인 환경 설정이라 사용자 확인 없이 임의로 건드리지 않음.

**해결:** WinGet 패키지 경로의 `jq.exe`를 `C:\Users\user\bin\jq.exe`로 복사(해당 디렉터리는 PATH 최우선순위였지만 실제로는 존재하지 않아 새로 생성함). Git Bash에서 PATH override 없이 `which jq` → `/c/Users/user/bin/jq` 로 정상 해석되는 것, `guard-plan-phase.sh`가 수동 PATH 지정 없이 정상 차단(exit 2)하는 것을 재검증 완료.

