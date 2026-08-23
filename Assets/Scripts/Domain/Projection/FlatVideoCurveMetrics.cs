namespace XRVLC
{
    public static class FlatVideoCurveMetrics
    {
        public const float SmallCurveCylinderRadius = 32f;
        public const float LargeCurveCylinderRadius = 16f;

        public static float GetCylinderRadius(FlatVideoCurveMode curveMode)
        {
            return curveMode == FlatVideoCurveMode.Large
                ? LargeCurveCylinderRadius
                : SmallCurveCylinderRadius;
        }

        public static float GetCylinderCentralAngle(float flatWidthMeters, FlatVideoCurveMode curveMode)
        {
            if (flatWidthMeters <= 0f)
                return 0f;

            return flatWidthMeters / GetCylinderRadius(curveMode);
        }
    }
}
