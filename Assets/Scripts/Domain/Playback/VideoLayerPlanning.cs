using System;

namespace XRVLC
{
    public enum VideoOutputLayerShape
    {
        Quad,
        Cylinder,
        Equirect
    }

    public readonly struct InputLayerSpec : IEquatable<InputLayerSpec>
    {
        public InputLayerSpec(uint contentWidth, uint contentHeight, bool hardwareDecoding)
        {
            ContentWidth = contentWidth;
            ContentHeight = contentHeight;
            HardwareDecoding = hardwareDecoding;
        }

        public uint ContentWidth { get; }
        public uint ContentHeight { get; }
        public bool HardwareDecoding { get; }

        public bool Equals(InputLayerSpec other)
        {
            return ContentWidth == other.ContentWidth
                && ContentHeight == other.ContentHeight
                && HardwareDecoding == other.HardwareDecoding;
        }

        public override bool Equals(object obj) => obj is InputLayerSpec other && Equals(other);
        public override int GetHashCode() => (ContentWidth, ContentHeight, HardwareDecoding).GetHashCode();
    }

    public readonly struct MapperSpec : IEquatable<MapperSpec>
    {
        public MapperSpec(
            bool fisheyeMappingEnabled,
            bool chromaKeyEnabled,
            StereoMode stereo,
            uint contentWidth,
            uint contentHeight)
        {
            FisheyeMappingEnabled = fisheyeMappingEnabled;
            ChromaKeyEnabled = chromaKeyEnabled;
            Stereo = stereo;
            ContentWidth = contentWidth;
            ContentHeight = contentHeight;
        }

        public bool FisheyeMappingEnabled { get; }
        public bool ChromaKeyEnabled { get; }
        public StereoMode Stereo { get; }
        public uint ContentWidth { get; }
        public uint ContentHeight { get; }

        public bool Equals(MapperSpec other)
        {
            return FisheyeMappingEnabled == other.FisheyeMappingEnabled
                && ChromaKeyEnabled == other.ChromaKeyEnabled
                && Stereo == other.Stereo
                && ContentWidth == other.ContentWidth
                && ContentHeight == other.ContentHeight;
        }

        public override bool Equals(object obj) => obj is MapperSpec other && Equals(other);
        public override int GetHashCode() =>
            (FisheyeMappingEnabled, ChromaKeyEnabled, Stereo, ContentWidth, ContentHeight).GetHashCode();
    }

    public readonly struct OutputLayerSpec : IEquatable<OutputLayerSpec>
    {
        public OutputLayerSpec(
            uint contentWidth,
            uint contentHeight,
            VideoOutputLayerShape shape,
            StereoMode stereo,
            bool alphaBlending)
        {
            ContentWidth = contentWidth;
            ContentHeight = contentHeight;
            Shape = shape;
            Stereo = stereo;
            AlphaBlending = alphaBlending;
        }

        public uint ContentWidth { get; }
        public uint ContentHeight { get; }
        public VideoOutputLayerShape Shape { get; }
        public StereoMode Stereo { get; }
        public bool AlphaBlending { get; }

        public bool Equals(OutputLayerSpec other)
        {
            return ContentWidth == other.ContentWidth
                && ContentHeight == other.ContentHeight
                && Shape == other.Shape
                && Stereo == other.Stereo
                && AlphaBlending == other.AlphaBlending;
        }

        public override bool Equals(object obj) => obj is OutputLayerSpec other && Equals(other);
        public override int GetHashCode() =>
            (ContentWidth, ContentHeight, Shape, Stereo, AlphaBlending).GetHashCode();
    }

    public readonly struct VideoLayerSpec
    {
        public VideoLayerSpec(
            InputLayerSpec input,
            MapperSpec mapper,
            OutputLayerSpec output,
            VideoProjection projection,
            FlatVideoCurveMode curveMode)
        {
            Input = input;
            Mapper = mapper;
            Output = output;
            Projection = projection;
            CurveMode = curveMode;
        }

        public InputLayerSpec Input { get; }
        public MapperSpec Mapper { get; }
        public OutputLayerSpec Output { get; }
        public VideoProjection Projection { get; }
        public FlatVideoCurveMode CurveMode { get; }
    }

    public readonly struct VideoLayerDelta
    {
        public VideoLayerDelta(bool rebuildInput, bool rebuildOutput, bool mapperChanged)
        {
            RebuildInput = rebuildInput;
            RebuildOutput = rebuildOutput;
            MapperChanged = mapperChanged;
        }

        public bool RebuildInput { get; }
        public bool RebuildOutput { get; }
        public bool MapperChanged { get; }
    }

    public static class VideoLayerPlanner
    {
        public static VideoLayerSpec Create(
            uint contentWidth,
            uint contentHeight,
            bool hardwareDecoding,
            VideoProjection projection,
            StereoMode stereo,
            FlatVideoCurveMode curveMode,
            bool chromaKeyEnabled)
        {
            return new VideoLayerSpec(
                new InputLayerSpec(contentWidth, contentHeight, hardwareDecoding),
                new MapperSpec(
                    projection == VideoProjection.Fisheye180,
                    chromaKeyEnabled,
                    stereo,
                    contentWidth,
                    contentHeight),
                new OutputLayerSpec(
                    contentWidth,
                    contentHeight,
                    GetOutputShape(projection),
                    stereo,
                    chromaKeyEnabled),
                projection,
                curveMode);
        }

        public static VideoLayerDelta Classify(
            VideoLayerSpec? bound,
            bool inputSurfaceValid,
            bool outputSurfaceValid,
            VideoLayerSpec target)
        {
            bool rebuildInput = !inputSurfaceValid
                || !bound.HasValue
                || !bound.Value.Input.Equals(target.Input);
            bool rebuildOutput = !outputSurfaceValid
                || !bound.HasValue
                || !bound.Value.Output.Equals(target.Output);
            bool mapperChanged = !bound.HasValue
                || !bound.Value.Mapper.Equals(target.Mapper);
            return new VideoLayerDelta(rebuildInput, rebuildOutput, mapperChanged);
        }

        public static bool NeedsInputRebuild(
            InputLayerSpec? bound,
            bool inputSurfaceValid,
            InputLayerSpec target)
        {
            return !inputSurfaceValid || !bound.HasValue || !bound.Value.Equals(target);
        }

        public static bool NeedsOutputRebuild(
            OutputLayerSpec? bound,
            bool outputSurfaceValid,
            OutputLayerSpec target)
        {
            return !outputSurfaceValid || !bound.HasValue || !bound.Value.Equals(target);
        }

        public static VideoOutputLayerShape GetOutputShape(VideoProjection projection)
        {
            return projection switch
            {
                VideoProjection.Cylinder => VideoOutputLayerShape.Cylinder,
                VideoProjection.Sphere360 or VideoProjection.Sphere180 or VideoProjection.Fisheye180 =>
                    VideoOutputLayerShape.Equirect,
                _ => VideoOutputLayerShape.Quad
            };
        }
    }
}
