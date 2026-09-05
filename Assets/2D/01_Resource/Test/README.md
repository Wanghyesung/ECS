# 2D 슈팅게임 에셋 팩 — 메탈 렌더링 스타일

보내주신 레퍼런스(정밀한 금속 함선 · 발광 볼트 · 강한 블룸)에 맞춰 다시 제작했습니다.
플랫한 벡터 느낌이 아니라 **원통형 금속 그라데이션 + 패널 라인 + 그리블(부속 디테일)
+ 발광 엔진 + 블룸** 파이프라인으로 렌더링했습니다.

> 이전 도트 세트는 지우지 않고 `alt_pixel/` 에 그대로 남겨뒀습니다.
> 배경(`backgrounds/`)도 변경 없이 유지됩니다.

---

## 1. 폴더 구조

```
ships/       ships_atlas.png(+json)  256x256 셀 · 8종
             boss_dreadnought.png (512x512)
bullets/     bullets_atlas.png       64x128 셀 · 60종
missiles/    missiles_atlas.png      64x128 셀 · 6종
fx/          타격 · 피격 · 폭발 · 총구화염 · 충격파 · 파티클 아틀라스
trails/      트레일 렌더러 텍스처 (256x64)
individual/  낱장 PNG 전부
backgrounds/ 1920x1080 배경
alt_pixel/   도트 버전 (원하시면 이쪽 사용)
tools/       생성 스크립트
```

## 2. 함선 — 256x256 셀

| 프레임 | 이름 | 방향 |
|---|---|---|
| 0~2 | player_ship / _red / _blue | ↑ |
| 3 | enemy_drone | ↓ |
| 4 | enemy_interceptor | ↓ |
| 5 | enemy_gunship | ↓ |
| 6 | enemy_bomber | ↓ |
| 7 | enemy_elite | ↓ |
| — | boss_dreadnought (512x512) | ↓ |

각 기체는 건메탈 도장 위에 **밝은 하이라이트 → 중간톤 → 어두운 면** 3단 원통 셰이딩,
패널 이음매, 흡기구·센서 같은 작은 그리블, 컬러 데칼, 발광 노즐로 구성했습니다.
엔진 플룸과 블룸이 스프라이트에 포함돼 있어 별도 이펙트 없이도 살아 있어 보입니다.

- 슬라이스: `Grid By Cell Size` `256 x 256`, Pivot `Center`
- Pixels Per Unit: **256** 권장
- 블룸이 알파에 섞여 있으니 **Alpha Is Transparency 켜기**, 압축은 `High Quality`

## 3. 총알 — 64x128 셀 (10색 × 6타입)

색 순서: `cyan, blue, green, yellow, orange, red, purple, pink, white, lime`

| 행 | 타입 | 설명 |
|---|---|---|
| 0 | `bolt` | 기본 에너지 볼트 (레퍼런스의 파란 탄과 같은 계열) |
| 1 | `small` | 소형 연사탄 |
| 2 | `long` | 롱 볼트 / 관통 |
| 3 | `heavy` | 대형 차지샷 |
| 4 | `orb` | 플라즈마 구체 |
| 5 | `beam` | 레이저 빔 (세로로 늘려도 자연스러움) |

전부 **흰 코어 → 밝은 색층 → 본체색 → 넓은 헤일로** 4단으로 쌓아
어두운 배경에서 확실히 뜨도록 만들었습니다. 위쪽이 진행 방향입니다.

> URP Bloom을 켜면 훨씬 좋습니다. 안 켜도 스프라이트에 블룸이 구워져 있습니다.

## 4. 미사일 — 64x128 셀

`standard / homing / heavy / plasma / toxic / enemy` 6종.
금속 동체 + 패널 이음매 + 컬러 노즈콘 + 카나드/테일핀 + 시커 렌즈 + 발광 배기까지
디테일을 넣었습니다. 유도탄 뒤에 `trail_taper_*` 를 붙이면 완성도가 확 올라갑니다.

