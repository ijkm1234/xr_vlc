Shader "XRVLC/ProceduralStarfieldSkybox"
{
    Properties
    {
        _StarGridSize ("Star Distribution Rows", Range(4, 24)) = 6
        _MinStarRadius ("Minimum Star Radius (Pixels)", Range(0.35, 1.5)) = 0.55
        _MaxStarRadius ("Maximum Star Radius (Pixels)", Range(0.5, 2.5)) = 1.35
        _RadiusTwinkleAmount ("Radius Twinkle Amount", Range(0, 0.5)) = 0.25
        _BrightnessTwinkleAmount ("Brightness Twinkle Amount", Range(0, 0.8)) = 0.35
        _MinBrightness ("Minimum Brightness", Range(0, 1)) = 0.35
        _MaxBrightness ("Maximum Brightness", Range(0, 1)) = 0.95
        _BlueStarColor ("Blue Star Color", Color) = (0.35, 0.62, 1.0, 1.0)
        _TwinkleSpeed ("Twinkle Speed", Range(0, 4)) = 1.8
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
                float _MinStarRadius;
                float _MaxStarRadius;
                float _RadiusTwinkleAmount;
                float _BrightnessTwinkleAmount;
                float _MinBrightness;
                float _MaxBrightness;
                float4 _BlueStarColor;
                float _TwinkleSpeed;
                float _Seed;
            CBUFFER_END

            #define MAX_STARS_PER_REGION 3

            float3 Hash33(float3 value)
            {
                value = frac(value * float3(0.1031, 0.1030, 0.0973));
                value += dot(value, value.yxz + 33.33);
                return frac((value.xxy + value.yxx) * value.zyx);
            }

            float3 RandomForCell(float3 cell, float salt)
            {
                float3 offset = float3(
                    _Seed * 11.73 + salt * 19.19,
                    _Seed * 23.57 + salt * 73.31,
                    _Seed * 37.17 + salt * 41.53);
                return Hash33(cell + offset);
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
                float distributionRows = max(4.0, floor(_StarGridSize + 0.5));
                float distributionColumns = floor(
                    distributionRows * PI + 0.5);
                float2 distributionSize = float2(
                    distributionColumns,
                    distributionRows);

                // Uniform steps in azimuth and direction.y divide a sphere into
                // equal-area regions. Each region owns at least one jittered star,
                // removing the large voids from probability-based thinning.
                float azimuth01 = frac(
                    atan2(direction.z, direction.x) / TWO_PI + 0.5);
                float elevation01 = min(
                    saturate(direction.y * 0.5 + 0.5),
                    0.999999);
                float distributionRow = floor(
                    elevation01 * distributionRows);

                // Rotate every equal-area row independently so adjacent rows do
                // not reveal aligned longitudinal columns.
                float rowRotation = RandomForCell(
                    float3(0.0, distributionRow, 0.0),
                    0.5).x;
                float shiftedAzimuth01 = frac(
                    azimuth01 + rowRotation / distributionColumns);
                float2 cell = float2(
                    floor(shiftedAzimuth01 * distributionColumns),
                    distributionRow);
                float3 cell3D = float3(cell, 0.0);
                int starsInRegion = 1 + (int)floor(
                    RandomForCell(cell3D, 12.0).x *
                    (float)MAX_STARS_PER_REGION);

                // Project the angular offset to pixel space. This preserves the
                // subpixel stability of the previous shader in stereo rendering.
                float3 directionDx = ddx(direction);
                float3 directionDy = ddy(direction);
                float dxSquared = dot(directionDx, directionDx);
                float dxdy = dot(directionDx, directionDy);
                float dySquared = dot(directionDy, directionDy);
                float determinant = max(
                    dxSquared * dySquared - dxdy * dxdy,
                    0.000000000001);
                half3 accumulatedStars = 0.0h;

                // Every region contains one to three independently jittered stars.
                // The mean remains two, but local density is less repetitive.
                [unroll]
                for (
                    int starIndex = 0;
                    starIndex < MAX_STARS_PER_REGION;
                    starIndex++)
                {
                    if (starIndex >= starsInRegion)
                    {
                        break;
                    }

                    float saltOffset = (float)starIndex * 3.0;
                    float3 positionRandom = RandomForCell(
                        cell3D,
                        1.0 + saltOffset);
                    float2 shiftedStarUv = (
                        cell + 0.08 + positionRandom.xy * 0.84) /
                        distributionSize;
                    float starU = frac(
                        shiftedStarUv.x -
                        rowRotation / distributionColumns);
                    float starY = shiftedStarUv.y * 2.0 - 1.0;
                    float starAzimuth = (starU - 0.5) * TWO_PI;
                    float starHorizontal = sqrt(
                        saturate(1.0 - starY * starY));
                    float starSin;
                    float starCos;
                    sincos(starAzimuth, starSin, starCos);
                    float3 starDirection = float3(
                        starCos * starHorizontal,
                        starY,
                        starSin * starHorizontal);

                    float3 randomA = RandomForCell(
                        cell3D,
                        2.0 + saltOffset);
                    float3 randomB = RandomForCell(
                        cell3D,
                        3.0 + saltOffset);

                    float3 directionDelta = starDirection - direction;
                    float deltaDx = dot(directionDelta, directionDx);
                    float deltaDy = dot(directionDelta, directionDy);
                    float2 starPixelOffset = float2(
                        (deltaDx * dySquared - deltaDy * dxdy) / determinant,
                        (deltaDy * dxSquared - deltaDx * dxdy) / determinant);
                    float starPixelDistance = length(starPixelOffset);

                    float baseRadius = lerp(
                        _MinStarRadius,
                        _MaxStarRadius,
                        randomA.y);
                    float phase = randomB.y * TWO_PI;
                    float speedVariation = lerp(0.65, 1.35, randomB.z);
                    float radiusWave = sin(
                        _Time.y * _TwinkleSpeed * speedVariation + phase);
                    float twinkle01 = radiusWave * 0.5 + 0.5;
                    float starRadius = max(
                        0.1,
                        baseRadius *
                        (1.0 + radiusWave * _RadiusTwinkleAmount));
                    float innerRadius = max(0.0, starRadius - 0.5);
                    float starMask = 1.0 - smoothstep(
                        innerRadius,
                        starRadius + 0.5,
                        starPixelDistance);

                    float brightness = lerp(
                        _MinBrightness,
                        _MaxBrightness,
                        randomB.x);
                    brightness *= lerp(
                        1.0 - _BrightnessTwinkleAmount,
                        1.0,
                        twinkle01);
                    float3 starColor = lerp(
                        float3(1.0, 1.0, 1.0),
                        _BlueStarColor.rgb,
                        randomA.z);

                    accumulatedStars += (half3)(
                        starMask * brightness * starColor);
                }

                return half4(saturate(accumulatedStars), 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
