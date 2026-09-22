# 플레이어 차지 샷 무기 — 설계 회고

- 세션: 2026-08-21 (단일 세션)
- 브랜치: `devel/PlayerWeapon`
- 관련 시스템: `Weapon`, `SOWeaponFireBehavior`(신규), `SOChargeShotBehavior`(신규), `Bullet`,
  `CircleCollider`, `BoxColliderGrid`, `ColliderManager`, `Player`, `Drone`
- 관련 커밋: 미커밋(이 문서 작성 시점 기준 워킹트리에 있음, 커밋 메시지는 별도로 정리해둠)

이 문서는 "무엇을 만들었는지"보다 **왜 이 구조로 귀결됐는지, 그 과정에서 어떤 제안이
반려됐고 왜 반려됐는지**에 초점을 둔다. 최종 코드의 상세 근거는 각 클래스 상단 주석을
참고. `Collider.md`/`Retrospective_CollisionMoveSystem.md`와 같은 톤으로 작성함.

---

## 1. 시작 요구사항

"플레이어 차지 샷 무기를 만들고 싶다. 기존 `Weapon` 클래스 구조에 최대한 맞추되, 더 좋은
방법이 있으면 바꿔도 된다. `WeaponType`이 Charge면 입력 없이 자동으로 기가 모이고, 차지가
끝나면 자동으로 쏜다. 차지된 시간에 따라 총알의 크기와 속도가 증가한다."

핵심 제약은 두 가지였다: (1) 플레이어가 아무 것도 안 눌러도 자동으로 차징/발사되는 것,
(2) 기존 `Weapon`/`Player`/`Bullet` 구조를 최대한 깨지 않는 것. 이 프로젝트는 `Player.Update()`가
매 프레임 각 무기의 `CheckTime()`을 폴링해서 쿨다운이 다 차면 즉시 쏘는 자동연사 구조라,
"입력 없이 자동"이라는 요구사항 자체는 기존 구조와 잘 맞았다.

---

## 2. 설계 타임라인 — 세 번 갈아엎음

### 2-1. 1차 설계 — `Player.Fire()`에 타입 분기, `Weapon`이 상태 머신을 직접 보유

Claude(나)는 계획 단계에서 `Weapon.eWeaponType.Charge`를 추가하고, `Player.Fire()`에서
`if (weapon.WeaponType == Charge) UpdateCharge(...) else ...CheckTime()/Fire()...`로
분기하는 안을 세웠다. 차지 진행률/배율은 `Weapon`에 정적 함수 3개(`GetChargeRatio`,
`GetChargeSizeScale`, `GetChargeSpeedMultiplier`)로 두고, `Weapon`이 `m_bIsCharging`/
`m_fChargeStartTime` 상태를 직접 들고 있는 구조였다.

**`unity-critic` 1차 검토(사용자 승인 하에 파이프라인 규칙대로 최대 2회 실행)**에서
구조적 결함을 다수 지적받았다:
- 조기 발사가 없는 무기라 완충 시 항상 100% 비율로만 발사되는데, 크기/속도 계산에
  `Lerp(ratio, ...)`를 쓰고 있어서 **`ratio < 1`인 경로가 프로덕션에서 단 한 번도
  실행되지 않는 죽은 코드**였다.
- 무기가 카드로 비활성화되는 동안 `m_fChargeStartTime`이 얼어붙어, 재활성화 시 "경과시간
  수십 초"로 오인해 즉시 완충 발사되는 버그.
- 총알 반지름이 커지면(`MaxChargeSizeScale`) `BoxColliderGrid`가 전제로 삼는 "이웃
  1겹만 봐도 안 놓친다"는 불변식이 깨져서, 작은 운석 근처에서 판정을 놓칠 수 있음(수학적
  증명까지 요구해서 실제로 셀 크기 공식을 유도해 확인함).
- 오브젝트 풀 고갈 시 `FireCharged`가 실패해도 `m_bIsCharging`을 무조건 리셋해서, 다
  채운 차지가 조용히 증발하는 문제.
- `Drone.Fire()`가 `Weapon.CheckTime()/Fire()`를 직접 호출하는데, Charge 타입 SO를
  실수로 여기 물리면 차징 없이 즉발돼버리는 구조적 구멍.

