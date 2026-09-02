# Burst / Job System / SIMD 개념 정리

- 정리 시점: 2026-08-07
- 관련 코드: `Assets/02_Player/Weapon/BulletMoveManager.cs`
- 관련 문서: `Docs/Retrospective_CollisionMoveSystem.md` 7장 (ECS/AoS·SoA/Burst/Job 개요 — 이 문서는 그중 Burst 내부 동작과 벡터화 개념을 더 깊게 다룬 보충 자료)

이 문서는 "왜 Burst/Job/SIMD가 빠른가"를 컴파일러 구조 → 언어 제약 → 메모리 레이아웃 → 벡터화/대역폭 순으로 정리한 참고 자료다. 세션 중 개념이 헷갈렸던 지점(16바이트=캐시라인 오해, 벡터화/대역폭 짝 오해)도 정정 과정 그대로 남겨둔다.

---

## 1. 이 프로젝트가 실제로 쓴 것 — ECS와 DOTS는 다르다

Unity가 말하는 **DOTS(Data-Oriented Technology Stack)**는 별개 패키지 4개를 묶은 상위 개념이다. "ECS"는 그중 정확히 하나(Entities 패키지)만 가리키는 말이고, 나머지 셋은 ECS 없이도 단독으로 쓸 수 있다.

| 구성요소 | 역할 | 이 프로젝트가 씀? |
|---|---|---|
| **Burst** | `[BurstCompile]` 코드를 네이티브+SIMD로 컴파일 | 사용 |
| **Job System** | 워커 스레드 풀에 작업 분배 | 사용 |
| **Collections** | `NativeArray`/`NativeList` 등 blittable 컨테이너 | 사용 |
| **Entities (=ECS 패키지)** | Entity / IComponentData / SystemBase / EntityManager / Archetype·Chunk | **미사용** |

`BulletMoveManager`가 하는 일은 `GameObject`/`Transform`/`MonoBehaviour` 위에서 Burst·Job System·Collections만 가져다 쓴 것이다. Entity, EntityManager, SystemBase, Chunk 중 이 프로젝트에 존재하는 건 하나도 없으므로 "ECS를 썼다"는 표현은 부정확하고, "ECS적 사고방식(SoA + 일괄 처리)만 빌려왔다" 또는 "DOTS 스택 중 Job/Burst/Collections만 부분 적용했다"가 정확한 표현이다.

**아키텍처 원칙**: 의사결정 계층(FSM, BT, Blackboard, SO 기반 Action)은 그대로 OOP/MonoBehaviour로 유지하고, 프로파일러로 확인된 "동일 연산을 수천 개에 반복"하는 핫스팟(총알 이동 등)에만 선택적으로 Job+Burst+SoA를 적용한다. BT의 분기·재귀 순회나 SO 참조(관리 객체)는 HPC# 제약(2장 참고)과 근본적으로 안 맞기 때문에, "판단"은 OOP에 남기고 "대량 반복 계산"만 떼어내는 경계를 지킨다.

---

## 2. Burst 컴파일러가 빠른 이유 — LLVM 재사용 구조

일반 C#은 Mono(IL을 JIT) 또는 IL2CPP(C++ 경유 네이티브 컴파일)로 실행된다. 둘 다 "C# 전체"(가상 호출, GC, 예외, 박싱)를 지원해야 해서 특정 루프 하나에 극한 최적화를 걸기 어렵다.

컴파일러는 보통 3단 구조로 만들어진다.

```
프론트엔드 (언어별)  →  LLVM IR (중립 중간 표현)  →  최적화 패스  →  백엔드 (CPU별)
```

가운데 LLVM IR이 언어 쪽과 CPU 쪽을 분리해준다. Clang(C/C++), Rust, Swift, Zig가 각자 자기 언어를 IR로 바꾸면, 그 뒤의 최적화기와 x86/ARM 코드 생성기는 전부 공유된다.

**Burst가 한 일은 "IL을 LLVM IR로 번역"하는 프론트엔드 한 조각을 새로 만든 것뿐이다.** 그 뒤부터는 수십 년 축적된 LLVM 최적화기(벡터화 패스 포함)가 그대로 작동한다. Unity가 벡터화 알고리즘을 직접 짤 필요가 없었던 이유다.

