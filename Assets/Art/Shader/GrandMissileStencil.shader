Shader "Custom/GrandMissileStencil"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.39
        _Metallic ("Metallic", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // Stencil: Grand Missile 영역에 값 1을 기록
        Stencil
        {
            Ref 1
            Comp Always
            Pass Replace
            Fail Keep
            ZFail Keep
        }

        // Forward Lit 패스 (URP SimpleLit 기반)
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Smoothness;
                half _Metallic;
            CBUFFER_END

            // 플레이어 주변 관통 (글로벌 셰이더 프로퍼티)
            float4 _PlayerWorldPos;
            float _PlayerClipRadius;
            float _PlayerClipEdgeWidth;
            half4 _PlayerClipEdgeColor;

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 플레이어 주변 관통 처리 (스크린 스페이스 기준)
                float4 playerClip = TransformWorldToHClip(_PlayerWorldPos.xyz);
                float2 playerNDC = playerClip.xy / playerClip.w;

                float4 missileClip = TransformWorldToHClip(input.positionWS);
                float2 missileNDC = missileClip.xy / missileClip.w;

                // 종횡비 보정 (화면에서 원형으로 보이도록)
                float2 diff = missileNDC - playerNDC;
                diff.x *= _ScreenParams.x / _ScreenParams.y;
                float dist = length(diff);

                if (dist < _PlayerClipRadius - _PlayerClipEdgeWidth)
                    discard;
                if (dist < _PlayerClipRadius)
                    return _PlayerClipEdgeColor;

                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 color = texColor * _BaseColor;

                // 기본 조명 계산
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = color.rgb;
                surfaceData.alpha = color.a;
                surfaceData.smoothness = _Smoothness;
                surfaceData.metallic = _Metallic;

                half4 litColor = UniversalFragmentPBR(inputData, surfaceData);
                litColor.rgb = MixFog(litColor.rgb, input.fogFactor);

                return litColor;
            }
            ENDHLSL
        }

        // ShadowCaster 패스 (그림자 생성용)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // DepthOnly 패스 (깊이 버퍼 기록용)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
