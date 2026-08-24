# ColliderManager Box 브로드페이즈 최적화 — 대화 정리

- 세션: 2026-08-20 (같은 날 이어진 단일 세션)
- 관련 시스템: `ColliderManager`, `BoxColliderGrid`(신규), `ObbCollider`, `BaseCollider`
- 관련 파일: `Assets/3D/05_Manager/ColliderManager.cs`, `Assets/3D/05_Manager/BoxColliderGrid.cs`,
  `Assets/3D/02_Player/Weapon/ObbCollider.cs`, `Assets/3D/02_Player/Weapon/BaseCollider.cs`

이 문서는 "무엇을 구현했는지"보다 **설계를 두고 오갔던 제안·반려·타협 과정**에 초점을 둔다.
최종 코드의 상세 근거는 각 클래스 상단 주석을 참고.

---

## 1. 시작 문제

총알 540개 × Box 콜라이더(운석/장애물) 100개 씬에서 프레임이 9~12ms까지 나온다는 리포트로 시작.
`ColliderManager.CheckOverlaps()`가 레이어 쌍마다 완전 브루트포스(N×M) 대조를 하고 있었고,
`(Obstacle, PlayerAttack)` 쌍 하나만으로 100×540=54,000쌍이 매 프레임 완전 대조되고 있었다.

코드/씬(BattleScene의 `m_arrLayerCollisionMatrix`, `MapBoundaryConstraint`)을 직접 조사해서
확인한 것:
- 실제로 켜져 있는 충돌 쌍은 `(Player,Obstacle)`, `(Obstacle,PlayerAttack)`, `(Monster,PlayerAttack)`,
  `(Player,MonsterAttack)`, `(Obstacle,MonsterAttack)` 다섯 개뿐
- `Bullet.Attack()`이 Enter마다 `GetComponent<IDamageable>()`를 호출하는데, Box 콜라이더(운석)엔
  `IDamageable` 구현체가 하나도 없어 항상 실패하는 GetComponent + 히트이펙트 풀링이 따라붙음 —
  체감 스파이크와 정성적으로 부합하는 2차 가설로 남겨두고, 이번 작업 범위에서는 제외(후속
  확인 항목으로만 기록)

## 2. 설계 논의 타임라인 — 제안과 반려

브로드페이즈 도입 여부와 자료구조 선택을 두고 총 **4차례** 설계를 다시 잡았다. 매번 Claude가
초안을 제시하면 사용자가 구체적인 약점을 짚어 반려했고, 그 지적이 타당해서 반영하는 방식으로
좁혀졌다.

### 2-1. 1차 설계(Claude) — AABB 다중 셀 등록 + 제네릭 위임(Func) 구조

Claude는 `StaticBoxColliderGrid<T>` 제네릭 클래스로 설계했다. 아이템을 자기 AABB가 겹치는
모든 셀에 등록해 경계 누락을 원천 차단하고, `Func<T,Vector3>`/`Func<T,float>`로 실제 콜라이더
타입과 분리했다. Obstacle 레이어는 `ObbCollider`의 "정적 전용" 계약을 근거로 그리드를 한 번만
빌드하도록 했다.

**사용자 반려**: 정적 위치를 전제로 한 설계는 이후 오브젝트가 움직이게 될 경우 문제가 될
수 있으니 정적이라고 가정하지 말라고 지적했다. 또한 지금 다뤄야 할 도형 조합이 몇 개 안 되는
상황에서 제네릭+델리게이트 구조는 과하니, 그냥 if로 분기하는 편이 낫다고 판단했다.

### 2-2. 2차 설계(Claude) — 매 프레임 전체 재빌드, 비제네릭 구조로 단순화

Claude는 정적 전제를 없애기 위해 그리드를 매 프레임 통째로 재빌드하는 방식으로 바꾸고,
제네릭과 델리게이트를 제거해 `BaseCollider`/`ObbCollider`를 직접 다루는 구체 클래스로
단순화했다.

