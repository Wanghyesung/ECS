---
name: urp-pipeline
description: "Universal Render Pipeline — URP 에셋 구성, Renderer Feature, 2D 렌더러, 라이팅, 그림자, 포스트 프로세싱 볼륨, SRP 배처."
globs: ["**/URP*.asset", "**/*Renderer*.asset", "**/*Volume*.cs"]
---

# Universal Render Pipeline (URP)

## URP 파이프라인 에셋 구성

URP Pipeline Asset은 전역 렌더링 설정을 제어한다. Assets > Create > Rendering > URP Asset (with Universal Renderer) 메뉴로 생성한다.

### 주요 파이프라인 에셋 설정

| 설정 | 권장값 | 비고 |
|---------|-------------|-------|
| HDR | 활성화 | Bloom과 컬러 그레이딩에 필수 |
| Anti-Aliasing | MSAA 4x 또는 FXAA | 모바일은 MSAA, 데스크톱은 FXAA |
| Shadow Resolution | 2048(데스크톱) / 1024(모바일) | 품질과 성능의 균형 |
| Shadow Cascade Count | 4(데스크톱) / 2(모바일) | 캐스케이드가 많을수록 그림자 분포가 좋아짐 |
| Shadow Distance | 50~150 | 게임 스케일에 따라 다름 |
| SRP Batcher | 활성화 | 드로우 콜 최적화의 핵심 |
| Dynamic Batching | SRP Batcher 사용 시 비활성화 | 서로 충돌하며, SRP Batcher가 더 우수함 |

### 스크립트로 파이프라인 에셋 구성하기

```csharp
using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class URPQualityManager : MonoBehaviour
{
    [SerializeField] private UniversalRenderPipelineAsset[] m_arrSOQualityLevels;

    public void SetQualityLevel(int _iLevel)
    {
        if (_iLevel >= 0 && _iLevel < m_arrSOQualityLevels.Length)
            QualitySettings.renderPipeline = m_arrSOQualityLevels[_iLevel];
    }

    public void AdjustShadowDistance(float _fDistance)
    {
        var refURPAsset = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
        refURPAsset.shadowDistance = _fDistance;
    }
}
```

## Renderer Feature(커스텀 렌더 패스)

Renderer Feature를 사용하면 URP의 렌더 파이프라인에 커스텀 렌더링 로직을 주입할 수 있다.

