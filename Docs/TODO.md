# TODO / 리팩토링 백로그

현재는 진행을 위해 임시로 넘어가기로 한 문제점들을 모아두는 문서. 우선순위 없이 발견 순서대로 기록.

---

## 1. DungeonManager가 로비부터 전체 스테이지 데이터를 들고 있음 (메모리)

- **현재 상태**: `DungeonManager`(`List<SOStage>` 보유)를 로비 씬에 두고, 로비에서 어떤 스테이지로 진행할지 고른 뒤 던전 씬으로 넘어가는 구조로 결정. 씬 전환 시 선택 정보를 유지해야 해서 로비부터 계속 살아있게 됨.
- **문제**: `SOStage.SpawnEntry.MonsterPrefab`/`BossPrefab`이 `PoolObject` 하드 레퍼런스라서, `DungeonManager`가 로드되는 로비 시점부터 **모든 스테이지의 몬스터 프리팹**(메시/텍스처/애니메이션 등)이 메모리에 올라옴. 실제로는 지금 진행 중인 스테이지 것만 있으면 되는데 전체가 상주하게 되는 낭비.
- **임시 결정**: 일단 이 구조(로비에 DungeonManager, 하드 레퍼런스)로 진행. 아래 리팩토링은 나중에.

### 리팩토링 후보안

**A안: 선택과 실행을 분리**
- 로비에는 "어떤 던전/스테이지를 고를지"만 아는 가벼운 선택 정보(문자열 id, 인덱스, config 에셋 참조 등)만 두고, 무거운 `List<SOStage>` 로드 및 던전 진행 로직(`DungeonManager` 본체)은 **던전 씬 진입 시점**으로 미룸.
- `GameSceneManager`가 씬마다 `SOSceneData`를 그 씬 진입 시점에만 로드하는 것과 동일한 패턴.
- 장점: 로비에서는 실제로 고른 것 하나만큼의 부담도 없음(선택 정보만 가벼움). 던전 씬 진입 후에도 여전히 원본 프리팹 하드 레퍼런스로 인한 로드는 남지만, 최소한 "필요한 시점"으로 미뤄짐.

**B안: PoolObject 하드 레퍼런스 → AssetReferenceGameObject(Addressables)로 전환**
- `SOStage.SpawnEntry.MonsterPrefab`/`BossPrefab`, `ObjectPool`의 해시 키, `ObjectSpawner.tSpawnData.refSpawnObject` 등을 전부 `AssetReferenceGameObject` 기반으로 변경.
- **주의**: `AssetReferenceGameObject`는 `Equals`/`GetHashCode`를 오버라이드하지 않아 기본적으로 참조(인스턴스) 동일성 비교. 같은 프리팹을 가리켜도 서로 다른 SO에 직렬화된 `AssetReferenceGameObject`는 다른 인스턴스이므로, **`AssetReferenceGameObject` 객체 자체를 Dictionary 키로 쓰면 안 됨** — 조회가 항상 실패(null)할 위험.
- 대신 `AssetReferenceGameObject.AssetGUID`(string)를 키로 사용해야 함.
  - string 키는 `PoolObject`(참조/정수 해시) 키보다 해싱·비교 비용이 근소하게 더 듦. 스폰/디스폰 시점에만 발생하는 빈도라 실측 전엔 병목 여부 단정 어려움.
  - 더 빠르게 하려면 풀 등록 시점에 `AssetGUID`를 한 번 `System.Guid.Parse`로 변환해 `Dictionary<Guid, ...>`로 키를 잡는 방안 고려 (구조적 비교라 string보다 빠름, `PoolObject` 키에 근접).
- 이 방식은 `ObjectPool`뿐 아니라 `PoolObject`를 키/참조로 쓰는 다른 지점(`Bullet.m_refHitEffectObj`, `SOSpawnAttackObject.m_refAttackObjectPrefab` 등)까지 건드리는 넓은 리팩터라, A안과 별개로(혹은 병행해서) 진행 여부를 판단할 것.
- **A안만으로는 "로비에서의" 메모리 문제는 해결되지만, 던전 씬 진입 후 여전히 원본 프리팹이 통째로 로드되는 "무거움" 자체는 남음** — 완전히 해결하려면 결국 B안이 필요.

---

## 2. Boss가 Monster.OnMonsterDied를 실제로 발행하는지 미확인

- `DungeonManager`의 스테이지 진행 로직(`MonsterDead` → `m_bBossSpawned` 체크)은 보스도 `Monster.OnMonsterDied` 정적 이벤트를 발행한다는 전제로 동작함.
- 아직 Boss 전용 클래스가 없음. Boss를 만들 때 `Monster`를 상속하거나, 최소한 사망 시 `Monster.OnMonsterDied`를 직접 호출하도록 구현해야 다음 스테이지 진행/던전 클리어가 정상 동작함.

## 3. ObjectPool 고갈 시 DungeonManager 진행이 멈출 수 있음

- `ObjectSpawner.SpawnObject`는 `ObjectPool.GetObject`가 null(풀 고갈)이면 조용히 스폰을 스킵함.
- 이 경우 `DungeonManager`가 기대하는 "예약된 몬스터 수"만큼 실제로는 스폰되지 않으므로, 그만큼의 `Monster.OnMonsterDied`도 영원히 안 옴 → `m_iRemainMonsterCount`가 0에 도달하지 못해 보스/다음 스테이지 진행이 멈출 수 있음.
- 지금은 `SOPoolData.PreLoad`를 충분히 크게 잡아 회피하는 것을 전제로 미해결 상태로 둠.

## 4. 씬/에셋 배치 (에디터 작업, 코드로 대체 불가)

- 씬에 `DungeonManager`/`ObjectSpawner` 오브젝트 배치 및 인스펙터 연결.
- `SO_Stage_XX` 에셋들 실제 생성 및 몬스터/보스 스폰 테이블 데이터 입력.
- 몬스터/보스 프리팹에 `PoolObject` 부착 + `SOPoolData` 프리로드 목록 등록.
