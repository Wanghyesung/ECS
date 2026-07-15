# DEVLOG

## 2026-07-15

### PlayerMovement 부스트 잔여 속도 제거
- **문제**: `MaxRoll`(배럴롤) 시 `ApplySpeedBoost`로 걸리는 롤 방향 가산 속도(`vBoostSpeed`)가 `FixedUpdate`에서 계산만 되고 실제 이동에 반영되지 않았음. 또한 `m_fBoostValue`가 감쇠 후 최소 1.0에서 멈추기 때문에, 그대로 가산에 썼다면 부스트가 끝나도 잔여 속도가 사라지지 않는 구조였음.
- **수정**: `Assets/02_Player/PlayerMovement.cs`
  - `vBoostSpeed`를 `vNewPos` 계산에 실제로 더해서 롤 부스트가 이동에 반영되도록 연결
  - 가산량을 `m_fBoostValue`가 아닌 `(m_fBoostValue - 1.0f)`로 계산해, 부스트가 끝나는 시점(`m_fBoostValue == 1.0f`)에 가산 속도가 정확히 0으로 수렴하도록 변경
  - 기본 이동 배율용 `m_fBoostValue`(최소 1.0 유지)와 가산용 부스트 속도의 역할을 분리

### Bullet 도착(명중/AliveTime 만료) 로직을 SO Action으로 분리
- **배경**: `Missiles`가 `PoolObject.OnPush`에 `SpawnExplosion`을 직접 구독하는 구조였음. 향후 "도착 시 사방으로 총알을 뿌리는" 것처럼 더 복잡한 도착 이벤트가 추가될 경우, 서브클래스별로 구현이 늘어나며 조합이 꼬일 우려가 있어 구조를 논의.
- **결정**: 몬스터 BT의 `SONode` 패턴(SO 기반 Action, 인스펙터에서 조합/교체 가능)을 총알 도착 이벤트에도 동일하게 적용.
- **변경 사항**
  - `Assets/02_Player/Weapon/BulletArriveAction.cs` (신규): `Execute(Bullet _refOwner)`를 갖는 abstract SO 베이스
  - `Assets/02_Player/Weapon/SOSpawnExplosionAction.cs` (신규): 기존 `Missiles.SpawnExplosion` 로직을 이전한 구체 Action
  - `Assets/02_Player/Weapon/Bullet.cs`: `[SerializeField] BulletArriveAction[] m_arrArriveActions` 추가, `OnEnable`에서 `PoolObject.OnPush`에 `RunArriveActions` 공통 구독 → 모든 Bullet 계열이 프리팹 단위로 도착 이벤트를 조합 가능해짐
  - `Assets/02_Player/Weapon/Missiles.cs`: `m_refExplodeObj`, `SpawnExplosion()`, base 호출만 남은 `OnEnable`/`OnDisable` 오버라이드 제거
- **에디터 후속 작업 필요**: `SO_SpawnExplosionAction` 에셋 생성 후 기존 폭발 프리팹 재할당, `Missiles.prefab`의 `Bullet.m_arrArriveActions`에 등록
- **별개로 발견한 이슈**: `Missiles.UpdateDirMissile()`에서 목표 근접 시 조기 반납(`SetAliveTime(0)`)하던 `fArriveDist` 체크 블록이 현재 코드에는 빠져 있어 `fMoveDist`가 미사용 상태이고, 미사일이 타겟을 통과할 수 있음. 이번 작업 범위 밖이라 수정하지 않음.
