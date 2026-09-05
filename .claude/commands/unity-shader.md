---
name: unity-shader
description: "셰이더를 생성하거나 디버깅합니다 — HLSL/ShaderLab을 작성하고, MCP를 통해 머티리얼을 생성하며, 테스트 오브젝트에 적용하고, 렌더링 통계를 확인합니다."
user-invocable: true
args: shader_description
---

# /unity-shader — 셰이더 생성

다음을 기반으로 셰이더를 생성합니다: **$ARGUMENTS**

## 작업 흐름

`unity-shader-dev` 에이전트를 사용하여 다음을 수행합니다:

1. **셰이더 유형 결정** — URP Lit, Unlit, 커스텀 이펙트(모바일 최적화, 컴퓨트 셰이더 미사용)
2. **셰이더 파일 작성** (`.shader` 또는 `.hlsl`):
   - URP 인클루드 및 HLSL 구조
   - SRP 배처(Batcher) 호환 (CBUFFER_START)
   - 적절한 태그 및 렌더 큐
3. **머티리얼 생성** — `manage_material` MCP를 통해 — 셰이더를 할당합니다
4. **테스트 오브젝트에 적용** — `manage_components` MCP를 통해:
   - 테스트용 메시를 생성하거나 찾습니다
   - 머티리얼을 할당합니다
5. **렌더링 통계 확인** — `manage_graphics` MCP를 통해:
   - SRP 배처 호환성을 확인합니다
   - 드로우 콜에 미치는 영향을 확인합니다
6. **콘솔 확인** — `read_console`를 통해 셰이더 컴파일 오류를 확인합니다

셰이더 파일, 머티리얼, 그리고 성능 관련 참고 사항을 보고합니다.