### HPC# (High Performance C#)

별도 언어가 아니라 Burst가 컴파일할 수 있는 C#의 부분집합이다. 문법은 그냥 C#이지만 쓸 수 있는 게 제한된다.

**금지**
- `class` (모든 참조 타입 접근), GC 할당, 박싱
- `string`, `delegate`, 인터페이스 가상 디스패치
- `try`/`catch`
- 관리 배열 `int[]` — 대신 `NativeArray<int>`

**허용**
- `struct`, 모든 값 타입, `unsafe` 포인터
- `NativeArray` 등 네이티브 컨테이너
- `Unity.Mathematics` 타입
- 함수 포인터(`FunctionPointer<T>`) — 델리게이트 대용

이렇게 조여놓은 이유: 컴파일러가 메모리 레이아웃과 aliasing(포인터 겹침 여부)을 100% 확신해야 벡터화가 가능하다. GC 객체가 하나 끼면 그 포인터가 언제 이동할지 알 수 없고, 가상 호출이 하나 있으면 어떤 코드가 실행될지 몰라 인라인도 벡터화도 못 한다.

`BulletMoveManager.MoveJob`이 `NativeArray<float>`/`NativeArray<bool>`만 필드로 갖는 순수 struct인 것도 이 제약을 지키기 위해서다 — class나 `List<T>`가 하나라도 섞이면 애초에 `[BurstCompile]` 자체가 컴파일 에러가 된다.

---

## 3. Unity.Mathematics와 SIMD 레지스터

`Unity.Mathematics`는 의도적으로 HLSL(셰이더 언어)을 베껴서 만들었다. 소문자 타입명(`float4`, `float3`, `int4`, `bool4`), 스위즐링(`v.xyz`, `v.wzyx`), `math.dot()`/`math.normalize()` — 셰이더 코드와 C# 코드가 거의 똑같이 보이도록 설계됐다.

동작도 대응된다. GPU에서 `float4`가 벡터 레지스터에 들어가듯, CPU에서 Burst가 `float4`를 SSE/NEON의 128비트 레지스터 하나에 그대로 매핑한다. `Missiles`/`GuidedBullet`의 회전 계산에 `Unity.Mathematics.quaternion.LookRotationSafe`를 쓴 것도 이 타입이 Burst 전용으로 blittable하게 설계됐기 때문이다.

**단, GPU와 CPU의 병렬화 방식은 다르다.** 요즘 GPU는 사실 `float4` 단위(성분 방향)로 병렬 처리하지 않는다. SIMT 방식이라 스레드 하나가 원소 하나를 맡고, 하드웨어가 32개 스레드를 나란히 굴린다. 즉 GPU는 "성분 방향"이 아니라 "원소 방향"으로 병렬화한다 — 이게 5장의 AoS vs SoA 논의와 직결된다.

---

## 4. 메모리 레이아웃 기초

데이터가 메모리 주소 공간에 실제로 어떤 순서·간격·정렬로 놓이는가를 말한다. 소스 코드에 필드를 쓴 순서와 실제 바이트 배치가 반드시 같지는 않다.

구성 요소는 넷이다.
- **순서** — 필드가 어떤 순서로 놓이는지
- **크기** — 각 필드가 몇 바이트인지
- **정렬(alignment)** — `int`는 4의 배수 주소, `float4`는 16의 배수 주소에 놓여야 하는 식의 규칙
- **패딩** — 정렬을 맞추려고 컴파일러가 끼워 넣는 빈 바이트

```csharp
struct Bad  { byte a; int b; byte c; }   // 12바이트 (패딩 6바이트 낭비)
struct Good { int b; byte a; byte c; }   // 8바이트
```

필드 순서만 바꿔도 크기가 달라진다. C#에서는 `[StructLayout(LayoutKind.Sequential)]`로 선언 순서 유지를 강제하거나, `[StructLayout(LayoutKind.Explicit)]` + `[FieldOffset]`으로 바이트 위치를 직접 지정할 수 있다.

