---
name: cinemachine
description: "Cinemachine 카메라 시스템 — virtual camera, FreeLook, 블렌딩, 노이즈 프로파일, 상태 기반 카메라, Confiner, follow/aim 동작. 카메라 연출 작업에 사용합니다."
globs: ["**/*Cinemachine*", "**/*Camera*.cs", "**/*Cam*.cs"]
---

# Cinemachine

## 설정

1. Main Camera에 `CinemachineBrain` 컴포넌트 추가
2. Virtual Camera 생성 — Brain이 우선순위가 가장 높은 카메라로 자동 블렌딩함

## Virtual Camera 구성 요소

**Body (추종):**
- `Transposer` — 3D 오프셋 추종 (댐핑 설정 가능)
- `Framing Transposer` — 2D/스크린 스페이스 추종 (데드 존, 소프트 존)
- `Orbital Transposer` — 타겟 주위를 공전 (사용자 입력으로 회전)
- `Tracked Dolly` — 경로를 따라 이동

**Aim (조준):**
- `Composer` — 데드/소프트 존을 이용해 타겟을 프레임 안에 유지
- `Group Composer` — 여러 타겟을 그룹으로 프레이밍
- `Hard Look At` — 댐핑 없이 즉시 바라봄
- `POV` — 플레이어가 직접 제어하는 회전 (FPS)

## 일반적인 설정

### 2D 플랫포머 카메라
```
Virtual Camera:
  Body: Framing Transposer
    - Screen X/Y: 0.5 (중앙)
    - Dead Zone Width: 0.1, Height: 0.1
    - Damping: X=1, Y=0.5
  Follow: Player Transform
  Add Extension: CinemachineConfiner2D
    - Bounding Shape: PolygonCollider2D (방 경계)
```

### 3D 3인칭 카메라
```
FreeLook Camera:
  Follow: Player Transform
  Look At: Player Head/Chest
  Top/Middle/Bottom Rig:
    - Rig별 Height, Radius
    - 각 Rig마다 Composer로 조준
  X Axis: 마우스/스틱 입력 (공전)
  Y Axis: 마우스/스틱 입력 (고도)
```

### State-Driven Camera (Animator)
```
State-Driven Camera:
  Animated Target: Player Animator
  States:
    Idle → VCam_Idle (와이드샷)
    Run → VCam_Run (더 멀리서)
    Combat → VCam_Combat (오버 숄더)
```

## 카메라 블렌딩

- 기본 블렌드: 2초, EaseInOut
- 트랜지션별로 커스텀 블렌드 설정 가능 (VCam A → VCam B)
- 즉시 전환하려면 Cut (0초) 사용

## Cinemachine Impulse (화면 흔들림)

```csharp
// 소스: 임펄스를 생성함
[SerializeField] private CinemachineImpulseSource m_refImpulseSource;

public void OnExplosion()
{
    m_refImpulseSource.GenerateImpulse();
}
```

반응해야 할 virtual camera에는 `CinemachineImpulseListener` 익스텐션을 추가하세요.

## Noise (핸드헬드 느낌)

virtual camera에 `CinemachineBasicMultiChannelPerlin`을 추가하세요:
- Profile: `6D Shake` 또는 `Handheld_normal_mild`
- 강도 조절은 Amplitude/Frequency로

## 코드로 제어하기

```csharp
// 우선순위로 카메라 전환
m_refCombatCamera.Priority = 20; // 값이 높을수록 활성화됨
m_refExploreCamera.Priority = 10;

// Follow 대상 변경
m_refVirtualCamera.Follow = refNewTarget;
m_refVirtualCamera.LookAt = refNewTarget;
```

## Confiner

- **2D:** `CinemachineConfiner2D` + `PolygonCollider2D` (Collider는 트리거로 설정, 물리 연산이 없는 레이어 사용)
- **3D:** `CinemachineConfiner` + `BoxCollider` 또는 `MeshCollider` 볼륨
