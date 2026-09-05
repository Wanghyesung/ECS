---
name: shader-graph
description: "ShaderGraph — 커스텀 함수 노드, 서브 그래프, 키워드 기반 변형, 마스터 스택 출력, URP 이펙트를 위한 공통 패턴."
globs: ["**/*.shadergraph", "**/*.shadersubgraph"]
---

# ShaderGraph

## 개요

ShaderGraph는 Unity의 비주얼 셰이더 에디터다. 노드 기반이며 URP와 HDRP 모두에서 동작한다. 그래프로부터 HLSL 셰이더 코드를 생성한다.

## 마스터 스택 출력

### Vertex 스테이지
- Position (object/world/absolute world)
- Normal (object/tangent)

### Fragment 스테이지 (URP Lit)
- Base Color, Normal (Tangent), Metallic, Smoothness, Emission, Ambient Occlusion, Alpha

### Fragment 스테이지 (URP Unlit)
- Base Color, Alpha

## Custom Function 노드

### 인라인(작은 함수)
```hlsl
// Custom Function 노드에서, Type: String으로 설정
void MyFunction_float(float3 In, out float3 Out)
{
    Out = In * 2.0;
}
```

### 외부 파일(복잡한 함수)
프로젝트에 `.hlsl` 파일을 생성한다:
```hlsl
// Assets/Shaders/MyFunctions.hlsl
void TriplanarMapping_float(
    float3 Position, float3 Normal, float Sharpness,
    UnityTexture2D Tex, UnitySamplerState Sampler,
    out float4 Color)
{
    float3 blend = pow(abs(Normal), Sharpness);
    blend /= dot(blend, 1.0);

    float4 xProj = SAMPLE_TEXTURE2D(Tex, Sampler, Position.yz);
    float4 yProj = SAMPLE_TEXTURE2D(Tex, Sampler, Position.xz);
    float4 zProj = SAMPLE_TEXTURE2D(Tex, Sampler, Position.xy);

    Color = xProj * blend.x + yProj * blend.y + zProj * blend.z;
}
```

Custom Function 노드에서 참조: Source = Asset, File = MyFunctions.hlsl

## 키워드(Shader Variants)

- **Boolean Keyword:** 머티리얼별로 기능을 켜고 끔
- **Enum Keyword:** N개의 옵션 중 하나를 선택
- 사용하지 않으면 스트립되는 `shader_feature`를 사용하고, 항상 포함되는 `multi_compile`은 지양할 것
- 머티리얼 전용 키워드에는 `shader_feature_local`을 사용할 것

셰이더당 전체 변형(variant) 개수를 **1000개 미만**으로 유지할 것.

## 공통 패턴

### 디졸브 이펙트
1. 노이즈 텍스처를 샘플링(Gradient Noise 또는 텍스처)
2. 노이즈 값을 "Dissolve Amount" 프로퍼티와 비교(Step 또는 SmoothStep)
3. Alpha 출력에 곱함
4. 디졸브 경계에 emission을 추가(경계 = threshold보다 살짝 큰 범위)

### Fresnel / 림 라이팅
1. Fresnel Effect 노드(View Direction, Normal)
2. 색상을 곱함
3. Emission에 더함

### UV 스크롤(Water, Lava)
1. Time 노드 → 스크롤 속도를 곱함
2. UV 좌표에 더함
3. 변경된 UV로 텍스처를 샘플링

### 버텍스 변위(Wind, Waves)
1. Object Position + Time → 노이즈 함수
2. 변위량을 곱함
3. Vertex Position 출력에 더함

### 아웃라인(Inverted Hull Method)
2-패스 구성: Pass 1 = 일반 렌더, Pass 2 = 버텍스를 바깥으로 확장한 뒷면을 단색으로 렌더.
(URP에서는 커스텀 Renderer Feature가 필요하거나, 머티리얼 두 개를 사용하는 ShaderGraph 구성이 필요하다.)

## 서브 그래프(Sub-Graphs)

재사용 가능한 노드 그룹. 다음과 같은 공통 연산에 대해 만들어둘 것:
- 트라이플래너 매핑(Triplanar mapping)
- 회전을 포함한 타일링/오프셋
- 블렌드 모드(overlay, multiply, screen)
- 패럴랙스 매핑

## 성능 팁

- 프래그먼트당 텍스처 샘플 횟수를 최소화할 것
- 가능하면 `half` 정밀도를 사용할 것(그래프 설정에서 지정)
- 분기(branching)를 피할 것(대신 lerp/step 사용)
- 키워드가 적을수록 변형이 적어지고 빌드 시간도 빨라진다
- Shader Inspector에서 변형 개수를 미리 확인할 것
