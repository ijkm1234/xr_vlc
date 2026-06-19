using System;

namespace XRVLC.Subtitles
{
    public readonly struct SubtitleBitmap
    {
        public static readonly SubtitleBitmap Empty = new SubtitleBitmap(0, 0, 0, Array.Empty<byte>(), false);

        public SubtitleBitmap(int width, int height, int stride, byte[] rgba, bool hasVisiblePixels)
        {
            Width = width;
            Height = height;
            Stride = stride;
            Rgba = rgba ?? Array.Empty<byte>();
            HasVisiblePixels = hasVisiblePixels;
        }

        public int Width { get; }
        public int Height { get; }
        public int Stride { get; }
        public byte[] Rgba { get; }
        public bool HasVisiblePixels { get; }
        public bool IsValid => Width > 0 && Height > 0 && Stride > 0 && Rgba.Length > 0 && HasVisiblePixels;
    }
}
