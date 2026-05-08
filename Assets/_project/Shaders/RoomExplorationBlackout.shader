Shader "Eclipse Protocol/Room Exploration Blackout"
{
    Properties
    {
        _BlackColor ("Blackout Color", Color) = (0, 0, 0, 1)
        _OverlayAlpha ("Overlay Alpha", Range(0, 1)) = 1
        _Feather ("Edge Feather", Range(0, 4)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "RoomExplorationBlackout"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_EXPLORED_RECTS 32

            CBUFFER_START(UnityPerMaterial)
                float4 _BlackColor;
                float _OverlayAlpha;
                float _Feather;
                int _ExploredRectCount;
                float4 _PlayAreaRect;
                float4 _ExploredRects[MAX_EXPLORED_RECTS];
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            float RectMask(float2 position, float4 rect)
            {
                float2 insideDistance = min(position - rect.xy, rect.zw - position);
                float edgeDistance = min(insideDistance.x, insideDistance.y);
                float feather = max(_Feather, 0.0001);
                return smoothstep(0.0, feather, edgeDistance);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 worldXZ = input.positionWS.xz;
                float playAreaMask = RectMask(worldXZ, _PlayAreaRect);
                float exploredMask = 0.0;

                [unroll]
                for (int i = 0; i < MAX_EXPLORED_RECTS; i++)
                {
                    if (i >= _ExploredRectCount)
                    {
                        break;
                    }

                    exploredMask = max(exploredMask, RectMask(worldXZ, _ExploredRects[i]));
                }

                float blackout = saturate((1.0 - playAreaMask) + (1.0 - exploredMask));
                return half4(_BlackColor.rgb, _BlackColor.a * _OverlayAlpha * blackout);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
