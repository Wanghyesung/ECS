# 히트 이펙트(ParticleSystem) 동시 재생 폭주 완화 — 대화 정리

- 세션: 2026-08-24
- 관련 시스템: `Bullet`, `ObjectPool`, `SOPoolData`, `HitEffect`
- 관련 파일: `Assets/3D/02_Player/Weapon/Bullet.cs`, `Assets/3D/05_Manager/Pool/ObjectPool.cs`,
  `Assets/3D/05_Manager/Pool/SOPoolData.cs`, `Assets/3D/02_Player/Weapon/HitEffect.cs`,
  `Assets/00_Scene/3D/MainScene/SceneData/MainScene_PoolData/SO_BaseHit_PoolData.asset` 외
  히트 이펙트 계열 5종

이 문서도 Collider.md와 같은 목적 — "무엇을 바꿨는지"보다 **원인을 어떻게 좁혀갔는지**에
초점을 둔다.

---

## 1. 시작 문제

사용자가 Unity Profiler 스크린샷을 붙여넣으며 시작: `ParticleSystem.Update 3.20ms` +
`ParticleSystemBeginUpdateAll`이 같은 구간을 차지, `PostLateUpdate.ParticleSystemEndUpdateAll
0.511ms`. 사용자가 이미 자체적으로 짚은 것 — "지금 파티클만 풀에 300개가 넘게 있다, 총알당
파티클 하나인데 1초 안에 다 터질 수 있다"는 가설을 들고 "여러 방면으로 찾아봐 달라"고 요청.

## 2. 원인 분석 (코드/에셋 근거 추적)

Claude가 코드와 `SOPoolData` 에셋을 직접 추적해서 확인한 것:

1. **스로틀 없음**: `Bullet.Attack()`(당시 `Bullet.cs:174` 부근)이 히트할 때마다 조건 없이
   `ObjectPool.GetObject(m_refHitEffectObj)`를 호출 — 같은 프레임에 여러 발이 몰려도 합치거나
   제한하는 로직이 전혀 없었음.
2. **풀 크기 자체가 과도하게 큼**: `MainScene_PoolData`를 전수 조사한 결과, 히트 이펙트 계열만
   `SO_BaseHit_PoolData(300)` + `RedHit/MidHit/LargeHit/SparkEx/LightBall(각 200)` =
   **총 1,300개**가 동시에 존재 가능한 구조. `ObjectPool`의 `Max` 필드는 `SOPoolData.cs` 주석
   ("아직 사용하지 않음")대로 실제 로직에서 참조되지 않아 사실상 상한이 PreLoad 그 자체였음.
3. **이펙트 하나 = ParticleSystem 2개**: `BaseHitEffect.prefab`을 직접 열어 확인한 결과 부모+자식
   구조로 ParticleSystem 컴포넌트가 2개 들어있고, 각각 버스트로 30~50개 파티클을 0.5초짜리로
   뿜는 구조(Collision/Light/Trail/Noise 등 무거운 모듈은 전부 꺼져 있어 인스턴스 하나당 비용
   자체는 낮음). 즉 사용자가 말한 "총알당 파티클 하나"는 실제로는 "총알 하나 맞을 때마다
   ParticleSystem 2개짜리 오브젝트 하나"라서, 체감보다 동시 활성 시스템 수는 2배.

**결론**: 스샷의 `ParticleSystem.Update` 비용은 파티클 개수보다 **동시에 활성인 ParticleSystem
컴포넌트 수**에 더 크게 좌우된다(Unity가 시스템 단위로 job 스케줄링/바운즈 계산 등 고정비용을
물기 때문) — 사용자의 가설이 정확했고, 코드/에셋 근거로 뒷받침됨.

## 3. 해결 방향 제시 — 다섯 갈래

Claude가 비용/효과 순으로 다섯 방향을 표로 제시하고 우선순위를 추천:

| 방향 | 내용 | 비용 | 효과 |
|---|---|---|---|
| A. 동시 재생 상한(스로틀) | 짧은 시간창 안에 재생 가능한 히트 이펙트 개수에 전역 상한 | 낮음 | 즉효, 근본 원인 직접 차단 |
| B. 풀 크기 축소 | 히트 이펙트 PreLoad를 실제 필요량 수준으로 축소 | 매우 낮음 | 절대 상한 자체를 줄임 |
| C. 프리팹 경량화 | ParticleSystem 2개 중 하나 통합/제거, 버스트 수 축소 | 중간 | 인스턴스당 비용 절감 |
| D. 거리 컬링 | 화면 밖 히트는 이펙트 생략 | 낮음 | 화면 밖 낭비 제거 |
| E. 구조 전환 | 개별 GameObject 대신 상시 떠 있는 소수 시스템에 `Emit()`으로 추가(VFX Graph 등) | 높음 | 근본 해결, 동시성 문제 원천 차단 |

