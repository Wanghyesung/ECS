Shader "Custom/ForceFieldBoundary"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.2, 0.6, 1, 1)
        _EdgeColor("Edge Color", Color) = (0.4, 0.9, 1, 1)
        _FresnelPower("Fresnel Power", Range(0.5, 8)) = 3
        _OutlinePower("Outline Power", Range(0.5, 16)) = 10
        _OutlineIntensity("Outline Intensity", Range(0, 1)) = 0.1
        _GlowRadius("Glow Radius (world units)", Float) = 60
        _MaxAlpha("Max Alpha", Range(0, 1)) = 0.85
        _DistortionStrength("Distortion Strength", Range(0, 1)) = 0.15
        _ScrollSpeed("Scroll Speed", Float) = 0.5
        _TargetWorldPos("Target World Pos", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForceField"
            Tags { "LightMode"="UniversalForward" }

            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS   : SV_POSITION;
                float3 normalWS      : TEXCOORD0;
                float3 viewDirWS     : TEXCOORD1;
                float  localProximity : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EdgeColor;
                float _FresnelPower;
                float _OutlinePower;
                float _OutlineIntensity;
                float _GlowRadius;
                float _MaxAlpha;
                float _DistortionStrength;
                float _ScrollSpeed;
                float4 _TargetWorldPos;
            CBUFFER_END

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionOS = IN.positionOS.xyz;

                // 경계 전체가 아니라 "플레이어와 가까운 지점"만 반응하도록, 정점의 월드 좌표와
                // 타겟(카메라/플레이어) 사이 거리로 국소 근접도를 먼저 계산 (오브젝트 전체 스칼라 X)
                float3 approxWorldPos = TransformObjectToWorld(positionOS);
                float distToTarget = distance(approxWorldPos, _TargetWorldPos.xyz);
                float localProximity = 1.0 - saturate(distToTarget / max(_GlowRadius, 0.001));
                localProximity *= localProximity;

                float ripple = sin(positionOS.y * 6.0 + _Time.y * _ScrollSpeed) * _DistortionStrength * localProximity;
                positionOS += IN.normalOS * ripple * 0.05;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS);
                OUT.positionHCS = vertexInput.positionCS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                OUT.localProximity = localProximity;
                return OUT;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(IN.viewDirWS);
                // Cull Off로 안팎을 모두 그리므로, 뒷면 노멀도 앞면과 대칭이 되도록 abs()를 사용
                // (saturate만 쓰면 뒷면 반구 전체가 dot<0 → fresnel=1로 뭉개져 테두리만 빛나야 할 게 원판 전체가 빛남)
                float fresnel = pow(1.0 - abs(dot(normalWS, viewDirWS)), _FresnelPower);

                float proximity = saturate(IN.localProximity);
                float baseGlow = proximity * proximity;
                float rimGlow = fresnel * proximity;

                // 근접도와 무관하게 항상 옅게 보이는 실루엣 윤곽선 - 멀리서도 경계의 돔 형태가 보이도록
                float outline = pow(1.0 - abs(dot(normalWS, viewDirWS)), _OutlinePower) * _OutlineIntensity;

                float3 color = lerp(_BaseColor.rgb, _EdgeColor.rgb, saturate(fresnel + proximity * 0.5)) * lerp(1.0, 3.0, proximity);
                float alpha = saturate(outline + baseGlow * 0.6 + rimGlow * 0.7) * _MaxAlpha;

                return float4(color, alpha);
            }
            ENDHLSL
        }
    }
}