`class`는 힙 객체마다 헤더가 붙는다(64비트에서 오브젝트 헤더 + 타입 포인터 = 16바이트). `Vector3`가 `class`였다면 배열은 사실 포인터 배열이 되고, 12바이트 데이터를 위해 28바이트를 쓰며 여기저기 흩어진다. `struct`면 정확히 12바이트가 나란히 놓인다 — DOTS/HPC#이 `struct`만 고집하는 이유다.

---

## 5. AoS vs SoA — 벡터화 관점

- **AoS (Array of Structs)**: `Bullet[] bullets`처럼 오브젝트 하나에 위치/속도/회전 등 모든 필드가 붙어있고, 그 오브젝트들을 나열. 일반 MonoBehaviour 배열이 전형적인 AoS.
- **SoA (Structure of Arrays)**: 필드별로 따로 배열을 둠. `float3[] positions, float[] speeds` 식.

캐시 관점만 보면 AoS/SoA 둘 다 연속 메모리고 포인터 추적도 없어서 큰 차이가 없어 보인다. 진짜 갈리는 지점은 캐시가 아니라 **SIMD 레지스터에 싣는 방식**이다.

SIMD 연산은 레인(lane) 단위로 짝을 맞춰 동작한다. `add` 명령 하나가 레인0끼리, 레인1끼리, 레인2끼리, 레인3끼리 더한다. 그리고 로드 명령은 연속된 16바이트를 통째로 집어온다 — 골라 담을 수 없다.

입자 4개의 x좌표를 한꺼번에 갱신한다고 하면, **AoS**에서 필요한 x값들은 바이트 오프셋 0, 12, 24, 36에 흩어져 있다. 한 번의 로드로 못 모은다. 컴파일러는 둘 중 하나를 한다.
1. 3개 레지스터를 로드해서 셔플/전치 명령으로 재배치 — 추가 명령어 비용 발생
2. 포기하고 스칼라 코드로 컴파일 — 실제로 이 경우가 많음

`Vector3`가 12바이트라 16바이트 정렬 경계에 안 맞는 것도 겹친다. 원소마다 정렬이 어긋나서 로드가 캐시 라인을 걸치기도 한다.

**핵심: 성분 방향이 아니라 원소 방향으로 벡터화해야 한다.** `float3` 하나를 연산하면 4개 레인 중 3개만 쓰고 하나는 논다. 반면 SoA로 입자 4개의 x를 한 번에 처리하면 4레인을 꽉 채운다 — 3장에서 말한 GPU SIMT와 정확히 같은 방식이다.

```csharp
for (int i = 0; i < n; i++) xs[i] += vxs[i] * dt;   // Burst가 4개씩 묶어 자동 벡터화
for (int i = 0; i < n; i++) ys[i] += vys[i] * dt;   // 별개 패스
for (int i = 0; i < n; i++) zs[i] += vzs[i] * dt;
```

루프 3개가 비효율적으로 보이지만, 각 루프가 4~8배로 접히므로 전체적으로는 더 빠르다. Unity ECS의 Chunk가 내부적으로 컴포넌트 타입별 연속 배열을 잡는 것(Position 100개가 한 덩어리, Velocity 100개가 그다음 덩어리)도 같은 이유 — 엔티티 단위로 묶지 않고 구조 자체가 SoA다.

**SoA가 항상 답은 아니다.** 원소 하나의 전 성분을 동시에 쓰고 접근이 무작위라면(레이캐스트 히트 하나 처리, UI 요소 하나 갱신) AoS가 캐시 라인 하나로 끝나서 유리하다. 대량의 균일한 처리가 아니면 SoA로 뒤집을 이유가 없다.

---

## 6. 캐시라인(64B) vs SIMD 레지스터(16B) — 벡터화와 대역폭은 다른 문제

두 크기는 완전히 다른 두 가지 하드웨어 단위다.

| 크기 | 무엇의 단위인가 |
|---|---|
| 캐시라인 (보통 64바이트) | 메모리 → 캐시 전송 단위 |
| SIMD 레지스터 (16바이트, SSE/NEON 기준) | 캐시 → 레지스터 로드 및 연산 단위 |