**사용자 반려**: 이 설계에도 분명 잘못된 점이 있을 거라고 지적했다. Claude가 다시 살펴본
결과, 그리드를 매 프레임 재빌드하도록 고쳐도 원본 데이터인 `ObbCollider.CachedCenter`/축
자체가 `RefreshCenter()`가 빈 구현이라 `Start()` 이후 갱신되지 않는 결함을 스스로 찾아냈다.
사용자는 여기에 더해 "장애물"이라는 기획 용어를 버리고 Box 콜라이더는 무조건 동적으로
움직인다고 전제하고 개발하자는 방향을 제시했고, 목표도 "휴리스틱과 충돌 예측을 활용해
레이어 대조 비용을 줄이는 것"으로 명확히 다시 잡았다.

→ 이때 `ObbCollider.RefreshCenter()`를 실제로 매 프레임 위치/축을 갱신하도록 고치는 작업이
같이 확정됐고, 이후 설계에도 계속 유지됐다.

### 2-3. 3차 설계 — 셀 소속 기반(콜라이더당 셀 1개) + Dictionary 그리드

이번엔 사용자가 먼저 구체적인 대안을 제시했다: 콜라이더마다 소속 셀을 하나씩만 기록하고
(ID 기준 역참조 배열), 조회할 때는 반대편 오브젝트가 자기 셀 주변 이웃 그리드(9칸)만
확인하는 방식. Claude는 이 안을 받아 다음 세 가지를 조정해 반영했다:
- 셀 크기는 평균이 아니라 **최대** BoundingRadius 기준으로 산정 — 평균으로 잡으면 유난히 큰
  콜라이더 하나 때문에 경계 근처에서 실제로 겹치는 쌍을 놓칠 위험이 있다고 판단
- 이웃 셀 범위는 9칸(2D)이 아니라 27칸(3×3×3, 3D)으로 확장 — 이 게임이 실제로 Y축까지 쓰는
  3D 공간이라는 점을 구형 맵 경계 시스템으로 직접 확인하고 근거로 삼음
- 저장 구조는 고정 크기 `[,,]` 배열이 아니라 `Dictionary<long, List<BaseCollider>>`로 제안 —
  Box 콜라이더의 월드 범위를 미리 못박은 시스템이 없어, 고정 배열로는 범위 밖 콜라이더가
  조용히 누락될 위험이 있다고 판단

이 설계는 사용자 승인을 받아 실제로 구현/테스트까지 진행했다.

### 2-4. 4차 설계 — Dictionary도 배열 기반으로 재작성

실측(Unity Profiler) 결과 확실히 개선은 됐지만 사용자가 보기에 기대만큼 드라마틱하지
않았고, 사용자가 직접 디버깅하던 중 그리드의 `Unlink` 경로가 전혀 호출되지 않는다는 걸
발견해 Claude에게 원인을 물었다. Claude가 추적한 결과: Box 콜라이더 대부분이 Static으로
설정돼 있어 최초 등록 이후 재계산이 스킵되고 있었고, 사용자가 설정한 `m_fMaxBulletRange`
값(600)이 맵의 실제 반경(BattleScene `MapBoundaryConstraint` 실측 기준 300)보다 커서 거리
컬링이 사실상 한 번도 발동하지 않는 상태였다 — 버그가 아니라 "지울 대상이 없어 안 지워지는"
정상 동작이라고 설명했다.

사용자는 여기서 Dictionary 자체의 효용에 근본적인 의문을 제기했다: 셀 접근이 어차피 연속된
배열이 아니라 서로 다른 포인터를 따라가는 것이라면, 차라리 3차원 배열을 만들어 List에 ID를
넣고 빼는 게 낫지 않겠냐는 것이었다. Claude는 이 문제 제기를 두 단계에 걸쳐 반영했다.

1. **1차 수정(Claude)**: Dictionary를 제거하고 고정 배열(`int[] m_arrCell`) + 인트루시브
   연결 리스트(콜라이더끼리 ID로 서로를 참조)로 교체했다. 해시 계산은 없앴지만, 여전히
   ID를 따라 다음 원소를 찾아가는 구조였다.
