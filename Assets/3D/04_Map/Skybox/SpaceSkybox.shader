Shader "Skybox/ProceduralSpace"
{
    Properties
    {
        _ZenithColor("Zenith Color", Color) = (0.12, 0.14, 0.28, 1)
        _HorizonColor("Horizon Color", Color) = (0.22, 0.18, 0.32, 1)
        _NebulaColor("Nebula Color", Color) = (0.35, 0.2, 0.45, 1)
        _NebulaStrength("Nebula Strength", Range(0, 1)) = 0.5
        _StarDensity("Star Density", Range(20, 400)) = 140
        _StarBrightness("Star Brightness", Range(0, 5)) = 2.2
        _TwinkleSpeed("Twinkle Speed", Float) = 2
        _Exposure("Exposure", Range(0.2, 3)) = 1.3
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            fixed4 _ZenithColor;
            fixed4 _HorizonColor;
            fixed4 _NebulaColor;
            float _NebulaStrength;
            float _StarDensity;
            float _StarBrightness;
            float _TwinkleSpeed;
            float _Exposure;

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.pos = UnityObjectToClipPos(IN.vertex);
                // 스카이박스 메시는 카메라 중심의 큐브 - 오브젝트 공간 정점 위치 자체가 바깥 방향 벡터
                OUT.dir = IN.vertex.xyz;
                return OUT;
            }

            float Hash13(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            fixed4 Frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.dir);

                // 위/아래 방향 그라디언트 (검정이 아니라 어두운 네이비~보라)
                float t = saturate(dir.y * 0.5 + 0.5);
                float3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, t);

                // 은은한 네뷸라 색 얼룩 - 큰 격자 단위 랜덤값을 부드럽게 블렌드
                float nebulaN = Hash13(floor(dir * 3.0));
                col += _NebulaColor.rgb * smoothstep(0.55, 1.0, nebulaN) * _NebulaStrength;

                // 별 - 방향을 촘촘한 격자로 나눠 셀마다 하나씩 랜덤 점을 별로 표시
                float3 cell = floor(dir * _StarDensity);
                float starRand = Hash13(cell);
                float starMask = step(0.992, starRand);
                float twinkle = 0.6 + 0.4 * sin(_Time.y * _TwinkleSpeed + starRand * 100.0);
                col += starMask * _StarBrightness * twinkle;

                col *= _Exposure;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }

    FallBack Off
}