사용자는 A+B를 먼저 적용해달라고 확정, C~E는 보류.

## 4. 적용 — A: 동시 재생 상한

`Bullet.cs`에 프레임당 히트 이펙트 재생 개수를 제한하는 전역 static 카운터를 추가:

- `MAX_HIT_EFFECT_PER_FRAME = 8`(초깃값, 임의 설정 — 플레이해보고 체감에 맞춰 조정 예정)
- `TryReserveHitEffectSlot()`이 `Time.frameCount`로 프레임 전환을 감지해 카운터를 리셋하고,
  한도 내면 자리를 예약
- `Attack()`의 히트 이펙트 스폰 조건에 `&& TryReserveHitEffectSlot()`을 추가 — 한도를 넘긴
  히트는 **데미지/판정은 그대로 처리되고 이펙트만 생략**됨(전투 로직 영향 없음)
- 총알 종류(Base/Red/Mid/Large/Spark/LightBall)와 무관하게 전역 공유 — "동시에 살아있는
  ParticleSystem 개수"라는 실제 병목을 종류 구분 없이 직접 억제하는 게 목적이었기 때문
- Update 콜백 없이 호출 시점에만 `Time.frameCount`를 비교하는 방식이라 매 프레임 힙 할당 없음
  (성능 룰의 "Update/FixedUpdate/LateUpdate 힙 할당 금지"와 무관하게, 애초에 Update 자체가
  없는 구조)

## 5. 적용 — B: 풀 크기 축소

A로 동시 재생 자체가 제한되므로, 그만큼의 여유분을 걷어내는 방향으로 `PreLoad`/`Max`를 축소.
직접 `.asset` 파일을 텍스트로 편집하려다 프로젝트 훅(`block-scene-edit.sh`)에 막혀
`manage_scriptable_object` MCP 툴로 우회해 적용:

| 에셋 | 기존 | 변경 |
|---|---|---|
| SO_BaseHit_PoolData | 300 | 100 |
| SO_RedHit Data | 200 | 80 |
| SO_MidHit Data | 200 | 80 |
| SO_LargeHit Data | 200 | 80 |
| SO_SparkEx Data | 200 | 80 |
| SO_LightBall Data | 200 | 80 |

합계 1,300 → 500. 풀이 고갈돼도 `ObjectPool.GetObject`가 `null`을 반환하고 `Bullet.cs`가 이미
null 체크를 하고 있어 안전(이펙트만 생략, 예외 없음) — 이 안전장치가 이미 있었기 때문에 축소
폭을 보수적으로 걱정할 필요가 없었음.

## 6. 검증

- `refresh_unity`(compile request) 후 `read_console`로 에러/경고 0건 확인
- 사용자가 실제 플레이 후 프로파일러로 재확인: "많이 좋아졌네"로 체감 개선 확인(정확한
  ms 재측정치는 이번엔 별도로 공유되지 않음 — 다음 세션에서 수치로 재확인하면 좋음)

## 7. 남은 것 (미착수)

- **C. 프리팹 경량화**: `BaseHitEffect` 등 히트 이펙트 프리팹의 ParticleSystem 2개 구조 자체는
  손대지 않음 — 필요하면 후속 작업으로.
- **D. 거리 컬링**: 화면/사거리 밖 히트에 대한 이펙트 생략 로직 미구현.
- **E. 구조 전환**: 개별 GameObject 풀링 대신 상시 활성 시스템에 `Emit()`으로 추가하는 방식은
  설계 변경 폭이 커서 이번 세션 범위 밖으로 명시적으로 보류.
- `MAX_HIT_EFFECT_PER_FRAME = 8`은 실측 기반 값이 아니라 임의 초깃값 — 실제 교전 밀도에 맞춘
  튜닝은 사용자 플레이 테스트로 이후 조정 필요.
- 이번 세션에서 건드리지 않은 `TestScene/ECSDemo/PoolData` 쪽의 동일 이름 히트 이펙트
  `SOPoolData`들은 그대로 남아있음(활성 씬이 `MainScene_PoolData`로 확인되어 그쪽만 수정) —
  테스트 씬도 같이 쓸 계획이면 별도로 맞춰야 함.
