using System;

namespace XRVLC.Media
{
    public enum SubtitleCueSource
    {
        Unknown,
        Text,
        Bitmap,
        Clear
    }

    [Serializable]
    public class SubtitleCueSegment
    {
        public string text;
        public bool bold;
        public bool italic;
        public bool underline;
        public string color;

        public void Normalize()
        {
            if (text == null) text = string.Empty;
            if (color == null) color = string.Empty;
        }
    }

    [Serializable]
    public class SubtitleCueStyleRun
    {
        public int start;
        public int end;
        public string fontFamily;
        public float fontSize;
        public bool bold;
        public bool italic;
        public string fillColor;
        public string outlineColor;
        public float outlineWidth;

        public void Normalize()
        {
            if (fontFamily == null) fontFamily = string.Empty;
            if (fillColor == null) fillColor = string.Empty;
            if (outlineColor == null) outlineColor = string.Empty;
        }
    }

    [Serializable]
    public class SubtitleCueFontAttachment
    {
        public string name;
        public string family;
        public string mime;
        public string cachePath;
        public int size;
        public string sha256;

        public void Normalize()
        {
            if (name == null) name = string.Empty;
            if (family == null) family = string.Empty;
            if (mime == null) mime = string.Empty;
            if (cachePath == null) cachePath = string.Empty;
            if (sha256 == null) sha256 = string.Empty;
        }
    }

    [Serializable]
    public class SubtitleCueLayout
    {
        public string align;
        public int marginL;
        public int marginR;
        public int marginV;

        public static SubtitleCueLayout Default()
        {
            return new SubtitleCueLayout
            {
                align = "bottom-center",
                marginL = 0,
                marginR = 0,
                marginV = 0
            };
        }

        public void Normalize()
        {
            if (string.IsNullOrEmpty(align)) align = "bottom-center";
        }
    }

    [Serializable]
    public class SubtitleCue
    {
        public int version;
        public int seq;
        public string trackId;
        public long startMs;
        public long endMs;
        public string text;
        public SubtitleCueSegment[] segments;
        public SubtitleCueStyleRun[] styleRuns;
        public SubtitleCueFontAttachment[] fontAttachments;
        public SubtitleCueLayout layout;
        public string align;
        public string source;

        public SubtitleCueSource Source => ParseSource(source);
        public bool IsClear => Source == SubtitleCueSource.Clear;

        public static SubtitleCue Clear(int sequence = 0)
        {
            return new SubtitleCue
            {
                version = 1,
                seq = sequence,
                trackId = string.Empty,
                startMs = 0,
                endMs = 0,
                text = string.Empty,
                segments = Array.Empty<SubtitleCueSegment>(),
                styleRuns = Array.Empty<SubtitleCueStyleRun>(),
                fontAttachments = Array.Empty<SubtitleCueFontAttachment>(),
                layout = SubtitleCueLayout.Default(),
                align = "bottom-center",
                source = "clear"
            };
        }

        public void Normalize()
        {
            if (version <= 0) version = 1;
            if (trackId == null) trackId = string.Empty;
            if (text == null) text = string.Empty;
            align = string.IsNullOrEmpty(align) ? "bottom-center" : align;
            source = string.IsNullOrEmpty(source) ? (string.IsNullOrEmpty(text) ? "clear" : "text") : source;
            if (segments == null) segments = Array.Empty<SubtitleCueSegment>();
            if (styleRuns == null) styleRuns = Array.Empty<SubtitleCueStyleRun>();
            if (fontAttachments == null) fontAttachments = Array.Empty<SubtitleCueFontAttachment>();
            if (layout == null) layout = SubtitleCueLayout.Default();
            layout.Normalize();

            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null) segments[i] = new SubtitleCueSegment();
                segments[i].Normalize();
            }

            for (int i = 0; i < styleRuns.Length; i++)
            {
                if (styleRuns[i] == null) styleRuns[i] = new SubtitleCueStyleRun();
                styleRuns[i].Normalize();
            }

            for (int i = 0; i < fontAttachments.Length; i++)
            {
                if (fontAttachments[i] == null) fontAttachments[i] = new SubtitleCueFontAttachment();
                fontAttachments[i].Normalize();
            }
        }

        private static SubtitleCueSource ParseSource(string value)
        {
            if (string.IsNullOrEmpty(value)) return SubtitleCueSource.Unknown;

            switch (value.Trim().ToLowerInvariant())
            {
                case "text":
                    return SubtitleCueSource.Text;
                case "bitmap":
                    return SubtitleCueSource.Bitmap;
                case "clear":
                    return SubtitleCueSource.Clear;
                default:
                    return SubtitleCueSource.Unknown;
            }
        }
    }
}