**2차 검토**에서는 1차 수정 자체의 오류를 다시 잡았다 — `BoxColliderGrid` 링 개수
계산식이 수학적으로 틀려 있었고(`frac(r/S) > 0.5`인 경우 등에서 여전히 한 링 모자람),
`Bullet.SetAttack()`에서 스케일 적용 메서드를 정확히 어디서 호출해야 하는지 명시가
빠져 있어서(잘못 넣으면 풀 재사용 직전 값을 읽거나 예고선이 스케일 적용 전 크기로
그려짐) 정확한 호출 위치를 코드에 못박았다.

파이프라인 규칙상 critic 검토는 최대 2회로 캡이 걸려 있어, 2차 검토 이후 남은 우려사항은
"알려진 리스크"로 계획 파일에 기록해두고 진행하는 방식으로 정리했다.

### 2-2. 사용자 피드백 — "if 분기로 나누면 나중에 또 늘어나면 또 if문 쓸 거잖아"

`unity-critic` 검토를 반영해 계획을 다듬은 뒤 구현 흐름을 설명하자, 사용자가 정확히
이 지점을 짚었다: `Player.Fire()`의 타입 분기가 앞으로 발사 방식이 늘어날 때마다
계속 늘어나는 구조라는 것. Claude는 이걸 받아들여 **다형성**으로 바꿨다 — `Weapon`에
`virtual UpdateWeapon()`을 두고, 기본 구현은 기존 즉발 그대로, 차지 전용 상태 머신은
`Weapon`을 상속하는 `ChargeWeapon` 서브클래스가 오버라이드하는 구조. 이 프로젝트에 이미
있는 `Bullet`(base) → `Missiles`/`GuidedBullet`(서브클래스) 패턴과 동일한 모양이라
낯설지 않다고 판단했고, `architecture.md`의 "MonoBehaviour 상속 최대 깊이 2" 규칙도
지키는 설계였다.

### 2-3. 사용자 재피드백 — "상속보다 조합으로 가자"

이 안을 다시 설명하자, 사용자가 한 번 더 파고들었다: 차지샷 이후로도 발사 방식이 여러 개
계속 늘어날 걸로 예상되며, 그렇다면 상속 트리를 계속 늘리는 것보다 조합(컴포지션) 쪽을
선호한다는 방향을 `AskUserQuestion`으로 명확히 확인받았다. 이건 `architecture.md`가
이미 명시한 "MonoBehaviour는 컴포넌트이지 베이스 클래스가 아니다. 깊은 상속 트리를 만들지
마라"는 원칙과도 정확히 일치하는 방향이었다.

**최종 구조**: `SOWeaponFireBehavior`(추상 ScriptableObject)를 만들고, `Weapon`이 이걸
`[SerializeField]`로 조합해서 `Tick()`(현재 이름은 `UpdateWeapon(Weapon, pos, target)`)에
위임한다. `Player`/`Drone`은 `weapon.UpdateWeapon(...)` 하나만 호출하면 되고 무기가 어떤
정책을 쓰는지 전혀 몰라도 된다. `SOChargeShotBehavior`가 이 인터페이스의 첫 구현체다.
이 패턴은 이 프로젝트에 이미 있는 "SO 기반 Action 조립"(`SOBulletAction`, BT의 SO Action
노드)과 결이 같아서 역시 낯선 개념을 새로 들여온 게 아니었다.

런타임 상태(차징 타이머)를 SO 인스턴스가 갖는 게 "SO는 데이터만 가져야 한다"는 규칙에
위배되는 것 아니냐는 우려가 있었는데, `Weapon.Init()`이 인스펙터에 할당된 원본을
`Instantiate()`로 클론해서 그 클론에만 접근하는 구조라, `architecture.md`가 이미 명시한
"BT Composite 노드는 Instantiate로 클론되는 한 런타임 상태를 가져도 된다"는 예외 조항에
정확히 해당한다고 판단해 그대로 유지했다. `unity-reviewer`도 이 부분을 별도로 확인해서
문제없다고 답했다.

### 2-4. 세 설계의 실질적 차이

| | 1차 (if 분기) | 2차 (상속) | 3차 (조합, 최종) |
|---|---|---|---|
| 새 발사 방식 추가 시 `Player.cs` | 매번 수정 | 안 바뀜 | 안 바뀜 |
| 새 발사 방식 추가 시 `Weapon.cs` | 매번 수정 | 안 바뀜(가상 메서드 오버라이드만 추가) | **다시는 안 바뀜**(정책 SO만 추가) |
| 씬에서 무기 교체 | 컴포넌트 자체를 바꿔야 함 | `ChargeWeapon` 컴포넌트로 교체 | 같은 `Weapon` 컴포넌트에서 SO 필드만 교체 |
| 기존 무기 프리팹 영향 | 없음 | 없음 | 없음(필드 비워두면 기존 동작 그대로) |

