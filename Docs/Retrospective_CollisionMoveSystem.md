# 충돌/이동/풀 시스템 리팩터링 회고

- 기간: 2026-08-01 ~ 2026-08-02 세션, 2026-08-20 세션(총알 관통 버그 + 레이 판정 + 프로파일링)
- 관련 시스템: `CircleCollider`, `ColliderManager`, `Bullet`/`Missiles`/`GuidedBullet`,
  `BulletMoveManager`/`MissileMoveManager`/`GuidedMoveManager`, `ObjectPool`/`PoolObject`
- 관련 커밋: `93bc2df`, `2b8a7a4`, `d95b434` (+ 이 문서 작성 시점 기준 미커밋 변경분)

이 문서는 "무엇을 만들었는지"보다 "어떤 과정을 거쳤고, 왜 그 결정을 했는지"에 초점을 둔
회고 문서다. 설계 자체의 상세 근거는 각 클래스 상단 주석과 `DEVLOG.md`를 참고.

---

## 1. 시작 문제

프로파일러에서 `Bullet.FixedUpdate()`가 병목으로 잡힘. 목표는 총알 5000발 동시 존재 시
60FPS 유지 (저사양 타겟 머신 기준). 여기서 출발해 두 갈래로 파봤다.

1. 총알 이동 자체를 Job/Burst로 병렬화할 수 있는가
2. PhysX `OnTriggerEnter`/`OnTriggerStay` 기반 충돌 판정이 병목인가, 자체 판정으로 대체할 수 있는가

---

## 2. 전체 타임라인

### 2-1. Job/Burst 1차 시도 (보류)
`JobBullet`/`JobMissile`/`JobGuidedBullet` + 전용 MoveManager 3종을 Rigidbody 기반으로 만들어
측정. 기존 Mono/Rigidbody 방식 대비 명확한 이득이 없었고 복잡도만 커져서 채택하지 않고
보류(코드는 남겨둠). `DEVLOG.md`에 상세 기록.

### 2-2. PhysX 트리거 → CircleCollider 자체 판정 도입
Rigidbody 기반 트리거 판정을 걷어내고, `CircleCollider`(자체 원/구 충돌) + `ColliderManager`로
교체. 총알 이동도 `FixedUpdate`+Rigidbody에서 `Update`+Transform 직접 이동으로 전환(트리거용
Rigidbody가 필요 없어졌으므로).

### 2-3. 공간 그리드 여러 번 반복 → 결국 폐기
`ColliderManager` 내부 자료구조를 여러 차례 갈아엎음:
- 셀 크기 2로 시작 → GC.Alloc 발견(셀별 `List<int>`가 최초 사용 시 계속 새로 생김)
- 맵 범위(±500) 기준 사전 분할 시도, 셀 크기를 20으로 키워봤는데 오히려 더 느려짐
  (프로파일러로 확인 — 셀당 후보 밀도가 늘어 오히려 손해)
- 결론: 총알(다수) vs 몬스터(소수) 구조에서, 그리드의 "후보 줄이기" 이득보다 그리드 자체
  유지 비용이 더 크다고 판단 → 그리드 대신 다른 방식으로 전환

### 2-4. 레이어 매트릭스 + 순수 리스트 방식으로 정착
Unity Physics의 Layer Collision Matrix와 동일한 개념을, `ColliderManager`가 레이어 32개
고정 배열로 들고 있다가 Activate/UnActivate 시점에 O(1) 스왑백으로 유지하는 방식으로 전환.
`CircleCollider`는 개별 `LayerMask`를 버리고 `gameObject.layer` 캐싱만 함.

### 2-5. 직접 재작성한 버전 리뷰 → 수정
`ColliderManager`를 새로 작성한 뒤 리뷰 요청을 받아 살펴봤고, 다음을 발견해서 정리했다:
- `RemoveAtSwapBack`이 `List<T>`엔 없는 메서드라 컴파일 자체가 안 되는 상태였음
- 충돌 판정 조건이 반전(`>`이어야 할 자리에 반대 부등호)
- `struct`를 `TryGetValue`로 꺼낸 뒤 복사본만 수정하고 Dictionary에 다시 안 씀(값 타입 특성상
  흔히 놓치는 부분)
- `tPair.ColliderB = _refA` 같은 복사-붙여넣기성 오타
- `RegisterCollider`와 `Activate`가 둘 다 리스트에 Add해서 이중 등록되는 구조
- `FindIndex(x => x == ...)` 람다가 매번 GC Alloc + O(n) 스캔을 유발
- 안 쓰는 필드(`m_arrColliderLayer`), 안 쓰는 `using System.Linq`

수정 요청을 받아 전부 고쳤고, 고치는 과정에서 리뷰 때 못 짚었던 문제도 하나 추가로 발견했다:
안 겹치는 쌍도 검사만 하면 Dictionary 항목이 영구히 쌓이는 구조였음(총알 하나가 스쳐 지나간
모든 몬스터 조합이 다 남음) — 실제로 겹칠 때만 항목을 만들고 Exit 시 제거하도록 고침.

### 2-6. 몬스터 증가 시 성능 저하 재확인
몬스터 30마리 기준 프로파일러로 `ColliderManager.LateUpdate() = 1.88ms`(전체 프레임 ~19ms
중) 확인. `CheckCrossLayer`가 O(총알 수 × 몬스터 수) 구조라 몬스터가 늘수록 총알 수만큼
비용이 곱으로 늘어남을 확인. 그리드 재도입을 논의했으나, 판정 자체가 워낙 가벼운 연산이라
그리드 버킷팅 비용이 오히려 더 클 수 있다는 우려가 있어 보류하기로 했다. 대신 `CheckPair`에서
`Center` 프로퍼티(쿼터니언 곱셈 포함)가 쌍마다 중복 계산되는 부분을 캐싱하면 실질 이득이 있다는
점만 짚고, 이 최적화는 아직 미적용 상태로 남겨둠.

### 2-7. 총알 이동 재설계: Job/Burst 부활 (통합 구조)
Job이 붙은 서브클래스는 빼고 기본 Bullet 계열에만 적용하자는 방향이 나왔고, 처음엔 이걸
"Job 자체를 안 쓴다"는 뜻으로 받아들여서 평범한 `List<Bullet>` 순회 매니저로 구현했다.
곧바로 Job 방식(Update에서 Schedule, LateUpdate에서 Complete까지 대기)으로 가야 한다는
방향 정정을 받아 다시 Job/Burst 기반으로 재작업했다. 이번엔 `JobBullet`/`JobMissile`/
`JobGuidedBullet` 같은 별도 서브클래스 없이, `Bullet`/`Missiles`/`GuidedBullet`이 가상
메서드(`RegisterMoveJob`/`ActivateMoveJob`/`UnactivateMoveJob`)로 각자 매니저에 직접
등록하는 구조로 통합했다. 기존 `JobBullet.cs` 등 3개 파일은 이후 직접 정리하기로 함.