2. **사용자 재반박**: 이것도 마찬가지 아니냐고 다시 지적했다 — 셀 안의 콜라이더들이
   공간적으로 가깝다고 해서 ID 값까지 가까울 보장은 없으니, 이 방식도 결국 배열이라는
   껍데기만 배열일 뿐 접근 패턴은 포인터 추적과 다르지 않다는 것. Claude는 이 지적이
   타당하다고 판단해 받아들였다.
3. **최종 구조(Claude)**: 그리드 범위가 "정적 분할"로 Build 시점에 확정되는 구조라는 점을
   활용해, 셀마다 `List<BaseCollider>`를 Build 시점에 전부 미리 만들어두고, 런타임에는 그
   List에 직접 추가/스왑백 제거만 하도록 바꿨다. 신규 List 할당도, ID를 통한 참조 추적도
   없이 각 셀 내부에서는 연속된 메모리를 순회한다. 다만 사용자가 제안한 `[,,]` 다차원
   배열은 그대로 쓰지 않았다 — C#/.NET에서 다차원 배열 인덱싱이 1차원 배열 + 수동 좌표
   플래튼(`x + y*W + z*W*H`)보다 느리다는 특성을 Claude가 짚었고, "배열로 직접 접근한다"는
   목적에는 1차원 쪽이 더 맞다고 판단해 이 부분만 조정했다.

이 구조가 최종 채택됐다.

### 2-5. 함께 확정된 것 — 플레이어 사거리 기반 컬링

사용자가 제안한 내용을 그대로 채택했다: 맵 전체 범위를 기준으로 삼지 말고, 플레이어 위치와
총알이 도달 가능한 최대 사거리(인스펙터 설정값) 안에 있는 오브젝트만 그리드에 반영하자는
것. Claude는 `m_refPlayer`/`m_fMaxBulletRange` 필드를 추가하고, 사거리 밖 Box 콜라이더는
그리드에서 제외(`RemoveCollider`), 사거리 안이면서 Static이고 이미 그리드에 있으면 재계산을
스킵하도록 구현했다.

## 3. 부수 정리 작업

- **`s_arrNarrowPhase` 델리게이트 테이블 제거**: 사용자가 델리게이트 테이블 대신 함수를
  직접 호출하는 방식으로 바꾸자고 요청해서, Claude가 `CheckPair`에서 두 콜라이더의 `Shape`을
  직접 비교해 맞는 함수를 바로 호출하도록 단순화했다(`IsOverlapping` 함수 하나로 통합).
- **주석 다이어트**: 사용자가 `ColliderManager`의 주석이 특히 불필요하게 길다고 지적해서,
  Claude가 클래스 헤더 주석을 38줄에서 13줄로, 그 외 여러 메서드 주석을 압축했다. 이 과정에서
  이미 사실과 어긋나 있던 주석(Box 판정이 `CheckCrossLayer`에 통합돼 있다는 옛 설명 — 지금은
  `CheckCrossLayerGrid`로 분리됨)도 Claude가 발견해서 함께 바로잡았다.
- **레거시 유니티 BoxCollider 제거**: 사용자가 "Obstacle 자식의 Box 콜라이더를 지워달라"고
  요청했고, Claude는 처음 이를 자체 판정용 `ObbCollider`로 오해해 어떤 의미인지 확인을
  요청했다. 사용자가 유니티 기본 Collider를 가리킨 것이라고 정정하자, Claude가 씬을 MCP로
  직접 조사해 자체 판정용 `ObbCollider` 129개 중 127개에 유니티 기본 `BoxCollider`를 들고
  있는 자식 오브젝트(`Collider`/`Collider1`, PhysX를 걷어내기 이전의 잔재로 추정)가 딸려있는
  것을 확인하고 전부 삭제했다.

## 4. MCP로 검증한 것

세션 중반부터 Unity MCP 연결이 활성화되어, 그 이후 변경마다 직접 확인했다:
- `refresh_unity`(force + compile) → 컴파일 에러 없음
- EditMode 테스트(`ColliderManagerTests`) 반복 실행 → 매번 통과
- BattleScene을 Play 모드로 직접 실행해 `ColliderManager`/`BoxColliderGrid`/`ObbCollider`
  관련 에러·예외 0건 확인(발생한 에러는 기존에 이미 알려진 `DontDestroyOnLoad` 경고로 이번
  작업과 무관)
