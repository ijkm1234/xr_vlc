using UnityEngine;

namespace XRVLC
{
    public enum FisheyeProjectionFormula
    {
        Equidistant = 0,
        EquisolidAngle = 1,
        Stereographic = 2,
        Orthographic = 3
    }

    public static class FisheyeProjectionMath
    {
        public static float Radius(FisheyeProjectionFormula formula, float normalizedTheta)
        {
            float theta = Mathf.Clamp01(normalizedTheta) * Mathf.PI * 0.5f;
            switch (formula)
            {
                case FisheyeProjectionFormula.EquisolidAngle:
                    return Mathf.Sin(theta * 0.5f) / Mathf.Sin(Mathf.PI * 0.25f);
                case FisheyeProjectionFormula.Stereographic:
                    return Mathf.Tan(theta * 0.5f) / Mathf.Tan(Mathf.PI * 0.25f);
                case FisheyeProjectionFormula.Orthographic:
                    return Mathf.Sin(theta);
                default:
                    return normalizedTheta;
            }
        }
    }
}