### 2-8. TransformAccessArray로 전환
처음엔 Job이 위치만 계산하고(`NativeArray<float3>`), 메인 스레드에서 `ApplyMove()`로
Transform에 옮겨 적는 방식으로 구현했다. 이 방식이 리스트 인덱싱 + 함수 호출(네이티브 콜/
가상 호출) 오버헤드를 추가로 만든다는 지적을 받았고, Job이 `TransformAccessArray`로
Transform을 직접 쓰는 방식(`IJobParallelForTransform`)으로 전환했다. 회전까지 필요한
Missiles/GuidedBullet은 Job 안에서의 쿼터니언 연산이 Burst에서 안전한지 컴파일로 확인할
방법이 없어 처음엔 보류했는데, 그쪽도 같은 방식으로 가는 게 낫겠다는 의견을 받고 다시
검토한 끝에 `Unity.Mathematics.quaternion.LookRotationSafe`(Burst 전용으로 설계된 함수)로
안전하게 해결했다.

### 2-9. 운영 중 발견된 버그 2건 수정
- **NativeList 쓰기 경합**: Weapon 발사 로직(`Update`)이 매니저의 `Update`(Job Schedule)보다
  늦게 실행되면, 이미 Schedule된 Job이 읽는 중인 NativeList에 `Activate()`가 써서
  `InvalidOperationException` 발생. `[DefaultExecutionOrder(500)]`으로 매니저 Update가
  발사 로직보다 항상 나중에 돌도록 고정해서 해결.
- **NativeList 해제 후 접근**: 플레이모드 종료 시 매니저의 `OnDestroy()`(Dispose)가 먼저
  돈 뒤, 아직 안 죽은 총알의 `OnDisable()`이 뒤늦게 `Deactivate()`를 호출해
  `ObjectDisposedException` 발생. `m_bDisposed` 플래그로 해제 이후 호출은 조용히 무시하도록
  가드 추가.

### 2-10. 풀 오브젝트 만료 시스템 전환
`PoolObject.Update()`가 매 프레임 자기 `m_fAliveTime`을 직접 감소시키며 체크하던 방식을,
`ObjectPool`이 `PriorityQueue`(이미 `ObjectSpawner`가 쓰던 자체 구현 재사용)로 "가장 이른
만료 시각"만 매 프레임 확인하는 방식으로 전환. 구현 중 두 가지를 추가로 반영:
- **Generation(생애 번호)**: 조기에 수동 반납된 뒤 다시 발사된 오브젝트를, 낡은(이전 생애의)
  예약이 잘못 반납시키지 않도록 방지
- **`SetAliveTime(0)` 즉시 반납 케이스**: `Missiles.Arrived()`가 `SetAliveTime(0)`으로 "즉시
  반납"을 요청하는데, 최초 구현은 `<= 0`이면 예약 자체를 무시해서 도착한 미사일이 영영 안
  돌아가는 문제가 될 뻔했음 — 구현을 마무리하며 다시 살펴보다 발견해서 실제로 겪기 전에 수정.

### 2-11. ColliderManager 재측정 — 진짜 병목은 그리드가 아니라 Center 재계산이었다
총알 이동을 Job으로 전환한 뒤 몬스터를 실제로 두고 다시 재보니, 몬스터 25마리인데
`ColliderManager`만 14ms가 나옴(몬스터 30마리 기준이었던 이전 1.88ms보다 오히려 훨씬 큼).
원인을 좁히는 과정:

1. **같은 레이어끼리(총알-총알) 충돌 체크가 켜져 있는지 의심** → 확인 결과 레이어 매트릭스는
   몬스터-총알만 체크하도록 설정돼 있었음. 배제.
2. **`CheckPair`가 안 겹치는 쌍도 매번 `Dictionary.TryGetValue`를 호출하고 있던 것을 발견** →
   거리 비교에서 안 겹치면 Dictionary를 아예 안 건드리고 바로 return하도록 수정. 실제로
   겹친 쌍만 `m_hashPairInfo`에 남기고, 프레임 끝에 그 쌍들만(작은 집합) 순회해서 Exit
   판정하는 `ExitStalePairs()`로 분리(최초 커밋의 그리드 버전이 쓰던 `ExitPair()` 패턴과
   사실상 동일한 구조 — 지난 재작성 때 하나로 합치며 이 최적화가 같이 빠졌던 것으로 보임).
3. 이 수정 이후 재측정했더니 **20ms로 오히려 더 나빠짐** — 수정이 잘못됐거나, 단순히 더
   무거운 실전 조건(총알 수 증가 등)에서 잰 것인지 구분이 안 되는 상황이었음. Unity
   에디터의 프로파일러를 직접 볼 방법이 없어서, `CheckSameLayer`/`CheckCrossLayer`/
   `ExitStalePairs`에 각각 `ProfilerMarker`를 심어 Deep Profile 없이도 구간별 비용을
   정확히 볼 수 있게 함.
4. 마커 결과: `CheckCrossLayer`가 13.55ms로 `LateUpdate` 전체(13.57ms)의 사실상 전부.
   `ExitStalePairs`는 화면에 안 잡힐 정도로 작아서, 2번 수정은 문제가 아니었고(오히려 잘
   작동 중) 순수 후보 쌍을 도는 `CheckCrossLayer` 자체가 병목이라는 게 확정됨.
5. **진짜 원인 발견**: `CircleCollider.Center`(`transform.position + transform.rotation *
   m_vOffset`, 쿼터니언 곱셈 포함)가 `CheckPair`에서 쌍마다 매번 다시 계산되고 있었음 —
   총알 하나가 몬스터 수만큼, 몬스터 하나가 총알 수만큼 반복 계산되는 구조라 총알×몬스터
   조합 수만큼(수만~수십만 회) 쿼터니언 곱셈이 낭비되고 있었음.
6. **수정**: `CircleCollider`에 프레임당 한 번만 계산해서 캐시하는 값(`CenterPos()`로
   갱신, `CachedCenter`로 조회)을 추가하고, `ColliderManager`가 판정 루프 돌기 전에
   `RefreshAllCenters()`로 활성 콜라이더마다 딱 한 번씩 갱신 → `CheckPair`는 캐시값만 읽음.
7. **결과**: `RefreshAllCenters`(정상적으로 필요한 O(총알+몬스터) 비용) = 1.58ms,
   `CheckCrossLayer`는 타임라인에서 옆 항목과 겹쳐 보일 정도로 작아짐. 그리드나 Burst
   전환 같은 큰 구조 변경 없이, 캐싱 하나로 해결됨.