- 부수적으로 Play 모드 도중 시스템 메모리가 16GB 중 423MB까지 떨어진 것을 발견해 경고함 —
  이번 세션의 짧은 테스트만으로 생긴 현상은 아니고, 그 이전에 진행되던 장시간 스트레스
  테스트(8600+ 프레임) 누적으로 추정(원인 미확정)

## 5. 성능 결과 (실측 기반)

| 단계 | 확인된 내용 |
|---|---|
| 시작 | 총알 540 × Box 100에서 9~12ms(Profiler 실측) |
| Dictionary 기반 셀 소속 그리드 적용 후 | 총 4~5ms로 감소 — 다만 `Unlink`가 전혀 안 불려서 원인 진단이 필요했음 |
| 원인 확인 | `m_fMaxBulletRange=600`이 맵 실제 반경 300보다 커서 컬링이 사실상 무동작. Static 스킵 로직은 정상 동작 중이었음(버그 아님) |
| Dictionary → 고정 배열+연결 리스트 → 셀별 List 사전할당으로 재작성 후 | `ColliderManager.LateUpdate() = 3.73ms`(Profiler 실측, 유의미한 개선으로 확인) |

**아직 실측/확인되지 않은 것**:
- `m_fMaxBulletRange`를 맵 반경(300)보다 작은 실제 총알 사거리 값으로 낮췄을 때의 추가 이득
- `Bullet.Attack()`의 Enter마다 도는 `GetComponent<IDamageable>()` + 히트이펙트 풀링 비용이
  현재 남은 프레임 비용에서 차지하는 비중(2차 가설, 미착수)
- Play 모드 중 확인된 `Monster.Dead() → ObjectPool.PushObject`의 `ArgumentNullException`
  (Dictionary key null) — 이번 Collider 작업과 무관한 기존 버그로 보이나 미조사

## 6. 잘 됐던 접근 / 반복된 패턴

- 사용자는 설계를 반려할 때마다 구체적인 대안을 함께 제시했다(셀 소속 방식, 배열 기반 접근,
  플레이어 사거리 컬링). Claude는 그 대안을 그대로 받아들이지 않고 정확성이 걸린 지점(셀
  크기 산정 기준, 이웃 셀 범위, 다차원 배열 여부)만 짚어 조정한 뒤 반영하는 방식으로
  진행했다 — 설계를 매번 처음부터 다시 짜지 않고도 매번 더 나은 안으로 수렴했다.
- 사용자가 제시한 "정적이라고 가정하지 말자"는 설계 원칙이, Claude가 실제 결함
  (`ObbCollider.RefreshCenter` 미갱신)을 스스로 찾아내는 계기가 됐다 — 설계 원칙에 대한
  논의가 실질적인 버그 발견으로 이어진 사례.
- 성능에 대한 판단은 항상 Profiler 실측으로 뒷받침했다 — 사용자의 체감 평가("드라마틱하지
  않다" 등)가 나올 때마다 Claude가 실제 수치(9~12ms → 4~5ms → 3.73ms)와 원인(사거리 설정값
  오류 등)을 짚어 확인했다.
- MCP 연결 이후로는 Claude가 코드 작성 후 항상 컴파일·테스트·짧은 Play 모드 검증까지 직접
  수행했다 — 그 이전 구간은 정적 리뷰로만 검증했다는 한계를 스스로 밝히고, 사용자에게 라이브
  재검증을 권장했다.

---

# 두 번째 세션(2026-08-24) — Circle-Circle 통합 브로드페이즈

- 관련 시스템: `ColliderManager`, `BoxColliderGrid`, `BaseCollider`, `CircleCollider`, `ObbCollider`
- 이번 세션엔 Unity MCP 연결이 없어서 전부 정적 리뷰 + 코드 근거 확인으로만 진행했고, 컴파일/
  EditMode 테스트/PlayMode 실행/프로파일러 재측정은 매 단계 사용자가 직접 Unity 에디터에서
  수행하고 그 결과(스크린샷)를 다시 붙여넣는 방식으로 검증했다.

