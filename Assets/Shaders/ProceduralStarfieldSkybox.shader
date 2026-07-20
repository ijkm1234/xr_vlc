Shader "XRVLC/ProceduralStarfieldSkybox"
{
    Properties
    {
        _StarGridSize ("Star Grid Size", Range(16, 96)) = 48
        _StarDensity ("Star Density", Range(0, 0.2)) = 0.00875
        _MinBrightness ("Minimum Brightness", Range(0, 1)) = 0.35
        _MaxBrightness ("Maximum Brightness", Range(0, 1)) = 0.95
        _TwinkleSpeed ("Twinkle Speed", Range(0, 4)) = 0.9
        _Seed ("Seed", Float) = 17
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ColorMask RGB

        Pass
        {
            Name "ProceduralStarfield"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 directionOS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float _StarGridSize;
                float _StarDensity;
                float _MinBrightness;
                float _MaxBrightness;
                float _TwinkleSpeed;
                float _Seed;
            CBUFFER_END

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float RandomForCell(float2 cell, float faceId, float salt)
            {
                float2 offset = float2(
                    faceId * 37.17 + _Seed * 11.73,
                    faceId * 91.41 + _Seed * 23.57);
                return Hash21(cell + offset + float2(salt * 19.19, salt * 73.31));
            }

            float2 GetFaceUv(float3 direction, out float faceId)
            {
                float3 absoluteDirection = abs(direction);
                float2 faceUv;

                if (absoluteDirection.x >= absoluteDirection.y && absoluteDirection.x >= absoluteDirection.z)
                {
                    faceUv = direction.zy / absoluteDirection.x;
                    faceId = direction.x >= 0.0 ? 0.0 : 1.0;
                }
                else if (absoluteDirection.y >= absoluteDirection.z)
                {
                    faceUv = direction.xz / absoluteDirection.y;
                    faceId = direction.y >= 0.0 ? 2.0 : 3.0;
                }
                else
                {
                    faceUv = direction.xy / absoluteDirection.z;
                    faceId = direction.z >= 0.0 ? 4.0 : 5.0;
                }

                return faceUv * 0.5 + 0.5;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.directionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 direction = normalize(input.directionOS);
                float faceId;
                float2 faceUv = GetFaceUv(direction, faceId);
                float2 gridPosition = faceUv * _StarGridSize;
                float2 cell = floor(gridPosition);
                float2 positionInCell = frac(gridPosition);

                float starExists = step(1.0 - _StarDensity, RandomForCell(cell, faceId, 1.0));
                float2 starPosition = float2(
                    RandomForCell(cell, faceId, 2.0),
                    RandomForCell(cell, faceId, 3.0));
                starPosition = lerp(0.18, 0.82, starPosition);

                float2 gridDx = ddx(gridPosition);
                float2 gridDy = ddy(gridPosition);
                float determinant = gridDx.x * gridDy.y - gridDx.y * gridDy.x;
                float safeDeterminant = abs(determinant) > 0.000001
                    ? determinant
                    : (determinant >= 0.0 ? 0.000001 : -0.000001);
                float2 starDelta = positionInCell - starPosition;
                float2 starPixelOffset = float2(
                    (starDelta.x * gridDy.y - starDelta.y * gridDy.x) / safeDeterminant,
                    (gridDx.x * starDelta.y - gridDx.y * starDelta.x) / safeDeterminant);
                float starMask = step(
                    max(abs(starPixelOffset.x), abs(starPixelOffset.y)),
                    0.5);

                float brightness = lerp(
                    _MinBrightness,
                    _MaxBrightness,
                    RandomForCell(cell, faceId, 5.0));
                float phase = RandomForCell(cell, faceId, 6.0) * TWO_PI;
                float speedVariation = lerp(
                    0.65,
                    1.35,
                    RandomForCell(cell, faceId, 7.0));
                float twinkleWave = sin(_Time.y * _TwinkleSpeed * speedVariation + phase);
                brightness *= max(0.0, twinkleWave);

                half star = (half)(starExists * starMask * brightness);
                return half4(star, star, star, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