## 5. 이펙트 (fx/)

| 아틀라스 | 프레임 | 타일 | FPS | 용도 |
|---|---|---|---|---|
| `fx_hit_atlas` | 8 | 4x2 | 30 | **타격** |
| `fx_hit_energy_atlas` | 8 | 4x2 | 30 | 에너지 타격 |
| `fx_damage_atlas` | 8 | 4x2 | 30 | **피격** |
| `fx_explosion_atlas` | 16 | 4x4 | 24 | 격추 폭발 |
| `fx_muzzle_atlas` | 6 | 3x2 | 45 | 총구 화염 (↑) |
| `fx_shockwave_atlas` | 6 | 3x2 | 30 | 충격파 |
| `fx_particles_atlas` | 16 | 4x4 | — | 낱개 파티클(흰색 기준) |

셀 크기는 전부 128x128입니다.
파티클 시스템: Material `Particles/Standard Unlit` + Rendering Mode `Additive`,
**Texture Sheet Animation** Tiles X/Y 를 표대로, Start Lifetime = 프레임수 / FPS.

```csharp
fx.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
fx.transform.localScale = Vector3.one * Random.Range(0.85f, 1.15f);
```

## 6. 트레일 (trails/)

`trail_soft / taper / sharp / energy / smoke / dotted` + 10색 taper 버전.
**오른쪽이 머리, 왼쪽이 꼬리**입니다.

- Material: `Particles/Standard Unlit` (Additive면 네온)
- Texture Mode `Stretch`, Time 0.15~0.35, Min Vertex Distance 0.02
- Width 곡선 1 → 0, Color Gradient 알파 1 → 0

## 7. 배경 (backgrounds/ · 1920x1080)

### 스크롤용 — 위아래 완벽하게 이어짐 ✅
`bg_void`(탄막 구간) · `bg_deepspace` · `bg_nebula_purple / _cyan / _crimson / _emerald`

노이즈·별을 주기적으로 생성해 첫 행과 마지막 행이 정확히 맞물립니다.
두 장을 세로로 이어붙여 무한 스크롤하면 이음매가 보이지 않습니다.

```csharp
public float speed = 1.2f;
void Update() {
    foreach (var t in bgs) {                       // 같은 배경 2장, y 간격 = 높이
        t.position += Vector3.down * speed * Time.deltaTime;
        if (t.position.y <= -bgHeight) t.position += Vector3.up * bgHeight * 2f;
    }
}
```

### 패럴랙스 레이어 (투명 PNG, 세로 타일링 ✅)

| 파일 | 권장 배속 |
|---|---|
| `layer_nebula_*` | ×0.15 |
| `layer_stars_far` | ×0.3 |
| `layer_stars_mid` | ×0.6 |
| `layer_stars_near` | ×1.0 |
| `layer_speedlines` | ×1.6 |

### 고정 배경 / 엘리먼트 (타일링 ❌)
`bg_planet_blue / _ringed / _green`, `element_planet_*` 4종.

## 8. 커스터마이즈 (tools/)

```bash
pip install pillow numpy
python gen_metal_ships.py   # 함선
python gen_metal_proj.py    # 총알 / 미사일
python gen_fx.py            # 이펙트
python gen_bg.py            # 배경
python pack_mt.py           # 아틀라스 + JSON
```

`metal.py` 가 렌더링 코어입니다.

- `METAL` / `FLAT` / `DOME` — 금속 그라데이션 램프. 스톱을 바꾸면 재질감이 바뀝니다.
- `Ship.hull_shape / plate / pod / seam / greebles / stripe / thruster`
  로 조립하는 구조라, 함선 함수에서 좌표만 바꾸면 새 기체가 나옵니다.
- 색은 인자 `(accent, glow, hull)` 세 개만 바꾸면 같은 형상의 다른 도장이 됩니다.
- `Canvas.out(bloom=...)` 값으로 발광 세기를 조절합니다 (0이면 블룸 없음).