### 커스텀 Renderer Feature 만들기

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class OutlineRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public sealed class OutlineSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        public Material outlineMaterial;
        public LayerMask layerMask;
        [Range(1, 4)] public int downSample = 1;
    }

    public OutlineSettings settings = new OutlineSettings();
    private OutlineRenderPass m_outlinePass;

    public override void Create()
    {
        m_outlinePass = new OutlineRenderPass(settings);
        m_outlinePass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer _refRenderer, ref RenderingData _renderingData)
    {
        if (settings.outlineMaterial == null) return;
        _refRenderer.EnqueuePass(m_outlinePass);
    }
}
```

### 커스텀 Render Pass

```csharp
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class OutlineRenderPass : ScriptableRenderPass
{
    private readonly OutlineRendererFeature.OutlineSettings m_settings;
    private RTHandle m_refTempTexture;

    public OutlineRenderPass(OutlineRendererFeature.OutlineSettings _settings)
    {
        m_settings = _settings;
        profilingSampler = new ProfilingSampler("OutlinePass");
    }

    public override void OnCameraSetup(CommandBuffer _refCmd, ref RenderingData _renderingData)
    {
        var desc = _renderingData.cameraData.cameraTargetDescriptor;
        desc.depthBufferBits = 0;
        RenderingUtils.ReAllocateIfNeeded(ref m_refTempTexture, desc, name: "_TempOutline");
    }

    public override void Execute(ScriptableRenderContext _context, ref RenderingData _renderingData)
    {
        CommandBuffer refCmd = CommandBufferPool.Get();
        using (new ProfilingScope(refCmd, profilingSampler))
        {
            var refSource = _renderingData.cameraData.renderer.cameraColorTargetHandle;
            Blitter.BlitCameraTexture(refCmd, refSource, m_refTempTexture, m_settings.outlineMaterial, 0);
            Blitter.BlitCameraTexture(refCmd, m_refTempTexture, refSource);
        }
        _context.ExecuteCommandBuffer(refCmd);
        CommandBufferPool.Release(refCmd);
    }

    public override void OnCameraCleanup(CommandBuffer _refCmd)
    {
        m_refTempTexture?.Release();
    }
}
```

## Forward vs Forward+ 렌더러

- **Forward**: 전통적인 포워드 렌더링 방식. 모바일에 적합하며, 오브젝트당 추가 라이트 개수에 제한이 있다.
- **Forward+**: 클러스터드 라이팅(clustered lighting)을 사용한다. 오브젝트당 라이트 개수 제한이 사라진다. 라이트가 많은 씬에 유리하다. Unity 2022.2 이상이 필요하다.

Universal Renderer Data 에셋의 Rendering Path 항목에서 설정한다.

## 2D Renderer 설정

2D 게임에서는 2D Renderer를 사용한다:

1. 2D Renderer가 포함된 URP Asset을 생성(Assets > Create > Rendering > URP Asset with 2D Renderer)
2. 2D Renderer가 지원하는 것: Light2D, ShadowCaster2D, Sprite-Lit-Default 셰이더
3. Light2D 컴포넌트 사용: Global, Freeform, Sprite, Point, Spot

```csharp
using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class DynamicLight2DController : MonoBehaviour
{
    private Light2D m_refLight2D;

    private void Awake()
    {
        m_refLight2D = GetComponent<Light2D>();
    }

    public void SetIntensity(float _fIntensity)
    {
        m_refLight2D.intensity = _fIntensity;
    }

    public void FlickerLight(float _fMinIntensity, float _fMaxIntensity)
    {
        m_refLight2D.intensity = Random.Range(_fMinIntensity, _fMaxIntensity);
    }
}
```

## URP 라이팅 설정

### 메인 라이트(Directional)

- 그림자 타입: 품질을 위해서는 Soft Shadows, 성능을 위해서는 Hard
- 그림자 해상도: 라이트별로 설정하거나 Pipeline Asset에서 전역으로 설정
- 그림자 바이어스: Normal Bias 0.4, Depth Bias 1.0(시작값 기준)

### 추가 라이트(Additional Lights)

- Pipeline Asset에서 오브젝트당 제한을 설정(Forward 기본값은 4)
- Forward+는 클러스터드 라이팅으로 이 제한을 없앤다
- 추가 라이트의 그림자 지원은 Pipeline Asset에서 반드시 활성화해야 한다

### 그림자 설정

```
Pipeline Asset:
  Shadow Distance: 100
  Cascade Count: 4
  Cascade Ratios: 0.067, 0.2, 0.467 (기본값)
  Depth Bias: 1
  Normal Bias: 1
  Soft Shadows: Enabled
```

## 포스트 프로세싱(Volume 프레임워크)

URP는 포스트 프로세싱에 Volume 프레임워크를 사용한다. 별도의 포스트 프로세싱 패키지가 필요 없다.

### 포스트 프로세싱 설정하기

1. Camera 컴포넌트에서 Post Processing을 활성화
2. Global Volume을 생성(또는 콜라이더가 있는 Local Volume)
3. Volume Override를 추가(Bloom, Color Grading 등)

### 자주 쓰는 Volume Profile 스크립트

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class PostProcessController : MonoBehaviour
{
    [SerializeField] private Volume m_refGlobalVolume;

    private Bloom m_refBloom;
    private ColorAdjustments m_refColorAdjustments;
    private Vignette m_refVignette;
    private ChromaticAberration m_refChromaticAberration;

    private void Awake()
    {
        var refProfile = m_refGlobalVolume.profile;
        refProfile.TryGet(out m_refBloom);
        refProfile.TryGet(out m_refColorAdjustments);
        refProfile.TryGet(out m_refVignette);
        refProfile.TryGet(out m_refChromaticAberration);
    }

    public void SetDamageEffect(float _fIntensity)
    {
        if (m_refVignette != null)
        {
            m_refVignette.intensity.Override(Mathf.Lerp(0.2f, 0.6f, _fIntensity));
            m_refVignette.color.Override(Color.Lerp(Color.black, Color.red, _fIntensity));
        }

        if (m_refChromaticAberration != null)
            m_refChromaticAberration.intensity.Override(_fIntensity * 0.5f);
    }

    public void SetBloomIntensity(float _fIntensity)
    {
        if (m_refBloom != null)
            m_refBloom.intensity.Override(_fIntensity);
    }

    public void SetExposure(float _fExposure)
    {
        if (m_refColorAdjustments != null)
            m_refColorAdjustments.postExposure.Override(_fExposure);
    }
}
```