## 7. 시작 문제

프로파일러 실측(총알 3500 / 장애물 140 / 몬스터 20)에서 `ColliderManager.LateUpdate() = 8.82ms`,
그중 `CheckCrossLayer = 4.03ms`가 Circle-Circle 쌍(주로 Monster×PlayerAttack 70,000쌍)을 여전히
브루트포스로 처리하는 데서 나왔다. 1차 세션에서 만든 `BoxColliderGrid` + Burst Job은 Box(Obstacle)
쪽에만 적용돼 있었다.

## 8. 설계 논의 타임라인

### 8-1. 1차 설계(Claude) — 레이어별 그리드 소유권(Owner/Query 비대칭)

기존 Box 전용 그리드+Job 구조를 일반화: 레이어 쌍마다 "누가 그리드를 소유(Owner)하는가"를
결정하는 규칙(Box가 있으면 Box가 무조건 Owner, 둘 다 Circle이면 그 프레임 개체 수가 적은
쪽이 Owner)을 추가하고, Owner SoA/Query(Other) SoA를 분리해 `CollectLayerWork`가 레이어 쌍을
분류, `GridOverlapJob`이 Circle-Circle/Circle-Box를 함께 처리하도록 구현까지 완료했다. 이
과정에서 사용자가 제안한 "Update에서 미리 채우면 되지 않냐"는 대안은 코드 근거(Bullet/Missile/
GuidedMoveManager가 Update에서 이동 Job을 Schedule만 하고 실제 위치 쓰기는 자기 LateUpdate의
Complete에서 한다는 사실)로 반박하고, LateUpdate 유지로 정리했다 — 이 사실 확인이 이후
세션에서도 계속 근거로 쓰인다.

**사용자 반려**: 구현 결과를 보고 "통합된 느낌이 없다 - 불필요한 로직/데이터가 많다"고 재검토를
요청했다. 구체적으로 `CollectLayerWork`의 소유권 판단, `ScheduleGridJob`의 자기레이어 중복
가드(`GuardSelfLayerDuplicate`, 지금 매트릭스에선 절대 발동 안 함), `CheckSameLayer`/
`CheckCrossLayer`/`RunPendingLayerWork`로 이어지는 별도 브루트포스 폴백 경로를 짚었다. 원칙으로
"절차적으로는 콜라이더의 크기·위치·타입만 있으면 되고, 나머지(브로드페이즈 후보 탐색 + 레이어
매트릭스 필터 + 도형별 판정)는 Job의 `Execute(int index)` 안에서 분기 처리로 끝나야 한다"를
제시했다.

### 8-2. 2차 설계(Claude) — 단일 SoA + 단일 그리드 + 단일 Job

Owner/Query 구분을 완전히 없애고, 활성 콜라이더 전부(레이어/도형 무관)를 하나의 SoA와 하나의
`BoxColliderGrid`에 담는 구조로 재설계했다. 그리드에 들어간 콜라이더 자신이 곧 조회 주체가
되고(자기 배열 안에서 이웃 27칸을 찾아 `j > i`면 검사), `GridOverlapJob.Execute`가 레이어
매트릭스 체크(`IsLayerCollider`의 `NativeArray<int>` raw-parameter 버전 신설)와 도형 조합
분기(Circle-Circle/Circle-Box/Box-Circle/Box-Box)까지 전부 처리하도록 만들었다. 재설계안은
Before/After 파이프라인 다이어그램으로 시각화해 제시했고, 그리드를 하나로 합칠지 도형/밀도별로
나눌지도 다시 물어봐서 **단일 그리드**로 확정했다(셀 크기는 그리드 내 최대 `BoundingRadius`로
정해지는데, 이 프로젝트에서는 Obstacle이 이미 그 최댓값을 차지하므로 합쳐도 셀 크기 자체는
안 바뀐다는 게 확인 근거).

