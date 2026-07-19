namespace XRVLC
{
    public struct VideoGeometrySelection
    {
        public VideoProjection Projection;
        public StereoMode Stereo;
        public FlatVideoCurveMode CurveMode;
        public FisheyeProjectionFormula FisheyeProjectionFormula;

        public VideoGeometrySelection(
            VideoProjection projection,
            StereoMode stereo,
            FlatVideoCurveMode curveMode,
            FisheyeProjectionFormula fisheyeProjectionFormula = FisheyeProjectionFormula.Equidistant)
        {
            Projection = projection;
            Stereo = stereo;
            CurveMode = curveMode;
            FisheyeProjectionFormula = fisheyeProjectionFormula;
        }
    }
}