### 톤매핑 모드

- **None**: HDR 원본값을 그대로 클램프. 권장하지 않음.
- **Neutral**: 색상 변화가 최소화됨. 기본값으로 무난함.
- **ACES**: 필름 같은 느낌에 채도가 높음. 업계 표준.

## SRP Batcher 호환성

SRP Batcher는 셰이더 변형(variant) 단위로 드로우 콜을 묶는다. 셰이더를 호환되게 만들려면 다음을 따른다:

### 셰이더는 반드시 CBUFFER를 사용해야 한다

```hlsl
CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float _Smoothness;
    float4 _BaseMap_ST;
CBUFFER_END
```

**SRP Batcher 호환성을 위한 규칙:**
- 모든 머티리얼 프로퍼티는 `UnityPerMaterial` CBUFFER 안에 있어야 한다
- 엔진 내장 프로퍼티는 `UnityPerDraw` CBUFFER 안에 있어야 한다
- MaterialPropertyBlock은 배칭을 깨뜨리므로 URP에서는 사용을 피할 것

### SRP Batcher 호환성 확인하기

인스펙터에서 아무 셰이더나 선택한다. "SRP Batcher" 필드에 Compatible 또는 Not Compatible 여부와 그 사유가 표시된다.

## URP 셰이더 Include

```hlsl
// 커스텀 셰이더를 위한 URP 코어 include
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
```

## 카메라 스태킹

카메라 스태킹은 여러 카메라를 순서대로 렌더링한다(예: 월드 카메라 + UI 카메라 + 미니맵).

```csharp
using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class CameraStackManager : MonoBehaviour
{
    [SerializeField] private Camera m_refBaseCamera;
    [SerializeField] private Camera m_refUICamera;
    [SerializeField] private Camera m_refMinimapCamera;

    private void Awake()
    {
        var refBaseCameraData = m_refBaseCamera.GetUniversalAdditionalCameraData();
        refBaseCameraData.renderType = CameraRenderType.Base;

        var refUICameraData = m_refUICamera.GetUniversalAdditionalCameraData();
        refUICameraData.renderType = CameraRenderType.Overlay;

        var refMinimapData = m_refMinimapCamera.GetUniversalAdditionalCameraData();
        refMinimapData.renderType = CameraRenderType.Overlay;

        // 오버레이 카메라들을 스택에 추가
        refBaseCameraData.cameraStack.Add(m_refUICamera);
        refBaseCameraData.cameraStack.Add(m_refMinimapCamera);
    }
}
```

**카메라 스태킹 규칙:**
- Base 카메라는 하나만 존재하며 가장 먼저 렌더링된다
- Overlay 카메라들은 스택 순서대로 그 위에 렌더링된다
- Overlay 카메라 하나마다 렌더 패스 전체가 추가된다(비용이 큼)
- 꼭 필요한 경우에만 사용할 것 — 가능하면 레이어를 활용한 단일 카메라를 우선할 것

## Render Pass Events

커스텀 렌더 패스를 만들 때는 적절한 삽입 지점(injection point)을 선택한다:

| 이벤트 | 사용 사례 |
|-------|----------|
| BeforeRenderingShadows | 커스텀 그림자 패스 |
| AfterRenderingShadows | 그림자 후처리 |
| BeforeRenderingOpaques | 불투명 오브젝트 이전 이펙트 |
| AfterRenderingOpaques | 아웃라인, SSAO |
| BeforeRenderingTransparents | 투명 오브젝트 뒤쪽 이펙트 |
| AfterRenderingTransparents | 디스토션 이펙트 |
| BeforeRenderingPostProcessing | 커스텀 프리-포스트 이펙트 |
| AfterRenderingPostProcessing | 최종 오버레이, UI 이펙트 |
| AfterRendering | 디버그 시각화 |
