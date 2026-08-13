---
name: mobile
description: "모바일 최적화 — 타일 기반 GPU, ASTC 텍스처, 드로우 콜 예산(<100), 열 스로틀링, 배터리, 터치 입력, 세이프 에리어, 앱스토어 가이드라인."
alwaysApply: true
globs: ["**/*.cs"]
---

# 모바일 최적화

## GPU 아키텍처

모바일 GPU는 **타일 기반 렌더링(TBDR)**을 사용한다. 주요 시사점은 다음과 같다:
- 오버드로우 비용이 크다 — 투명 레이어를 최소화할 것
- 알파 테스트 지오메트리는 비용이 크다(early-Z가 깨짐)
- 프래그먼트 셰이더 복잡도를 낮게 유지할 것
- 가능하면 풀스크린 포스트 프로세싱을 피할 것

## 성능 예산

| 지표 | 저사양 | 중간사양 | 고사양 |
|--------|---------|-----------|----------|
| 드로우 콜 | < 50 | < 100 | < 200 |
| 삼각형 수 | < 50k | < 100k | < 200k |
| 프레임 타임 | 33ms(30fps) | 16.6ms(60fps) | 16.6ms |
| 텍스처 메모리 | < 100MB | < 150MB | < 256MB |
| 전체 메모리 | < 300MB | < 500MB | < 800MB |
| 빌드 크기 | < 100MB | < 200MB | < 500MB |

## 텍스처 압축

- **ASTC** — iOS와 Android 양쪽 모두에 사용(품질 대비 용량 비율이 가장 좋음)
- 최대 크기: UI 요소는 512, 소품은 1024, 히어로 캐릭터는 2048
- 3D 오브젝트는 밉맵을 활성화하고, UI 스프라이트는 비활성화할 것
- 드로우 콜을 줄이기 위해 텍스처 아틀라스를 사용할 것

## 드로우 콜 줄이기

1. **SRP Batcher** — URP에서 기본 활성화되어 있으니, 셰이더 호환성을 반드시 확인할 것
2. **GPU Instancing** — 반복되는 오브젝트(나무, 바위, 적)에 사용
3. **Static Batching** — 움직이지 않는 환경 오브젝트에 사용
4. **Texture Atlasing** — 스프라이트 시트를 하나로 결합
5. **머티리얼 공유** — 같은 머티리얼이면 같은 배치로 묶임

## 셰이더 복잡도

- 프래그먼트당 수학 연산 수를 제한할 것
- 종속 텍스처 읽기(dependent texture read)를 피할 것
- 가능하면 `half` 정밀도를 사용할 것(색상, UV, 노멀)
- 저사양 기기에서는 실시간 그림자를 쓰지 말 것(베이크된 그림자만 사용)
- 저사양 기기에서는 포스트 프로세싱 스택을 피하거나 더 단순한 대안을 사용할 것

## 열 스로틀링(Thermal Throttling)

```csharp
// Adaptive Performance 패키지
using UnityEngine.AdaptivePerformance;

private void Update()
{
    IAdaptivePerformance refAP = Holder.Instance;
    if (refAP != null && refAP.ThermalStatus.ThermalMetrics.WarningLevel > WarningLevel.NoWarning)
    {
        // 품질 낮추기: 해상도 낮추기, 파티클 감소, 프레임레이트 제한
        QualitySettings.resolutionScalingFixedDPIFactor = 0.75f;
    }
}
```

- 캐주얼 게임은 30fps를 목표로 할 것(배터리 절약)
- 60fps는 옵트인 옵션으로 제공할 것
- 배터리 20% 미만일 때는 GPU 부하를 줄일 것
- 열 상태를 모니터링하며 동적으로 다운스케일할 것

## 터치 입력

```csharp
// Input System 터치
[SerializeField] private float m_fSwipeThreshold = 50f;

// 최소 탭 타겟: 44x44 포인트(Apple HIG)
// 최소 탭 타겟: 48x48 dp(Material Design)
```

- 탭(Tap): 주 터치 press+release가 0.3초 미만
- 스와이프(Swipe): 한 방향으로 delta가 threshold를 초과
- 핀치(Pinch): 두 손가락 사이 거리 변화
- 롱 프레스(Long press): 0.5초 이상 유지
- 드래그(Drag): press + 이동

## 세이프 에리어(Safe Area)

```csharp
private void ApplySafeArea()
{
    Rect safeArea = Screen.safeArea;
    Vector2 vAnchorMin = safeArea.position;
    Vector2 vAnchorMax = safeArea.position + safeArea.size;

    vAnchorMin.x /= Screen.width;
    vAnchorMin.y /= Screen.height;
    vAnchorMax.x /= Screen.width;
    vAnchorMax.y /= Screen.height;

    RectTransform refRect = GetComponent<RectTransform>();
    refRect.anchorMin = vAnchorMin;
    refRect.anchorMax = vAnchorMax;
}
```

노치와 라운드 코너를 고려하려면 루트 UI 패널에 적용할 것.

## 오디오 압축

| 타입 | 포맷 | 품질 | Load Type |
|------|--------|---------|-----------|
| 음악 | Vorbis | 40-60% | Streaming |
| SFX(짧음) | ADPCM | — | Decompress On Load |
| SFX(김) | Vorbis | 70% | Compressed In Memory |
| UI 클릭음 | PCM | — | Decompress On Load |

## 빌드 크기

- 사용하지 않는 코드를 스트립할 것(IL2CPP Stripping: High)
- 텍스처를 공격적으로 압축할 것
- 선택적/DLC 콘텐츠에는 Addressables를 사용할 것
- manifest에서 사용하지 않는 패키지를 제거할 것
- 초기 다운로드 용량은 100MB 미만을 목표로 할 것(App Store 권장 사항)
