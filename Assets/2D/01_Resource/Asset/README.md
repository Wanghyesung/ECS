# 2D 슈팅게임 스프라이트 팩 (Top-down)

업로드해주신 3D Modular Spaceships 레퍼런스를 바탕으로 **탑다운 2D**용 PNG 에셋을 만들었습니다.
모든 파일은 투명 배경 RGBA PNG이며, 아틀라스는 **균일 그리드**라 Unity의 `Grid By Cell Size` /
파티클 `Texture Sheet Animation`에 바로 들어갑니다.

---

## 1. 폴더 구조

```
ships/        ships_atlas.png (+json)   256px 셀 · 8종
              boss_dreadnought.png      512px 단일
bullets/      bullets_atlas.png (+json) 64x128 셀 · 60종
missiles/     missiles_atlas.png(+json) 64x128 셀 · 6종
fx/           fx_hit / fx_damage / fx_hit_energy / fx_explosion
              fx_muzzle / fx_shockwave / fx_particles   (128px 셀)
trails/       trail_*.png (256x64) + trails_atlas.png
individual/   아틀라스에 들어간 낱장 PNG 전부
tools/        생성 파이썬 스크립트 (색/형태 수정 후 재생성용)
PREVIEW.png   전체 미리보기
```

## 2. 함선 (ships_atlas.png · 1024x512 · 셀 256x256)

| 프레임 | 이름 | 방향 | 용도 |
|---|---|---|---|
| 0~2 | player_ship / _red / _white | ↑ 위 | 플레이어 (3색 리버리) |
| 3 | enemy_drone | ↓ 아래 | 소형 잡몹 |
| 4 | enemy_interceptor | ↓ | 고속 돌격형 |
| 5 | enemy_gunship | ↓ | 중형 사격형 |
| 6 | enemy_bomber | ↓ | 장갑형 |
| 7 | enemy_elite | ↓ | 엘리트 |
| — | boss_dreadnought (512px) | ↓ | 보스 |

- 플레이어는 위, 적은 아래를 보도록 미리 회전되어 있습니다.
- Unity: Sprite Mode `Multiple` → Slice `Grid By Cell Size` `256 x 256` → Pivot `Center`.
- 권장 Pixels Per Unit: **256** (함선 1대 ≈ 1유닛)

### 애니메이션 관련
함선 자체는 정지 스프라이트이고, **엔진 불꽃이 이미 그려져 있습니다.**
움직임 연출은 코드 쪽에서 처리하는 걸 추천드립니다.

- 아이들: `localScale.y` 를 0.98~1.02로 살짝 펄스 + 엔진 위치에 파티클
- 좌우 이동: `Rotate(0,0,±12°)` 로 뱅크(기울기) 보간
- 피격: `SpriteRenderer.color` 를 흰색으로 0.06초 플래시

---

## 3. 총알 (bullets_atlas.png · 640x768 · 셀 64x128)

10색 × 6타입 = 60개. 색상 순서(열): `cyan, blue, green, yellow, orange, red, purple, pink, white, lime`

| 행 | 타입 | 설명 |
|---|---|---|
| 0 | `bullet_basic_*` | 기본 캡슐탄 |
| 1 | `bullet_small_*` | 소형 (연사용) |
| 2 | `bullet_long_*` | 롱 캡슐 (관통/레일) |
| 3 | `bullet_heavy_*` | 대형 (샷건/차지) |
| 4 | `plasma_*` | 플라즈마 구체 |
| 5 | `laser_*` | 레이저 빔 (세로 타일링 가능) |

- 전부 **위쪽이 진행 방향**입니다.
- 발광이 포함돼 있으므로 머티리얼은 `Sprites/Default` 로 충분하고,
  더 강한 네온을 원하면 Additive 셰이더나 URP Bloom(Emission)을 쓰세요.
- 레이저는 세로로 늘려도 자연스럽게 보이도록 상하 페이드를 넣었습니다.

## 4. 미사일 (missiles_atlas.png · 384x128 · 셀 64x128)

`missile_standard, missile_homing, missile_heavy, missile_plasma, missile_toxic, missile_enemy`
후방 배기 불꽃 포함. 유도탄은 뒤에 트레일을 붙이면 완성도가 올라갑니다.

---

## 5. 타격 / 피격 파티클 (핵심)

전부 **2D 스프라이트 시퀀스(플립북)** 입니다. 셀 128x128.

| 아틀라스 | 크기 | 프레임 | 타일 | 권장 FPS | 용도 |
|---|---|---|---|---|---|
| `fx_hit_atlas.png` | 512x256 | 8 | 4x2 | 30 | **타격** (총알이 적에게 맞음) |
| `fx_hit_energy_atlas.png` | 512x256 | 8 | 4x2 | 30 | 에너지/레이저 타격 (시안) |
| `fx_damage_atlas.png` | 512x256 | 8 | 4x2 | 30 | **피격** (플레이어가 맞음, 실드 파열형) |
| `fx_explosion_atlas.png` | 512x512 | 16 | 4x4 | 24 | 격추 폭발 |
| `fx_muzzle_atlas.png` | 384x256 | 6 | 3x2 | 45 | 총구 화염 (위 방향) |
| `fx_shockwave_atlas.png` | 384x256 | 6 | 3x2 | 30 | 충격파 링 |
| `fx_particles_atlas.png` | 512x512 | 16 | 4x4 | — | 낱개 파티클 텍스처 |