### 2-12. 총알 관통(피격 판정 누락) 버그 — 가설 다섯 개, 실제 원인은 둘

테스트 중 "총알이 몬스터를 맞아야 할 위치를 지나가는데 Enter가 안 뜬다"는 증상이
재현됨. `ColliderManager`를 Enter만 반복 발동하는 테스트 코드 상태로 단순화해두고
원인을 좁혀나감. 순서대로 가설을 세우고 하나씩 배제했다:

1. **Y축 배제 로직 자체의 계산 오류** — 양쪽 좌표 모두 Y=0으로 고정한 뒤 계산하는
   구조라 수학적으로 문제없음을 확인하고 배제.
2. **유도탄류 총알의 회전이 오프셋 계산을 왜곡** — `CenterPos()`가 `transform.rotation *
   m_vOffset`을 그대로 쓰는데, 목표를 추적하며 피치가 섞이는 회전이면 오프셋의 XZ 성분이
   실제로 짧아지는 결함이 맞긴 함(`MissileMoveManager`/`GuidedMoveManager`가 타겟의 Y까지
   포함한 3D 방향으로 `LookRotationSafe`를 계산하고 있어서). 다만 이번에 재현된 총알
   (MidBullet)은 오프셋이 `(0,0,0)`이라 이 결함과는 무관함을 실측(Debug Inspector)으로 확인.
3. **몬스터 피격 흔들림 연출(`VisualObject`)의 회전이 판정용 콜라이더에 영향** — 흔들림은
   판정용 콜라이더가 없는 자식 오브젝트에만 적용되는 구조라 무관함을 확인.
4. **콜라이더 등록 리스트 자체가 씹힘** — Debug Inspector로 개별 총알의 `m_bActivated`
   상태와 `ColliderManager`의 실제 등록 리스트를 직접 대조. 처음 확인한 개체는 정상이었지만,
   계속 대조하다 보니 "`m_bActivated=true`인데 실제 리스트엔 없는" 개체들을 발견 (섹션
   2-13에서 실제 원인으로 확정).
5. **터널링(이산 판정이 프레임 사이 이동 경로를 못 봄)** — "총알을 한 번에 여러 발(3연발)
   쏠 때만 증상이 심해진다"는 관찰을 근거로, 프레임당 dt 스파이크와 겹치면 빠른 총알이
   반지름 구간을 한 프레임에 건너뛸 수 있다고 판단. 직전/현재 프레임 위치를 잇는 선분과
   반지름 합 사이 최단거리를 구하는 스윕(swept) 판정을 `CircleCollider`/`ColliderManager`에
   추가 구현함.

**이후 사용자가 실측(Debug Inspector로 개별 총알 대조)을 계속 반복한 끝에, "터널링은
실제 원인이 아니었다"고 정정함.** 스윕 판정 자체는 논리적으로 성립하는 방어 코드였지만,
이번 버그의 진짜 원인이 아니었고, 3연발 조건에서 증상이 심해진 건 아래 2-13의 등록
타이밍 버그가 짧은 재사용 주기에서 더 잘 걸렸기 때문이었다. 스윕 판정 관련 코드는
섹션 2-15에서 전부 제거함 — **틀린 진단 위에 쌓은 방어 코드를 한 번 완성까지 시켰다가
되돌린 사례**로 남겨둔다.

### 2-13. 실제 원인 1 — 재사용된 콜라이더를 삭제 예약이 그대로 지워버림

`ColliderManager.Update()`가 예약된 삭제(`m_listPendingDelete`)를 처리할 때, 원래는
"지금 진짜로 비활성 상태인 것만" 지우는 가드가 있었는데(`IsActive` 필드 기반), 세션
중간에 그 필드가 다른 정리 작업 중 빠지면서 가드 없이 무조건 삭제하는 상태가 돼 있었음.

문제 시나리오: 총알이 반납되며 삭제 예약이 걸림 → 삭제가 실제로 처리되기 전(다음
프레임 `Update()`가 오기 전)에 같은 오브젝트가 빠르게 재사용(재발사)돼서 다시
활성화됨(`m_bActivated=true`로 복귀) → 그런데도 다음 프레임 `Update()`가 그 낡은 삭제
예약을 조건 없이 실행 → **방금 다시 날아가고 있는 총알을 판정 리스트에서 지워버림.**
GameObject는 화면에서 멀쩡히 움직이지만 판정 시스템에서만 조용히 빠지니, 눈에는
"매끄럽게 그냥 통과하는" 것처럼 보임 — 3연발처럼 재사용 주기가 짧은 무기일수록 이
타이밍 창에 걸릴 확률이 높아서, 그게 "총알 수가 많을 때만 심해진다"는 관찰로
이어졌던 것.

**수정**: 삭제 실행 직전에 `refCollider.gameObject.activeInHierarchy`를 다시 확인해서,
그 사이 재활성화된 것은 지우지 않도록 복구.

### 2-14. 실제 원인 2 — 피격 이펙트 풀 고갈 시 조기 `return`이 총알 반납까지 건너뜀

등록 타이밍 버그를 고친 뒤에도 일부 증상이 남아, `CheckPair`의 Enter 분기에 로그를
심어 "진짜로 Enter가 안 뜨는지"부터 확인. 결과: **Enter도 뜨고 `TakeDamage`까지 정상
실행되고 있었는데, 총알이 풀로 반납되지 않아 계속 날아가고 있었음.** 원인은
`Bullet.AttackMonster()`:

```csharp
if (m_refHitEffectObj != null)
{
    GameObject refHitEffect = ObjectPool.m_Instance.GetObject(m_refHitEffectObj);
    if (refHitEffect == null)
        return;               // ← 이펙트 풀이 비어있으면 메서드 전체를 탈출
    refHitEffect.transform.position = transform.position;
}

if (m_tShotInfo.HitCount >= m_refAttackInfo.MaxHitCount)
    ObjectPool.m_Instance.PushObject(gameObject);   // ← 위 return에 같이 스킵됨
```

피격 이펙트 풀이 고빈도 발사로 소진돼 `null`이 반환되면, 그 아래 있던 "총알을 풀로
반납"하는 코드까지 통째로 건너뛰어짐. 총알은 이미 데미지를 입힌 뒤였지만(`Damage=0`으로
테스트 중이라 눈에 보이는 피드백도 없었음) 풀로 안 돌아가고 계속 날아가서, "맞고도
그냥 지나가는 것"처럼 보였음.