3차 구조가 씬 작업(컴포넌트 타입을 안 바꿔도 됨)까지 포함해서 가장 마이그레이션 비용이
적었다.

---

## 3. TDD 파이프라인 실행 요약

`/unity-tdd-pipeline`을 그대로 따랐다: Plan(critic 2회) → RED → GREEN → REVIEW
(`unity-reviewer` 1회) → INTEGRATE → OPTIMIZE(`unity-optimizer` 1회).

- **TDD 대상**: `SOChargeShotBehavior.GetChargeRatio(elapsed, maxChargeTime)` 하나로
  좁혔다. 이 프로젝트의 기존 테스트 컨벤션(`ColliderManagerTests`)이 씬 의존 없는 순수
  함수만 검증하는 방식이라 여기 맞췄다. 크기/속도 배율은 별도 함수 없이 완충 시 필드를
  직접 곱하는 걸로 정리했다(1차 critic이 지적한 죽은 `Lerp` 코드를 반영한 결과).
- **RED**: `Assets/Tests/Editor/ChargeShotBehaviorTests.cs`에 완료 기준 5개(경과 0/전체/
  초과/절반/`MaxChargeTime<=0` 가드) 작성 → `NotImplementedException`으로 5개 전부 실패
  확인.
- **GREEN**: `Mathf.Clamp01` 기반 최소 구현 → 5개 전부 통과.
- **REVIEW**: `unity-reviewer` 1회. `Bullet.SetAttack()`에서 스케일 적용 순서, `SizeScale`
  기본값 0 회귀, `?.` 대신 `== null` 규칙, SO 런타임 상태 예외 조항 해당 여부, 
  `BoxColliderGrid`/`ColliderManager` 변경이 기존 판정을 안 깨뜨리는지를 확인 — 치명적
  이슈 없음.
- **INTEGRATE**: MCP로 `SO_ChargeAttackInfo`/`SO_ChargeShotBehavior` 에셋 생성, 기존
  `BaseWeapon_0`을 복제해 `ChargeWeapon` GameObject 구성, `Player.m_listWeapon`에 등록.
- **OPTIMIZE**: `unity-optimizer` 1회. 아래 4절 참고.

---

## 4. 씬 배선 중 있었던 실수 (Claude 쪽)

씬 작업 도중 두 가지 실수가 있었고, 둘 다 검증 단계에서 스스로 잡았다.

1. **`Player.m_listWeapon` 리스트 재구성 실수**: MCP로 무기 목록에 `ChargeWeapon`을
   추가하면서, 이전에 읽어둔(도메인 리로드로 stale해진) 인스턴스 ID를 그대로 써서 목록을
   통째로 다시 썼다. 그 결과 `ShotgunWeapon`이 목록에서 빠지고 `BaseWeapon_0`이 중복으로
   두 번 들어가는 사고가 났다. 리스트를 그대로 다시 읽어서 대조한 뒤 발견했고, 최신 라이브
   상태만 신뢰하는 방식(이름으로 다시 검색해서 ID를 새로 확보)으로 바로잡았다.
2. **fileID 정밀도 손실**: `SO_ChargeAttackInfo`의 `PoolPrefab` 참조를 기존 에셋에서 복사한
   19자리 fileID(`6662476535643214403`)로 넣으려 했는데, MCP 도구를 거치며 JSON 숫자가
   더블 정밀도 범위(2^53)를 넘어서 매번 반올림된 값(`...215000`)으로 잘못 들어갔다. 같은
   시도를 세 번 반복한 뒤에야 원인을 정밀도 문제로 특정했고, fileID 대신 에셋 경로만으로
   참조하는(단일 컴포넌트라 자동 해석되는) 방식으로 우회해서 해결했다.

두 경우 모두 "제대로 됐는지 다시 읽어서 확인한다"는 절차 덕분에 커밋 전에 걸러졌다.

---

## 5. `unity-optimizer`가 잡아낸 것

- **씬이 저장되지 않은 상태였다**: `ChargeWeapon` GameObject, `Player.m_listWeapon` 등록,
  두 SO 필드 할당까지 전부 에디터 메모리에만 있고 `.unity` 파일엔 반영되지 않은 상태로
  작업이 이어지고 있었다. optimizer가 씬 파일에 관련 문자열이 전혀 없다는 걸 grep으로
  확인해서 지적했고, 그제서야 `manage_scene save`를 호출했다 — 이게 없었으면 에디터를
  닫는 순간 이번 세션의 씬 작업이 통째로 날아갈 뻔했다.