AVX2면 레지스터가 32바이트, AVX-512면 64바이트로 커지지만 캐시라인은 여전히 64바이트다 — 서로 독립적인 값이다.

그래서 **AoS의 문제는 캐시 미스가 아니다.** 입자 4개의 x를 모으려면 바이트 0~40 범위를 훑는데, 이건 캐시라인 하나 안에 통째로 들어있다. 캐시는 불만이 없고 미스도 안 난다. 문제는 그다음 단계 — 캐시에서 레지스터로 옮길 때 연속 16바이트만 집어올 수 있는데, 원하는 x 4개가 12바이트 간격으로 어긋나 있다는 것. **순전히 레지스터 채우기(간격/stride) 문제다.**

그래도 캐시 이득이 아예 없는 건 아니다. x만 필요한 연산이라면:
- **AoS** — 캐시라인 64바이트를 가져오면 그 안에 x는 5~6개뿐이고 나머지는 안 쓸 y, z. 대역폭의 3분의 2를 버림.
- **SoA** — 64바이트가 전부 x. 낭비 없음.

즉 SoA의 이득은 **벡터화(주된 이유) + 대역폭 절약(부수적)** 두 갈래다. x, y, z를 어차피 다 쓰는 연산이라면 대역폭 쪽 이득은 사라지고 벡터화 이득만 남는다.

### 정정: 벡터화와 대역폭의 올바른 짝

| 문제 | 관련 하드웨어 | 핵심 질문 |
|---|---|---|
| 벡터화 | SIMD 레지스터 (16B) | 쓸 값들이 간격 없이 붙어 있나 |
| 대역폭 | 캐시라인 (64B) | 실어온 64바이트 중 몇 %를 실제로 쓰나 |

간격(stride)이 어긋나면 레지스터에 못 싣는다 → 벡터화 실패. 대역폭은 간격과 무관하게, 가져온 데이터 중 버리는 양의 문제다. 둘을 섞어서 "16바이트 = 캐시라인"이라고 생각하기 쉬운데, 16바이트는 SIMD 레지스터 크기이고 캐시라인은 별개로 64바이트다.

"벡터화 = 같은 속성끼리 연산"이라는 말도 더 정확히는 **"다른 원소들의 같은 속성"**이다. 세 조건이 다 필요하다.
1. 같은 연산일 것 (명령 하나로 처리하니까)
2. 서로 독립적인 원소일 것 (앞 결과를 뒤가 쓰면 못 묶음)
3. 그 값들이 메모리에 연속일 것

`x0` 하나에 대해 `x*y*z`를 계산하는 건 벡터화가 아니다. `x0~x3` 네 원소에 대해 같은 곱셈을 하는 게 벡터화다.

헷갈리기 쉬운 이유는 **SoA 하나로 두 문제가 동시에 풀리기 때문**이다. x를 모아놓으면 간격이 4바이트로 붙어서 벡터화가 되고, 동시에 캐시라인이 x로만 채워져서 대역폭도 안 버린다. 원인은 둘인데 처방이 같아서 하나처럼 보인다.

> 한 줄 요약: 간격이 어긋나면 레지스터에 못 싣고(벡터화 실패), 안 쓸 데이터가 섞여 있으면 대역폭을 버린다(캐시 낭비). 둘은 별개의 문제고, SoA가 우연히 둘 다 고쳐준다.

---

## 7. 이 프로젝트 실제 적용 — BulletMoveManager

### 7-1. `NativeList<float>`/`NativeList<bool>` 분리 이유

```csharp
private NativeList<float> m_listSpeed;   // 4바이트
private NativeList<bool> m_listActive;   // 1바이트
```

`struct { float Speed; bool Active; }` 하나로 합쳤다면, C#의 구조체 정렬 규칙상 가장 큰 필드(float, 4바이트) 기준으로 전체 크기가 정렬된다. `4 + 1 = 5`바이트인데 4의 배수로 올림되어 실제로는 **8바이트**를 차지한다(3바이트 패딩 낭비). 분리하면 `float`는 4바이트씩, `bool`은 1바이트씩 빈틈없이 붙는다 — 논리적으로 같은 정보인데 캐시라인에 싣는 데이터량이 5바이트 대 8바이트로 약 60% 더 넓게 쓴다.