**수정**: `if (refHitEffect != null) refHitEffect.transform.position = ...;`로 바꿔서,
이펙트 풀 고갈 여부와 무관하게 아래 반납 로직은 항상 실행되도록 조건문 범위를 좁힘.

### 2-15. 죽은 코드 정리

원인이 확정된 뒤, 2-12에서 만들었다가 틀린 진단으로 판명된 스윕 판정 관련 코드를
전부 제거: `CircleCollider`의 `PrevCachedCenter`/`m_bCenterInitialized` 필드,
`SnapshotSpawnPosition()`, `ColliderManager`의 `IsSweepOverlap()`. 겸사겸사 이전부터
남아있던 주석 처리된 `ProfilerMarker` 필드/`using(...).Auto()` 잔재(섹션 2-11 이전부터
안 쓰던 것)와, 이번 디버깅용으로 임시로 심었던 `[REG-MISMATCH]`/`[START-NULL]` 로그도
함께 정리.

### 2-16. `RaycastMask` 추가 — `Aim`의 `Physics.Raycast` 대체

`Aim.cs`가 조준 커서가 적 위에 있는지 판정할 때 `Physics.Raycast`를 쓰고 있었는데,
몬스터에는 PhysX Collider가 아예 없으므로(이 시스템 자체가 PhysX를 걷어내는 게 목적)
이 판정은 애초에 항상 실패했을 구조였음. `ColliderManager`에 레이-원 판정을 새로
추가해서 대체:

- 원리는 섹션 2-11에서 쓴 "선분-점 최단거리" 투영 공식과 동일한 계열 — 레이는 한쪽으로만
  뻗으므로 매개변수 `t`를 `[0,1]` 대신 `[0, 최대사거리]`로 clamp.
- 방향 벡터를 미리 정규화해두면 `dot(vToCenter, dir)`이 곧바로 "레이 원점에서 최근접점까지의
  실제 거리(월드 단위)"가 되어, 별도 정규화 나눗셈 없이 그대로 clamp/비교에 쓸 수 있음.
- 레이어 파라미터는 `params int[]`(호출부 리터럴 나열 시 매 프레임 배열 할당 발생 우려)
  대신, 기존 `Aim.cs`에 이미 있던 `LayerMask` 필드를 그대로 재사용하는 방식으로
  최종 결정 — 값 타입이라 할당이 전혀 없음.

### 2-17. 프로파일링 중 반복된 오독 — Job System 수치를 여러 번 잘못 해석함

`BulletMoveManager`/`GuidedMoveManager`/`MissileMoveManager` 각각의 부하를 재보는
과정에서, 다음 오독들이 연달아 나왔고 그때그때 정정됨:

1. **워커 스레드 시간을 합산 시도**: "워커 11개가 각각 0.1ms씩이니 3ms 아니냐"는 계산 —
   병렬로 동시에 도는 스레드들의 시간을 순차 실행처럼 더한 것. 툴팁의 "accumulated
   time"이 이미 전체 스레드 합계라는 걸 짚어서 정정.
2. **"N instances"를 스레드당 개수로 오독**: "16 instances over 11 threads"를 "스레드마다
   16개"로 읽은 것 — 실제로는 그 Job 호출 전체에서 처리한 총 개수.
3. **활성 개수와 등록 슬롯 전체를 혼동**: "16 instances"가 지금 이 순간 활성 총알 수라고
   판단했으나, 실제로는 그 프레임이 마침 한산했을 때 캡처된 값이었고, `m_listSpeed.Length`로
   직접 확인해보니 등록 슬롯 전체는 1300개였음.
4. **누적 카운터와 실시간 카운터 혼동**: `Activate()`에만 `++count`가 있는 줄 알고 "누적
   발사 횟수"라고 판단했으나, `Deactivate()`에도 `--count`가 짝지어 있어 실제로는
   "지금 이 순간 순증감(활성 개수)"을 제대로 추적하는 카운터였음 — `count=1002`가
   등록 슬롯 1300개 기준으로 봤을 때 실제로 타당한 수치였음.
5. **엔진 내장 Job을 자기 게임 코드로 착각**: `CullObjectsWithoutUmbra`(Unity 내장 렌더링
   컬링), `JobMoverDemo:MoveJob`(프로젝트 내 `ECSDemo` 폴더의 Job vs MonoBehaviour 비교
   학습용 데모, `BulletMoveManager`와 무관)를 순서대로 자기 총알 Job으로 오인.

**결국 해결 방법은 툴팁 해석 논쟁을 그만두고, 코드에 직접 로그/카운터를 심어서
실측하는 것**이었음 — 이후 확인들은 전부 이 방식으로 정리됨.

### 2-18. `ColliderManager` 자체 프로파일링 — Stay/PreLoadCenter/CheckSameLayer/CheckCrossLayer 마커 재도입

보스 1마리만 배치한 씬에서 Main Thread 타임라인 기준 `ColliderManager.LateUpdate()
[Invoke] = 1.88ms` 확인(같은 프레임의 `ParticleSystem.Update = 1.84ms`와 비슷한 수준,
전체 프레임 CPU 32.24ms 중). 섹션 2-11 때 지웠던 자리에, 이번엔 실제로 쓸 목적으로
`ProfilerMarker` 4개를 다시 심음: `ColliderManager.OnStay`(Stay 발동 구간만), 그리고
`PreLoadCenter`/`CheckSameLayer`/`CheckCrossLayer`(레이어 단위로만 불려서 마커 오버헤드
걱정이 없는 구간들). `CheckSameLayer`(같은 레이어끼리, 즉 총알-총알 대조 — 매트릭스
설정이 잘못돼 있으면 O(N²)로 튈 수 있는 구간)와 `CheckCrossLayer`(총알-보스, 순수
물량 문제) 중 어느 쪽이 실제로 큰지는 다음 세션에서 확인 예정.

곁가지로, "Job으로 이동시키는 것과 별개로 콜라이더에 BoxCollider(PhysX)를 썼다면
됐을까"라는 질문도 나옴 — 답은 "포지션 동기화 자체는 됐겠지만 원래 풀려던 문제(PhysX
시뮬레이션 비용)는 그대로 남았을 것"이었음. Job은 "이동 계산" 비용만 줄여주지 PhysX의
브로드페이즈/내로우페이즈/트리거 콜백 비용은 콜라이더 존재 여부에만 달려있어서 별개임.
게다가 **콜라이더만 있고 Rigidbody가 없으면 PhysX는 그걸 "정적(static)" 오브젝트로
취급**해서, 그런 걸 매 프레임 움직이면 정적 브로드페이즈 트리를 매번 다시 계산해야 해
오히려 Rigidbody 있는 동적 오브젝트를 움직이는 것보다 더 비쌈 — `MissileMoveManager.cs`
주석에 이미 "Rigidbody/Collider가 없어서 `Physics.SyncColliderTransform` 비용이
없다"고 적어뒀던 그 이유가 이번에 다시 한번 확인된 셈.

