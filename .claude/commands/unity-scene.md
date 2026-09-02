---
name: unity-scene
description: "MCP만으로 Unity 씬을 완전히 구축하거나 수정합니다 — 게임오브젝트, 계층 구조, 조명, 카메라, 물리 레이어."
user-invocable: true
args: scene_description
---

# /unity-scene — 씬 구축

다음 설명을 기반으로 씬을 구축하거나 수정합니다: **$ARGUMENTS**

## 워크플로우

`unity-scene-builder` 에이전트를 사용하여 다음을 수행합니다.

1. **씬 계획** — 게임오브젝트, 컴포넌트, 계층 구조, 조명, 카메라 설정을 식별합니다
2. **씬 생성 또는 로드** — `manage_scene` MCP를 통해 수행 (템플릿 사용: `3d_basic` 또는 `2d_basic`)
3. **계층 구조 구축** — `batch_execute`를 사용
   - 환경 오브젝트 (바닥, 벽, 플랫폼)
   - 캐릭터 스폰 지점
   - 카메라 (Cinemachine 가상 카메라)
   - 조명 (방향광, 점광)
   - 시스템 오브젝트 (매니저, 스포너)
4. **컴포넌트 구성** — `manage_components`를 통해 수행
5. **물리 설정** — `manage_physics`를 통해 수행 (레이어, 충돌 매트릭스)
6. **카메라 설정** — `manage_camera`를 통해 수행 (팔로우, 컨파이너, 블렌딩)
7. **검증** — `read_console`을 통해 오류가 없는지 확인

## 계층 구조 규칙
```
@Environment/ — 정적 월드 지오메트리
@Characters/  — 플레이어, NPC, 적
@Cameras/     — 메인 카메라, 가상 카메라
@Lighting/    — 조명, 반사 프로브
@UI/          — 캔버스
@Systems/     — 매니저, 스포너
_Dynamic/     — 런타임에 생성되는 오브젝트의 부모
```

완료되면 전체 씬 구조를 보고합니다.