- **`ColliderManager.m_fMaxBulletRange = 50`이 차지 샷 사거리(~600)보다 훨씬 짧다**:
  차지 샷뿐 아니라 기존 베이스 무기(사거리 480)도 이미 이 컬링 반경을 훨씬 초과하고
  있어서, 50유닛 밖의 운석과는 애초에 충돌 판정이 안 일어나는 상태였다. 차지 샷이 만든
  문제는 아니지만 사거리를 더 늘려 사각지대를 넓혔다. 씬 전역 튜닝 값이라 임의로 바꾸지
  않고 `known-issues.md`에 근본 원인과 권장값을 기록해뒀다.
- **Play Mode 중 무관한 기존 버그 발견**: 씬에 미리 배치된 몬스터 일부가 죽을 때
  `ObjectPool.PushObject`에서 `ArgumentNullException`(key null)이 발생. 스택 트레이스를
  추적해서 `PoolObject.PoolKey`가 오직 `ObjectPool`의 프리로드 경로에서만 채워지고,
  에디터에 미리 배치된 몬스터는 이 경로를 안 거쳐 `null`로 남는다는 걸 확인 — 이번 기능과
  무관함을 스택 트레이스로 검증한 뒤 `known-issues.md`에 별도 기록.
- **자잘한 낭비 3건**: 첫 발사 시 무의미한 Transform 재대입(`m_fLastAppliedScale` 초기값
  `-1f`→`1f`), `Bullet.cs` 두 곳에 남아있던 `?.`(→ `== null`), `Weapon.Init()`이
  `Instantiate`한 SO 클론이 `OnDestroy`에서 정리되지 않던 것 — 전부 그 자리에서 바로 수정.

---

## 6. 사용자 질문에 답하며 다시 검증한 것 — 링 개수 공식의 경계값

구현이 끝난 뒤 사용자가 `BoxColliderGrid.NeighborColliders`의 링 개수 계산식을
"수학적으로" 설명해달라고 요청했다. 처음 유도했던 식(`ceil(r/S + 0.5)`, `unity-critic`
2차 검토에서도 같은 식으로 나왔던 것)을 다시 처음부터 엄밀하게 재유도하는 과정에서,
**동점(=) 경계를 어느 쪽으로 처리하느냐에 따라 결과가 갈리는 지점**을 새로 발견했다:

- 실제 겹침 판정(`distance <= r+R`)은 동점도 겹침으로 치는데, 원래 유도는 "동점이면
  안전하게 제외해도 된다"는 쪽으로 암묵적으로 잡고 있었다.
- 완전히 엄밀하게(동점도 놓치지 않게) 하려면 `floor(r/S + 1.5)`가 맞고, 이건
  `r/S`가 정확히 반정수(0.5, 1.5, 2.5...)일 때만 기존 식보다 링이 하나 더 필요하다.
- 다만 이 경계는 "씬에서 가장 큰 운석의 반지름이 정확히 셀 크기의 절반"이면서 "쿼리
  원과 그 운석이 정확히 셀 반대쪽 끝에 딱 붙어있는" 두 조건이 동시에 맞아야 해서,
  부동소수점 좌표에서는 사실상 재현되지 않는 이론적 경계다.

이건 1차/2차 `unity-critic` 검토와 Claude 본인 둘 다 놓쳤던 지점이고, 사용자가 "왜 이렇게
했는지 수학적으로 설명해달라"고 다시 파고든 덕분에 뒤늦게 발견됐다. 코드는 고치지 않고
(실용적으로 무의미한 차이라 판단) 사용자에게 그대로 알렸다 — 더 보수적으로 가고 싶으면
`+0.5`→`+1.5`, `Ceil`→`Floor` 한 줄로 바꿀 수 있다는 것도 함께.

---

## 7. 잘 됐던 접근

- **`unity-critic` 2회 검토가 실제로 구조적 결함을 걸러냈다**: 죽은 `Lerp` 코드, 무기
  비활성화 중 상태 동결, 풀 고갈 시 차지 증발, `BoxColliderGrid` 불변식 붕괴 — 전부
  구현 전에 텍스트 리뷰 단계에서 잡혔다. 코드를 실제로 짜고 나서 발견했다면 훨씬 비쌌을
  변경들이다.