---

## 3. 잘 됐던 접근

- **프로파일러 우선, 감으로 안 고침**: 셀 크기, GC Alloc, 몬스터 30마리 기준 1.88ms 등
  전부 실측 기반으로 판단. 감으로 셀 크기를 만졌다가 역효과였던 경험을 반복하지 않으려
  계속 서로 확인하며 진행함.
- **기존에 이미 검증된 패턴 재사용**: 풀 만료 시스템을 만들 때 `ObjectSpawner`의 PriorityQueue
  패턴을 그대로 재사용 — 새 자료구조를 또 만들지 않고 이미 있는 걸 찾아서 씀.
- **작업 전 명시적 확인 절차**: "체크해달라"와 "고쳐달라"를 구분해서 요청 → 리뷰만 하고 코드는
  안 건드리다가, 명시적으로 수정 요청이 있을 때만 실제 수정. 세션 중반 이후로는 이 경계가
  잘 지켜짐.
- **불확실할 땐 추측 대신 계측**: 수정 후 오히려 20ms로 나빠졌을 때, "내 수정이 잘못됐다"고
  바로 결론 내리지 않고 `ProfilerMarker`로 구간을 쪼개서 확인부터 함 — 결과적으로 그 수정은
  문제가 아니었고, 훨씬 작은 원인(Center 재계산)이 진짜 병목이었다는 걸 정확히 찾아냄. 그리드나
  Burst 같은 큰 구조 변경으로 바로 안 가고, 캐싱 하나로 끝난 사례.
- **틀린 가설도 자신 있게 계속 검증했다**: 2-12의 터널링 가설은 스윕 판정까지 완성한 뒤
  틀린 것으로 밝혀졌지만, 그 과정에서 Y축/오프셋 회전/피격 흔들림 가설을 실측으로 하나씩
  배제해나간 순서 자체는 정상적인 디버깅 절차였음. 가설이 틀렸다고 그 절차가 낭비는 아니었고,
  배제해나간 덕분에 남은 후보(등록 타이밍, 풀 고갈)로 좁혀질 수 있었음.
- **프로파일러 수치를 코드로 직접 검증**: Job System의 "N instances" 같은 애매한 툴팁
  문구를 두고 여러 번 잘못 해석했는데, 결국 해석 논쟁을 그만두고 코드에 카운터/로그를
  직접 심어서(`++count`/`--count`, `m_listSpeed.Length`) 눈으로 확인하는 쪽으로 정리됨 —
  섹션 2-17.

---

## 4. Claude 쪽에서 아쉬웠던 점

1. **모호한 표현을 수정 허락으로 오해**: 세션 초반, "코드부터 보자" 정도의 모호한 표현을
   실제 수정 허락으로 받아들여 `Bullet.cs`를 바로 편집한 적이 있었음. 이후 "실행 지시가
   명확할 때만 코드를 수정한다"는 원칙을 세웠고, 그 뒤로는 지켜졌음.
2. **"Job 서브클래스는 빼자"는 방향을 "Job 자체를 빼자"로 오해**: 그 결과 평범한
   `List<Bullet>` 순회 매니저를 먼저 구현했다가, Job 방식이 맞다는 정정을 받고 다시
   Job/Burst 기반으로 재작업했음. 의도 확인을 한 번 더 거쳤으면 줄일 수 있었던 왕복이었음.
3. **TransformAccessArray를 스스로 먼저 제안하지 못함**: `ApplyMove()` 방식의 불필요한
   오버헤드를 먼저 지적받은 뒤에야 더 나은 방식으로 전환했음. Rigidbody/Collider가 이미
   프리팹에서 빠진 상태라는 걸 그 이전 턴에 직접 확인해두고도, 그게 TransformAccessArray를
   안전하게 다시 쓸 수 있게 한다는 결론까지 스스로 잇지는 못했음.
4. **Quaternion-in-Burst를 확신 없이 일단 보류**: Missiles/GuidedBullet의 회전 계산을
   Job 안에서 직접 하는 게 Burst에서 안전한지 컴파일로 검증할 방법이 없어 처음엔 미뤘음.
   다시 검토해달라는 요청을 받은 뒤에야 `Unity.Mathematics.quaternion`(Burst 전용 타입)이라는
   더 확실한 해법을 찾았는데, 처음부터 이 타입을 검토했으면 더 빨리 갈 수 있었음.
5. **NativeList 쓰기 경합/해제 순서 문제를 설계 단계에서 못 잡음**: Job Schedule/Complete를
   Update/LateUpdate로 나누는 설계를 제안할 때, 서로 다른 스크립트의 실행 순서는 Unity가
   보장하지 않는다는 제약을 미리 감안하지 못했고, `OnDestroy`의 Dispose 순서 문제도 마찬가지로
   설계 단계에서 못 잡았음. 둘 다 에디터에서 실제 에러가 난 뒤에 진단하고 고치는 흐름이었음 —
   두 문제 모두 처음 설계할 때 좀 더 방어적으로 짜뒀으면 미리 막을 수 있었던 부분.
6. **터널링 가설에 너무 오래 머묾**: 총알 관통 버그(섹션 2-12)에서, "다수 동시 발사 시
   증상이 심해진다"는 관찰 하나로 터널링 가설을 세우고 스윕 판정까지 완성했는데, 실제로는
   전혀 다른 원인(등록 타이밍 버그)이 같은 관찰 결과를 만들어내고 있었음. "화면이 매끄럽게
   지나간다"는 사용자의 반박을 받고 나서야 재검토를 시작했는데, 애초에 "터널링이면 프레임이
   튀는 순간 눈에 띄게 끊겨야 정상"이라는 반증 포인트를 스스로 먼저 떠올렸어야 했음.
7. **Debug Inspector에서 "값이 정상으로 보인다"를 성급하게 결론으로 씀**: 등록 타이밍 버그를
   찾는 과정에서, 처음 대조한 총알 한 개가 정상 상태였다는 이유로 "등록 문제는 아니다"라고
   판단한 순간이 있었음. 실제로는 표본이 하나뿐이라 결론 내리기엔 일렀고, 사용자가 계속
   여러 개체를 대조해본 뒤에야 등록이 어긋난 개체들이 실제로 발견됨.

## 5. 코드/방향 설정 과정에서 있었던 시행착오

