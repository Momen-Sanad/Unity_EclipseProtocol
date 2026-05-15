Shader "Eclipse Protocol/Runtime Solid Color"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _BaseColor;

            struct Attributes
            {
                float4 vertex : POSITION;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                return _BaseColor;
            }
            ENDCG
        }
    }
}