추가로, `Activate`/`Deactivate`가 `m_listActive` 하나만 건드리고 `m_listSpeed`는 안 건드리는데, struct 하나였다면 bool 하나 바꾸려고 float까지 포함된 8바이트 전체를 캐시에 올리고 다시 써야 한다. 필드가 분리돼 있어서 "이 필드만 갱신"이 가능한 것도 실질적 이유다.

### 7-2. TransformAccessArray는 SIMD 극대화보다 오버헤드 최소화를 우선한 트레이드오프

`MoveJob`은 `IJobParallelForTransform`이고, `Execute`가 매 인덱스마다 `TransformAccess`를 통해 위치/회전을 읽고 쓴다. `TransformAccess`는 순수 연속 배열(`NativeArray<float3>` 같은)이 아니라 Unity의 네이티브 트랜스폼 계층 내부 데이터를 포인터로 접근하는 방식이라서, `ArrSpeed`/`ArrActive`처럼 명확하게 SIMD로 묶이는 것과 달리 총알들 사이에서 실제로 벡터 레인이 꽉 차게 벡터화됐는지는 단정하기 어렵다.

"총알 여러 개를 SIMD 레인에 확실히 채우고 싶다"면, 위치 계산 결과를 `NativeArray<float3>`에 먼저 다 계산해두고 나중에 일괄로 Transform에 복사하는 방식이 벡터화엔 더 유리하다. 그런데 이 방식은 정확히 `BulletMoveManager.cs` 상단 주석에 적힌 대로 "리스트 인덱싱 + 함수 호출(네이티브 콜/가상 호출) 오버헤드"가 커서 이미 버리고 `TransformAccessArray`로 바꾼 방식이다(`Docs/Retrospective_CollisionMoveSystem.md` 2-8절).

즉 지금 구조는 **SIMD 벡터화를 최우선으로 노린 설계가 아니라, "Job 스케줄링/결과 적용 오버헤드 최소화"를 SIMD 가능성보다 우선한 트레이드오프**다. 둘 다 챙기고 싶다면 `NativeArray<float3>` 계산 + 일괄 적용 방식으로 되돌아가야 하는데, 그 경우 버려진 오버헤드 문제가 다시 돌아온다 — 실측 없이 어느 쪽이 최종적으로 더 나은지 단정할 수 없고, 필요해지면 두 방식을 다시 프로파일러로 비교해야 한다.

### 7-3. 검증 방법

실제로 벡터화가 일어났는지 확인하려면 Unity 에디터의 **Jobs → Burst → Open Inspector**(Burst Inspector)로 `MoveJob`의 디스어셈블리를 열어, SIMD 명령어(`vmulps`, `vaddps` 등 SSE/AVX 계열)가 나오는지 직접 확인할 수 있다.

---

## 8. 요약 체크리스트

- [ ] "ECS를 쓴다" ≠ "Job/Burst/Collections를 쓴다" — Entity/SystemBase/Chunk가 없으면 ECS가 아니다
- [ ] Burst는 자체 최적화기를 만든 게 아니라 LLVM 프론트엔드 하나만 얹은 것
- [ ] HPC# 제약(관리 타입 금지)은 컴파일러가 aliasing을 확신하기 위한 전제조건
- [ ] AoS의 문제는 캐시 미스가 아니라 SIMD 레지스터에 실을 때 간격(stride)이 어긋나는 것
- [ ] 벡터화(레지스터 간격 문제)와 대역폭(캐시라인 낭비 문제)은 별개이며, SoA가 우연히 둘 다 해결한다
- [ ] 필드를 쪼개 별도 `NativeList`로 두는 건 패딩 방지 + 독립 갱신이 실질 이유이고, SIMD는 "가능성을 열어두는" 정도
- [ ] `TransformAccessArray` 채택은 SIMD보다 Job 오버헤드 최소화를 우선한 선택 — 필요시 Burst Inspector로 실제 벡터화 여부 재확인 가능