1. **`ColliderManager` 직접 재작성 시 여러 버그가 함께 들어감**: 컴파일 에러 1건
   (`RemoveAtSwapBack`), 핵심 로직 버그 3건(조건 반전, 값 타입 미반영, 오타), 구조적 문제
   2건(이중 등록, GC Alloc 스캔) — 섹션 2-5 참고. 리뷰 요청을 먼저 한 덕분에 커밋 전에
   전부 걸러졌음.
2. **그리드 셀 크기를 키웠다가 역효과가 난 시도**: 20으로 키운 시도가 실측상 더 느렸음
   (섹션 2-3). 실측으로 바로 확인하고 방향을 튼 사례라, "시도 → 측정 → 다음 방향"이라는
   정상적인 개발 사이클로 볼 수 있음.
3. **Job 관련 방향이 세션 중 한 번 바뀜**(Job을 빼는 쪽 → 다시 Job을 쓰는 쪽): 설계 의도를
   다듬는 과정에서 자연스럽게 있을 수 있는 정정이었지만, Claude 쪽 오해와 겹치며 재작업이
   좀 늘었음. "Schedule은 Update, Complete는 LateUpdate에서 기다린다"까지 처음부터 구체적으로
   정해뒀다면 한 번에 갈 수 있었던 부분.

---

## 6. 성능 변화 요약 (실측 기반, 미측정 구간은 명시)

| 단계 | 확인된 내용 |
|---|---|
| 시작 | `Bullet.FixedUpdate()`(Rigidbody 기반)가 프로파일러 병목으로 확인됨 |
| Job/Burst 1차(Rigidbody 기반) | 기존 Mono 방식 대비 명확한 이득 없음 → 채택 안 함 |
| Rigidbody→Transform 직접 이동 | 트리거용 Rigidbody 제거, PhysX 트리거 자체를 CircleCollider로 대체 |
| 그리드(셀 2) | GC.Alloc 304 instances/frame 확인, 셀별 List 최초 생성 시 할당이 원인으로 진단 |
| 그리드(셀 20) | 더 느려짐(프로파일러로 확인, GC.Alloc도 여전히 존재) |
| 그리드 폐기 → 레이어 매트릭스 순수 리스트 | 총알↑ 몬스터↓ 구조에 더 적합하다고 판단(그리드 재도입 논의 시에도 이 결론 유지) |
| ColliderManager 버그 수정 후, 몬스터 30마리 기준 | `ColliderManager.LateUpdate() = 1.88ms` / 전체 프레임 ~19ms 중 — O(총알×몬스터) 구조 확인 |
| 총알 이동 Job(TransformAccessArray) 전환 후 | 정성적으로 체감 개선 확인, 정확한 ms 재측정은 아직 안 함 |
| 풀 만료 PriorityQueue 전환 | 매 프레임 전체 풀 오브젝트 순회(O(N))를 만료 이벤트당 O(log n)으로 대체 — 아직 프로파일러로 재확인 전 |
| **전체 종합 (Job/Burst + 풀 만료 전환 이후)** | **`PlayerLoop`(`BehaviourUpdate` 포함) 기준 최초 Mono 방식 7~8ms → 2~3ms로 약 2배 감소** — 프로파일러로 직접 확인 |
| 몬스터 실제 배치 후 재측정, 몬스터 25마리 | `ColliderManager` 단독 14ms → Dictionary 접근 최소화 수정 후 20ms(다른 조건에서 측정된 것으로 추정, 회귀 아님) — `ProfilerMarker`로 확인한 결과 `CheckCrossLayer`가 13.55ms로 거의 전부 |
| `Center` 캐싱 적용 후 | `RefreshAllCenters`(정상 비용) = 1.58ms, `CheckCrossLayer`는 타임라인에서 거의 안 보일 만큼 감소 — 그리드/Burst 없이 캐싱만으로 해결 |
| 보스 1마리 배치 후 재측정(2026-08-20) | Main Thread 기준 `ColliderManager.LateUpdate() = 1.88ms`(전체 프레임 CPU 32.24ms 중) — 같은 프레임 `ParticleSystem.Update = 1.84ms`와 비슷한 비중. `PreLoadCenter`/`CheckSameLayer`/`CheckCrossLayer` 세부 마커는 재도입만 하고 결과는 아직 미확인 |

**아직 실측으로 재확인 안 된 것**:
- Job(TransformAccessArray) 전환 후 `BehaviourUpdate`/`PoolUpdate` 각각의 개별 ms 기여도
- `Center` 캐싱 적용 후 전체 프레임(`PlayerLoop`) 기준 최종 ms (`ColliderManager` 자체는 확인됨)
- 이번 측정은 에디터 Play Mode 프로파일러 기준(`EditorLoop` 오버헤드 포함)이라, 실제 빌드
  기준 수치는 별도로 확인 필요
- 보스 1마리 기준 재측정된 `ColliderManager.LateUpdate() = 1.88ms`가 `PreLoadCenter` /
  `CheckSameLayer` / `CheckCrossLayer` 중 어디서 나오는지 (섹션 2-18, 마커만 재도입한 상태)

---

## 7. 배경 이론 — ECS / AoS·SoA / Burst / Job System이 왜 빠른가

이번 리팩터링에서 실제로 쓴 네 가지 개념(ECS적 사고방식, AoS/SoA, Burst, Job System)이
근본적으로 왜 빠른지 정리한다. 이번 프로젝트가 이 중 무엇을 얼마나 채택했는지는 7-9에서
따로 정리.

### 7-1. 출발점: CPU 캐시와 메모리 지역성

CPU는 메모리에서 변수 하나만 딱 가져오지 않는다. 캐시라인(보통 64바이트) 단위로 통째로
가져와 L1/L2/L3 캐시에 올려둔다. L1 캐시 접근은 1ns 안팎, 메인 메모리(RAM) 접근은
100ns 안팎 — 대략 100배 차이. 그래서 실제 체감 성능은 "연산이 얼마나 복잡한가"보다
"필요한 데이터가 캐시에 이미 올라와 있는가(캐시 히트)"에 훨씬 크게 좌우되는 경우가 많다.

게임 로직은 대개 "같은 연산을 수천 개 오브젝트에 반복"하는 패턴(총알 5000개 이동, 몬스터
BT 틱 등)이라, 이 지역성을 얼마나 지키느냐가 실측 프레임 타임을 크게 가른다.

### 7-2. AoS vs SoA

- **AoS (Array of Structs)**: 지금까지 익숙한 방식. `Bullet[] bullets`처럼 오브젝트 하나에
  위치/속도/회전/HP 등 모든 필드가 붙어있고, 그 오브젝트들을 배열/리스트로 나열한다.
  일반적인 MonoBehaviour 배열이 전형적인 AoS.