이 설계로 삭제된 것: `IsGridOwnerLayerA`, `CollectLayerWork`, `CheckSameLayer`, `CheckCrossLayer`,
`RunPendingLayerWork`, `m_listPendingLayerPair`, `m_arrGridOtherLayer`, `GuardSelfLayerDuplicate`,
그리고 브루트포스 경로로만 호출되던 `IsOverlapping`/`IsCircleBoxOverlapPair`/
`IsBoxCircleOverlapPair`/`IsBoxBoxOverlap`(전부 도달 불가능해진 죽은 코드라 함께 제거).
유지된 것: `BaseCollider.BoundingRadius`(1차 세션에서 이미 가상 프로퍼티로 승격해둔 것),
`IsCircleCircleOverlap`/`IsCircleBoxOverlap`(raw-parameter, Job이 그대로 호출), `BoxColliderGrid`의
CSR 구조 자체(로직 변경 없음, 이름만 "Box 전용"에서 "그리드 소유 콜라이더 전체"로 주석 갱신).

## 9. 스케줄 재구조화 — Update에서 Schedule, LateUpdate에서 Complete

통합 직후 실측한 프로파일러에서 `LateUpdate() = 11.23ms`(PreLoadCenter 3.29 + GridBuild 2.72 +
GridJobComplete 2.54 + GridJobDrain 2.37)를 확인. 사용자가 "병렬로 처리해도 뭘 하고 돌아오는게
아니라 실행하고 기다리는 거여서 그런가보다"라고 원인을 짚었다 — Schedule 직후 바로 Complete를
부르니 워커 스레드가 도는 동안 메인스레드가 할 일이 없어 순수 대기가 된다는 것.

사용자 제안: 오브젝트가 다 움직이는 이상 이번 프레임 안에서 충돌을 처리하면 콜라이더 콜백
안의 상태 전환 로직(예: 피격 시 방어 상태로 전환)이 순서에 따라 판정에 영향을 줄 수 있으니,
애초에 판정을 다음 프레임으로 미루자 - Update에서 Job을 미리 Schedule하고 LateUpdate에서
받는 구조로 바꾸자는 것. Claude는 Bullet/Missile/GuidedMoveManager와 완전히 같은 패턴(Update=
Schedule, LateUpdate=Complete)임을 확인하고 그대로 적용: `PreLoadCenter`/`BuildGrid`/
`ScheduleGridJob`을 `Update()`로, `CompleteAndDrainGridJob`만 `LateUpdate()`에 남겼다. 이 시점의
캐시된 위치는 "저번 프레임 LateUpdate가 끝난 시점의 스냅샷"이라 판정이 의도적으로 한 프레임
늦어지지만, 대신 Job이 이번 프레임 나머지 Update + LateUpdate 전체 구간 동안 겹쳐 돌 수 있게 됐고,
한 프레임의 모든 Enter/Stay/Exit이 동일한 스냅샷 기준으로 계산되어 콜백 순서 의존성도 없어졌다.
`[DefaultExecutionOrder(1000)]`는 그대로 유지했는데, 이제는 "최신 위치를 읽기 위해서"가 아니라
"LateUpdate가 이 프레임에서 가장 늦게 돌수록 Job이 겹쳐 도는 시간이 길어지기 때문"으로 근거가
바뀌었다.

**결과(실측)**: `LateUpdate() 11.23ms` → `1.92ms`로 감소, 대신 `Update() 4.96ms`(PreLoadCenter
3.12 + GridBuild 1.71)로 비용이 옮겨감 — 총량은 줄고, Complete 대기(순수 블로킹)가 실제 겹치는
작업으로 바뀐 것이 핵심.

## 10. Gather 단계의 불필요한 O(N) 복사 제거

