using System;

namespace XRVLC
{
    public static class VideoRotation
    {
        public const int StepDegrees = 90;

        public static int NormalizeToQuarterTurn(int degrees)
        {
            int normalized = degrees % 360;
            if (normalized > 180)
                normalized -= 360;
            else if (normalized < -180)
                normalized += 360;

            int steps = (int)Math.Round(
                normalized / (double)StepDegrees,
                MidpointRounding.AwayFromZero);
            return steps * StepDegrees;
        }

        public static bool IsSupported(VideoProjection projection)
        {
            return projection == VideoProjection.Flat || projection == VideoProjection.Cylinder;
        }

        public static bool UsesSideEdgeAsBottom(int degrees)
        {
            return Math.Abs(NormalizeToQuarterTurn(degrees)) == StepDegrees;
        }
    }
}