- **SoA (Structure of Arrays)**: 필드별로 따로 배열을 둔다. `float3[] positions,
  float[] speeds, quaternion[] rotations` 식으로, "총알들의 위치 전부"가 한 배열에
  연속으로 붙어있다.
- **왜 SoA가 빠른가**: 이동 계산처럼 위치+속도만 읽고 쓰는 연산을 5000개에 돌린다고 하면,
  AoS는 캐시라인 하나(64바이트)를 채울 때 그 순간 필요 없는 다른 필드(HP, 데미지, 이벤트
  델리게이트 등)까지 같이 끌려온다 — 캐시 대역폭 낭비. SoA는 지금 필요한 필드만 완전히
  붙어있는 배열이라, 캐시라인이 낭비 없이 실제로 쓸 값들로 꽉 찬다.
- **이번 프로젝트에서 쓴 실제 예**: `BulletMoveManager`의 `NativeList<float> m_listSpeed`,
  `NativeList<bool> m_listActive`처럼, 총알 하나하나의 필드를 붙여서 저장하는 대신 필드별로
  따로 배열을 들고 있는 구조 자체가 SoA다.
- **트레이드오프**: "오브젝트 하나의 모든 필드"를 한 번에 다뤄야 하는 코드(개별 총알의
  상세 상태를 종합적으로 다루는 로직 등)는 SoA에서 오히려 여러 배열을 흩어서 접근해야 해서
  불편해진다. 그래서 "수천 개에 같은 연산 반복" 구간에만 선택적으로 SoA를 쓰는 게 실전적.

### 7-3. 데이터 지향 설계 (Data-Oriented Design, DOD)

- OOP는 "이 세상에 뭐가 있나"(총알, 몬스터, 플레이어...)를 기준으로 코드를 짠다 →
  자연스럽게 AoS가 된다.
- DOD는 "데이터가 어떻게 흐르고 변환되나"를 기준으로 코드를 짠다 → 자연스럽게 SoA + 일괄
  처리가 된다.
- ECS는 이 DOD 사고방식을 게임 아키텍처 레벨로 강제하는 패턴이라고 볼 수 있다.

### 7-4. ECS (Entity Component System)

- **Entity**: 그냥 정수 ID. 로직도 데이터도 없다.
- **Component**: 순수 데이터 구조체(Position, Velocity 등). 메서드/로직이 없다.
- **System**: Component 묶음을 일괄로 훑으며 로직을 적용하는 함수/클래스.
- Unity의 실제 ECS(Entities 패키지)는 같은 Component 조합(Archetype)을 가진 Entity들을
  16KB "Chunk"에 몰아넣고, Chunk 내부는 Component별로 SoA로 저장한다. Job/Burst가 Chunk를
  통째로 훑을 때 캐시 효율이 극대화되는 구조.
- 이번 프로젝트는 Entities 패키지(진짜 ECS)를 쓴 게 아니라, MonoBehaviour 기반
  `Bullet`/`Missiles`/`GuidedBullet` 구조는 그대로 두고 "이동 계산"이라는 가장 뜨거운
  경로만 손으로 SoA + Job + Burst로 옮긴 것 — 흔히 "하이브리드"/"DOTS-lite" 방식이라고
  부른다. 전체를 ECS로 갈아엎지 않고, 실측으로 확인된 병목 부분만 발췌 적용했다.

### 7-5. Burst 컴파일러

평소 C#은 Mono(IL을 JIT 컴파일) 또는 IL2CPP(C++로 변환 후 네이티브 컴파일)를 거쳐 실행된다.
둘 다 "일반적인 C# 전체"(가상 호출, GC, 예외, 박싱 등)를 지원해야 하다 보니 특정 루프
하나에 극한의 최적화를 걸기가 어렵다.

Burst는 `[BurstCompile]`이 붙은 코드 — HPC#(High Performance C#)이라 불리는 C#의 제한된
부분집합(클래스 참조/GC 힙 할당/가상 호출 금지, `NativeArray` 등 블리터블 값 타입만 사용
가능)을 LLVM 백엔드로 직접 네이티브 기계어로 컴파일한다. 빨라지는 이유:

1. **관리 오버헤드 자체가 없음** — GC, 가상 호출, 박싱이 애초에 불가능한 코드만 허용하니
   그런 비용 자체가 안 생긴다.
2. **포인터 앨리어싱 걱정 없이 최적화** — `NativeArray`의 `[ReadOnly]`/안전성 핸들 덕분에
   컴파일러가 "이 배열과 저 배열은 절대 겹치지 않는다"를 확신할 수 있어서, 일반 C/C++
   컴파일러가 앨리어싱 가능성 때문에 못 하는 재정렬/캐싱 최적화까지 공격적으로 적용한다.
3. **SIMD 자동 벡터화** — float 하나씩 계산하는 루프를 CPU의 SSE/AVX/NEON 명령어로 묶어서
   한 번에 4개, 8개씩 처리한다. 이게 실제로 먹히려면 데이터가 SoA로 붙어있어야 한다
   (AoS면 벡터화가 사실상 막힌다) — 그래서 Burst와 SoA는 세트로 붙어다닌다.
4. **플랫폼별 타겟 최적화** — 빌드 시점에 실제 타겟 CPU 명령어셋에 맞춰 컴파일한다.

### 7-6. Job System

- 스레드를 매번 새로 만들지 않고, CPU 코어 수에 맞춘 고정 워커 스레드 풀을 재사용한다
  (스레드 생성/파괴 자체가 비싼 OS 콜이라, 이것만 피해도 이득).
- 일감을 batch 단위로 쪼개서(`job.Schedule(count, batchSize)`) 워커들에게 나눠주고, 한
  워커가 일찍 끝나면 다른 워커의 남은 batch를 가져가는 워크 스틸링으로 코어 간 부하를
  분산한다.
- `JobHandle`로 "이 Job은 저 Job이 끝나야 시작한다"는 의존성만 표현하고, 메인 스레드는
  결과가 실제로 필요해질 때까지 기다리지 않아도 된다 — 이번 세션에서 쓴 "Update에서
  Schedule, LateUpdate에서 Complete" 패턴이 바로 이 특성을 이용한 것이다. 그 사이 다른
  스크립트들의 Update가 메인 스레드에서 도는 동안, Job은 워커 스레드에서 동시에 돈다.
- `AtomicSafetyHandle`: 같은 `NativeContainer`를 Job이 아직 쓰는 중인데 다른 코드가
  건드리면 즉시 예외로 잡아준다 — 이번 세션에서 겪은 NativeList 관련 에러 두 건이 바로
  이 안전장치가 실제로 문제를 잡아준 사례다(섹션 2-9).

### 7-7. TransformAccessArray

