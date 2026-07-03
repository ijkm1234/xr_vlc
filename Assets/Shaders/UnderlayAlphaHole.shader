Shader "Hidden/XRVLC/UnderlayAlphaHole"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Geometry-100"
        }

        Pass
        {
            Name "AlphaHole"
            Cull Off
            ZTest Always
            ZWrite Off
            ColorMask A

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(0.0h, 0.0h, 0.0h, 0.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