- **사용자의 지적을 그대로 받아들이지 않고 한 단계 더 검증해서 반영**: "if 분기가 늘어난다"는
  지적을 받았을 때 상속으로 먼저 바꿨는데, 곧바로 "상속도 결국 계속 늘어나는 구조 아니냐"는
  재지적을 받고서야 조합으로 다시 바꿨다 — 한 번에 최종 구조로 못 간 건 아쉽지만, 각 단계의
  피드백을 즉시 반영하며 수렴한 과정 자체는 정상적인 설계 논의였다.
- **씬 배선 직후 반드시 실제로 검증**: Play Mode를 몇 초라도 돌려서 런타임 예외를 직접
  확인했고, `unity-optimizer`에게 "씬 배선까지 포함해서" 점검을 요청한 덕분에 씬 미저장
  같은, 정적 코드 리뷰만으로는 절대 못 잡는 문제를 잡아낼 수 있었다.
- **무관한 기존 버그를 발견했을 때 스코프를 지켰다**: `ObjectPool` 예외와
  `m_fMaxBulletRange` 문제 둘 다 스택 트레이스/코드 추적으로 "이번 기능과 무관함"을
  먼저 증명한 뒤에야 "고치지 않고 기록만 한다"고 판단했다 — 추측으로 스코프를 판단하지
  않았다.

## 8. Claude 쪽에서 아쉬웠던 점

1. **1차 설계에서 상속 트리가 늘어나는 문제를 스스로 예측 못 함**: `architecture.md`에
   "상속보다 조합을 우선하라"는 원칙이 이미 명시돼 있었는데도, 첫 설계는 곧바로 상속
   기반으로 짰다. 사용자가 두 번(if 분기 → 상속) 연달아 지적하고 나서야 조합으로
   갔다 — 프로젝트 규칙을 계획 단계에서 한 번 더 대조했으면 한 번에 갔을 왕복이었다.
2. **링 개수 공식의 경계값 처리(동점 취급)를 처음부터 엄밀하게 안 함**: `unity-critic`
   2차 검토에서 나온 식을 그대로 코드에 반영했는데, 그 식 자체도 "동점이면 안전하게
   제외"라는 암묵적 가정을 깔고 있었다. 사용자가 재차 수학적 설명을 요구하지 않았다면
   이 경계 조건은 계속 묵인된 채로 남았을 것 — 실용적으로는 무해하지만, 처음 유도할 때
   더 엄밀하게 갔어야 하는 지점이었다.
3. **씬 저장을 스스로 챙기지 못함**: MCP로 GameObject/컴포넌트를 다 만들고도 저장을
   깜빡했다. `unity-optimizer`가 잡아줬기에 망정이지, 점검을 요청하지 않았다면 이번
   세션의 씬 작업이 통째로 유실될 뻔했다 — 앞으로는 씬 변경이 끝나는 시점에 저장을
   기본 절차로 넣어야 한다.
4. **fileID를 JSON 숫자로 그대로 전달하다 정밀도 손실을 세 번 반복**: 같은 실수를
   반복하지 않고 원인을 특정하는 데 시행착오가 있었다 — 큰 정수 ID를 다루는 도구는
   처음부터 문자열/경로 참조를 우선 시도했어야 했다.

---

## 9. 다음에 확인/검토할 것

- [ ] `ColliderManager.m_fMaxBulletRange`를 실제 사거리 기준(≥600)으로 올릴지 결정 —
      씬 실측(운석 129개, 평균 밀도 낮음) 기준 올려도 프레임 비용 증가는 미미할 것으로
      추정됨(`known-issues.md` 참고)
- [ ] 씬에 미리 배치된 몬스터의 `PoolObject.PoolKey` 미등록 문제(`ArgumentNullException`)
      — 이번 세션 스코프 밖, `known-issues.md`에 근본 원인/해결책 기록해둠
- [ ] 데미지는 이번 요구사항(크기/속도만 증가)대로 스케일하지 않았음 — 발사 주기가
      늘어나는데 데미지는 그대로라 순수 DPS가 낮아질 수 있어 밸런싱 검토 필요
- [ ] `BoxColliderGrid.NeighborColliders`의 링 개수 공식을 더 엄밀한
      `floor(r/S + 1.5)`로 바꿀지 여부(6절 참고) — 현재 씬에서는 차이가 없어 보류
- [ ] `SpawnAttackObject`가 총알 하나당 `GetComponent<IAttackObject>()`를 두 번 호출하는
      기존 패턴(`unity-optimizer`가 발견, 이번 기능이 만든 문제는 아님) — 반환값을 넘기는
      방식으로 절반으로 줄일 수 있음