`Transform`은 원래 스레드 세이프하지 않다(Unity 엔진의 네이티브 씬 그래프를 감싸는 얇은
관리 객체라서, 여러 스레드가 동시에 건드리면 위험하다). `TransformAccessArray`는 Transform
포인터들을 모아 Job이 안전하게 병렬로 읽고 쓸 수 있게 해주는 전용 브릿지 구조다 — 이번
세션에서 `BulletMoveManager` 등이 Job 안에서 바로 `_transform.position = ...`을 쓸 수
있었던 이유가 이것.

### 7-8. 넷을 합치면 왜 곱연산으로 빨라지는가

SoA(자료구조) → Burst(그 자료구조를 도는 루프를 네이티브+SIMD로 컴파일) → Job System(그
네이티브 코드를 여러 코어에 병렬로 분배) → 관리 오버헤드 원천 제거. 이 넷은 서로 전제
조건이자 배수 관계다:

- SoA가 아니면 Burst의 SIMD 벡터화가 사실상 안 먹힌다.
- Burst가 아니면 Job System은 그냥 "느린 C# 코드를 여러 스레드에 나눠 돌리는 것"에 그친다.
- Job System이 없으면 Burst로 빨라진 루프를 코어 1개로만 돌리는 셈이 된다.

그래서 이 넷이 세트로 갖춰졌을 때, 같은 로직의 순정 C# 대비 수십 배 차이가 나는 사례가
흔히 보고된다. 이번 세션에서 관찰한 약 2배(7~8ms → 2~3ms)는 이동 계산 부분에만 이 스택을
적용한 결과이고, 전체 프레임에서 이 최적화 대상이 차지하는 비중에 따라 실제 배율은
달라진다.

### 7-9. 이 프로젝트가 실제로 채택한 범위

- **채택**: Job System + Burst + 손으로 짠 SoA(NativeList 여러 개) + TransformAccessArray
  — 총알 이동이라는 가장 뜨거운 경로에만 적용.
- **미채택**: Unity Entities 패키지의 진짜 ECS(Entity/Archetype/Chunk, `SystemBase` 등).
  `Bullet`/`Missiles`/`GuidedBullet`은 여전히 MonoBehaviour/GameObject 기반이고, 몬스터
  BT/Blackboard 등 나머지 게임 로직도 전통적인 OOP 구조 그대로다.
- 이런 부분 채택 방식을 흔히 "DOTS-lite"/하이브리드라고 부른다. 전체를 ECS로 갈아엎는
  비용 없이, 프로파일러로 실측 확인된 병목 한 곳에만 이 스택을 적용한 것.

### 7-10. PhysX Collider 비용의 실체 — 정적(static) vs 동적(dynamic) 브로드페이즈

이 프로젝트가 총알에서 PhysX Collider를 완전히 걷어낸 이유를 다시 정리해둔다(섹션 2-18에서
질문이 나와 다시 짚음).

- **Rigidbody가 있는 Collider = 동적 오브젝트**: PhysX가 "매 프레임 움직일 것"으로 가정하고,
  움직임에 최적화된 동적 브로드페이즈 구조에서 관리한다.
- **Rigidbody가 없는 Collider = 정적 오브젝트**: PhysX가 "안 움직일 것"으로 가정하고, 정적
  브로드페이즈 트리에 넣는다. 이 트리는 안 움직이는 지형/구조물 기준으로 최적화돼 있어서,
  여기 들어간 오브젝트를 스크립트/Job으로 매 프레임 옮기면 그때마다 트리를 다시 계산해야
  해서 **오히려 동적 오브젝트를 옮기는 것보다 더 비싸다.**
- 즉 "총알에 Collider만 달고 Rigidbody는 빼자"는 선택지는 이 셋 중 실제로는 제일 비싼
  선택지였을 것 — 지금처럼 Collider 자체를 아예 안 쓰는 것만이 PhysX 시뮬레이션 비용을
  0으로 만드는 방법이었다.
- Job/Burst로 이동 계산을 병렬화하는 것과, Collider를 걷어내 PhysX 시뮬레이션 자체를
  없애는 것은 **서로 다른 병목을 해결하는 별개의 최적화**다. 이동 계산이 아무리 빨라도
  Collider가 존재하는 한 브로드페이즈/내로우페이즈/트리거 콜백 비용은 그대로 남는다.

---

## 8. 다음에 확인/검토할 것

- [x] `ColliderManager.CheckPair`의 `Center` 프로퍼티(쿼터니언 곱셈) 캐싱 적용 —
      섹션 2-11에서 적용 완료, `CheckCrossLayer` 13.55ms → 거의 0 수준으로 감소 확인
- [ ] Job 전환 + 풀 만료 전환 + Center 캐싱 전환 이후 프로파일러로 `BehaviourUpdate`/
      `PoolUpdate`/`PlayerLoop` 전체 기준 최종 ms 재측정
- [ ] 몬스터 수가 더 늘어나는 시나리오(50~100마리)에서 레이어 매트릭스 순수 리스트 방식이
      여전히 그리드보다 나은지 재검증
- [ ] `JobBullet`/`JobMissile`/`JobGuidedBullet`, `MissileMoveManager`/`GuidedMoveManager`의
      옛 Job 서브클래스 의존 여부 최종 정리(삭제 또는 완전 방치 여부 확정)
- [x] 총알 관통(피격 판정 누락) 버그 — 실제 원인 두 가지(등록 삭제 타이밍, 이펙트 풀 고갈
      조기 return) 확정 및 수정 완료. 섹션 2-12~2-14
- [x] `Aim`의 `Physics.Raycast`를 `ColliderManager.RaycastMask`로 교체 — 섹션 2-16
- [ ] 보스 1마리 기준 재측정된 `ColliderManager.LateUpdate() = 1.88ms`를 `PreLoadCenter`/
      `CheckSameLayer`/`CheckCrossLayer` 마커로 실제 분해해서 어느 구간이 큰지 확인
      (마커는 재도입 완료, 섹션 2-18)
- [ ] `CheckSameLayer`가 유의미하게 크게 나오면, 레이어 충돌 매트릭스에 총알-총알처럼
      굳이 안 부딪혀야 할 조합이 잘못 켜져 있는 건 아닌지 확인
- [ ] `CheckCrossLayer`(총알-몬스터)가 크게 나오고 몬스터 수/총알 수가 더 늘어날 예정이면,
      그때 가서 섹션 7의 Job/Burst 방식으로 판정 자체(겹침 여부 계산)를 병렬화할지 검토 —
      단, Enter/Stay/Exit 이벤트 발동 자체는 managed 콜백이라 Job 안에서 못 하므로 "판정
      계산은 Job, 이벤트 발동은 메인 스레드"로 나누는 구조가 필요함