`fx_particles` 수록: soft_dot, hard_dot, star4, star6, spark_streak, shard, smoke_puff,
ring_thin, ring_soft, flare, debris, bubble, plus_spark, dust, hex, glow_ring_grad
→ 전부 **흰색 기준**이라 파티클 시스템 Start Color로 원하는 색을 입히면 됩니다.

### Unity 사용법 A — 파티클 시스템 플립북
1. Material: `Particles/Standard Unlit`, Rendering Mode `Additive`, Texture에 아틀라스 지정
2. Renderer → Render Mode `Billboard`, Material 연결
3. **Texture Sheet Animation** 체크 → Tiles X/Y 를 위 표대로 (예: 폭발 4 x 4)
4. Time Mode `Lifetime`, Frame over Time = 0→1 커브, Cycles 1
5. Start Lifetime = 프레임수 / FPS (폭발이면 16/24 ≈ 0.67초)
6. Emission: Rate over Time 0, Bursts에 Count 1 추가 (1회 재생)

### Unity 사용법 B — 스프라이트 애니메이션 (더 가벼움)
1. 아틀라스를 `Multiple` + `Grid By Cell Size 128x128` 로 슬라이스
2. 잘린 스프라이트들을 전부 선택 → 씬에 드래그 → Animation Clip 저장
3. Sample Rate를 표의 FPS로, Loop Time 해제
4. 오브젝트 풀링으로 재사용, 마지막 프레임에서 비활성화

```csharp
// 타격 이펙트 스폰 예시
void OnBulletHit(Vector3 pos, bool isEnergy) {
    var fx = pool.Get(isEnergy ? hitEnergyPrefab : hitPrefab);
    fx.transform.position = pos;
    fx.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f)); // 회전 랜덤화
    fx.transform.localScale = Vector3.one * Random.Range(0.85f, 1.15f);
}
```
회전·스케일을 매번 랜덤으로 주면 8프레임짜리도 반복이 티나지 않습니다.

---

## 6. 트레일 렌더러 텍스처

`trail_*.png` (256x64) — **오른쪽 끝이 머리(head), 왼쪽이 꼬리(tail)** 로 알파가 빠집니다.

| 파일 | 느낌 |
|---|---|
| `trail_soft` | 기본 부드러운 잔상 |
| `trail_taper` | 뾰족하게 좁아지는 형태 (총알/미사일 추천) |
| `trail_sharp` | 얇고 선명한 라인 |
| `trail_energy` | 코어+헤일로, 맥동 무늬 |
| `trail_smoke` | 노이즈 섞인 연기 |
| `trail_dotted` | 점선/펄스 |
| `trail_taper_<색>` | 10색 프리컬러 버전 |

**TrailRenderer 설정**
- Material: Unlit/Transparent 또는 `Particles/Standard Unlit` (Additive면 네온)
- Texture Mode: `Stretch` (텍스처를 트레일 전체에 한 번 매핑)
  - 반복 무늬를 원하면 `Tile` + `trail_dotted`
- Time: 0.15~0.35 / Min Vertex Distance: 0.02
- Width: 곡선으로 시작 1 → 끝 0
- Color: Gradient로 알파 1 → 0
- Alignment: `View`, Autodestruct 끄고 풀링

> 흰색 텍스처(`trail_soft` 등)에 Gradient 색을 입히는 방식이 가장 유연합니다.
> 색상 버전은 바로 쓰고 싶을 때 쓰세요.

---

## 7. 임포트 공통 설정 (Unity)

| 항목 | 값 |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Filter Mode | Bilinear |
| Compression | High Quality / RGBA32 (발광이 많아 압축 밴딩이 잘 보입니다) |
| Generate Mip Maps | 끄기 |
| Alpha Is Transparency | 켜기 |
| Max Size | 원본 그대로 |

Godot / Phaser / Cocos 를 쓰신다면 각 아틀라스 옆의 `*.json`
(TexturePacker hash 포맷: `frames{ name → frame/pivot }` + `meta.grid`)을 그대로 임포트하면 됩니다.

---

## 8. 커스터마이즈

`tools/` 안의 스크립트로 색/형태를 바꿔 다시 뽑을 수 있습니다.

```bash
pip install pillow numpy
python gen_ships.py        # 함선 (common.py의 PALETTES 수정)
python gen_projectiles.py  # 총알/미사일/트레일 (BULLET_COLORS 수정)
python gen_fx.py           # 파티클 프레임 (프레임 수/색 수정)
python pack.py             # 아틀라스 + JSON 재생성
```

예를 들어 초록 플레이어기가 필요하면 `gen_ships.py` 하단의
`("player_ship", "blue")` 를 `"green"` 으로 바꾸면 됩니다.
