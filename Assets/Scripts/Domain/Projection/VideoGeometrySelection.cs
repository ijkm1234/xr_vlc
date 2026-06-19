namespace XRVLC
{
    public struct VideoGeometrySelection
    {
        public VideoProjection Projection;
        public StereoMode Stereo;
        public FlatVideoCurveMode CurveMode;

        public VideoGeometrySelection(VideoProjection projection, StereoMode stereo, FlatVideoCurveMode curveMode)
        {
            Projection = projection;
            Stereo = stereo;
            CurveMode = curveMode;
        }
    }
}
