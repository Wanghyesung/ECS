# 충돌/이동/풀 시스템 리팩터링 회고

- 기간: 2026-08-01 ~ 2026-08-02 세션
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

**아직 실측으로 재확인 안 된 것**:
- Job(TransformAccessArray) 전환 후 `BehaviourUpdate`/`PoolUpdate` 각각의 개별 ms 기여도
- `CheckPair`의 `Center` 캐싱 최적화(논의만 하고 미적용) 적용 시 실제 이득
- 이번 측정은 에디터 Play Mode 프로파일러 기준(`EditorLoop` 오버헤드 포함)이라, 실제 빌드
  기준 수치는 별도로 확인 필요

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

---

## 8. 다음에 확인/검토할 것

- [ ] Job 전환 + 풀 만료 전환 이후 프로파일러로 `BehaviourUpdate`/`PoolUpdate`/
      `ColliderManager.LateUpdate` 재측정
- [ ] `ColliderManager.CheckPair`의 `Center` 프로퍼티(쿼터니언 곱셈) 캐싱 적용 여부 결정
      (섹션 2-6에서 논의만 하고 보류)
- [ ] 몬스터 수가 더 늘어나는 시나리오(50~100마리)에서 레이어 매트릭스 순수 리스트 방식이
      여전히 그리드보다 나은지 재검증
- [ ] `JobBullet`/`JobMissile`/`JobGuidedBullet`, `MissileMoveManager`/`GuidedMoveManager`의
      옛 Job 서브클래스 의존 여부 최종 정리(삭제 또는 완전 방치 여부 확정)