사용자가 `BuildGrid`의 첫 단계(`m_listAllActive.Clear()` 후 32개 레이어를 순회하며 콜라이더
3000~4000개를 전부 리스트에 다시 담는 코드)를 보고 "매번 다 다시 넣을 필요가 있냐"고 지적했다.
Claude가 확인한 결과: `m_arrCollider[layer]`는 이미 Activate/DeleteCollider로 "활성 콜라이더만"
유지되는 배열이고, `List<T>.Count`는 O(1)이라 전체 개수는 32개 레이어를 순회하는 것만으로
구해진다 — 개별 콜라이더를 복사할 필요가 없었다. `m_listAllActive`는 `BoxColliderGrid.Build()`가
필요로 하는 `List<BaseCollider>`를 만들기 위해 그리드가 아직 한 번도 안 지어졌을 때(생애주기 중
최대 한 번)만 채우는 일회성 스크래치로 용도를 좁히고, 매 프레임 도는 SoA/그리드 채우기 루프는
32개 레이어 리스트를 직접 순회하도록 고쳤다.

이어서 사용자가 "그냥 처음부터 그리드를 가장 큰 얘 기준으로 바꾸자"고 제안했는데, 확인해보니
이미 `m_grid.IsBuilt` 가드로 `Build()`(그리드 경계/셀 크기 계산)는 콜라이더가 처음 활성화되는
프레임에 딱 한 번만 실행되고 있었다 — 사용자가 걱정한 지점이 이미 해결돼 있었던 것으로 확인,
추가 작업 없이 오해만 정리했다. 매 프레임 도는 건 `Build()`가 아니라 셀 *멤버십* 재계산
(`BeginRebuild`→`AddCollider`×N→`EndRebuild`)이고, 이건 콜라이더가 실제로 움직이는 이상
피할 수 없는 비용이라고 설명했다.

## 11. 다음 병목 — PreLoadCenter (미착수, 다음 세션 과제로 이월)

10번 최적화 이후 재측정에서 `Update() 4.96ms` 중 `PreLoadCenter`가 3.12ms로 새 병목으로
확인됐다. 원인: `RefreshCenter()`가 활성 콜라이더(~3660개)마다 `transform.position`/
`transform.rotation`을 읽는데, Unity의 `Transform` 프로퍼티 접근은 매번 관리형↔네이티브 경계를
넘는 호출이라 단가가 붙고, 이게 메인스레드 순차 가상 호출로 3660번 반복되는 구조.

Claude는 `BulletMoveManager`/`MissileMoveManager`/`GuidedMoveManager`가 이미 쓰고 있는
`TransformAccessArray` + Burst Job 패턴을 `PreLoadCenter`에도 그대로 적용하자고 제안(콜라이더
등록/해제 시 `TransformAccessArray`에도 같이 등록, Job이 병렬로 위치/축을 읽어 `NativeArray`에
담는 구조)했으나, **사용자가 이번 세션에서는 보류**하고 대신 이 문서 기록과 커밋 메시지 정리를
요청했다. `TransformAccessArray` 전환은 다음 세션 후속 과제로 남는다.

## 12. 성능 결과 요약 (이번 세션, 실측 기반)

| 단계 | LateUpdate/Update 비용 |
|---|---|
| 시작 (1차 세션 종료 시점, Box만 그리드) | `LateUpdate() 8.82ms` (`CheckCrossLayer` 4.03ms 포함) |
| Owner/Query 통합 직후 (Update/LateUpdate 분리 전) | `LateUpdate() 11.23ms` (PreLoadCenter 3.29 + GridBuild 2.72 + GridJobComplete 2.54 + GridJobDrain 2.37) |
| Update/LateUpdate 분리 후 | `LateUpdate() 1.92ms`, `Update() 4.96ms`(PreLoadCenter 3.12 + GridBuild 1.71) |

**아직 확인/착수되지 않은 것**:
- `PreLoadCenter`를 `TransformAccessArray` + Job으로 전환하는 작업(11번 참고, 다음 세션 과제)
- Bullet처럼 그리드에서 절대 조회되지 않는(항상 Query만 하는) 콜라이더를 애초에 `AddCollider`
  대상에서 제외해 `EndRebuild`의 스캐터 비용을 줄이는 방향 — 9번 논의 중 후보로만 언급됐고
  구현 여부는 결정되지 않음
- Unity MCP 연결 없이 진행된 세션이라 컴파일/EditMode 테스트/PlayMode 검증을 Claude가 직접
  수행하지 못했음 — 전부 사용자가 에디터에서 확인
