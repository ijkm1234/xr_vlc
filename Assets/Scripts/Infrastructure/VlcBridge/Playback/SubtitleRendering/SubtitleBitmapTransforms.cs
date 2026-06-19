using System;

namespace XRVLC.Subtitles
{
    public static class SubtitleBitmapTransforms
    {
        private const int BytesPerPixel = 4;

        public static byte[] ToUnityTextureRgba(SubtitleBitmap bitmap, bool rotate180)
        {
            return ToUnityTextureRgba(bitmap, flipVertical: rotate180, flipHorizontal: rotate180);
        }

        public static byte[] ToUnityTextureRgba(SubtitleBitmap bitmap, bool flipVertical, bool flipHorizontal)
        {
            if (!bitmap.IsValid)
                return Array.Empty<byte>();

            int rowBytes = bitmap.Width * BytesPerPixel;
            if (bitmap.Stride < rowBytes)
                return Array.Empty<byte>();

            int requiredBytes = (bitmap.Height - 1) * bitmap.Stride + rowBytes;
            if (bitmap.Rgba.Length < requiredBytes)
                return Array.Empty<byte>();

            byte[] output = new byte[bitmap.Width * bitmap.Height * BytesPerPixel];
            if (!flipVertical && !flipHorizontal)
                CopyCompact(bitmap, output);
            else
                CopyTransformed(bitmap, output, flipVertical, flipHorizontal);

            return output;
        }

        private static void CopyCompact(SubtitleBitmap bitmap, byte[] output)
        {
            int rowBytes = bitmap.Width * BytesPerPixel;
            for (int y = 0; y < bitmap.Height; y++)
            {
                Buffer.BlockCopy(bitmap.Rgba, y * bitmap.Stride, output, y * rowBytes, rowBytes);
            }
        }

        private static void CopyTransformed(SubtitleBitmap bitmap, byte[] output, bool flipVertical, bool flipHorizontal)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;

            for (int y = 0; y < height; y++)
            {
                int sourceRow = y * bitmap.Stride;
                int targetY = flipVertical ? height - 1 - y : y;

                for (int x = 0; x < width; x++)
                {
                    int sourceOffset = sourceRow + x * BytesPerPixel;
                    int targetX = flipHorizontal ? width - 1 - x : x;
                    int targetOffset = (targetY * width + targetX) * BytesPerPixel;

                    output[targetOffset] = bitmap.Rgba[sourceOffset];
                    output[targetOffset + 1] = bitmap.Rgba[sourceOffset + 1];
                    output[targetOffset + 2] = bitmap.Rgba[sourceOffset + 2];
                    output[targetOffset + 3] = bitmap.Rgba[sourceOffset + 3];
                }
            }
        }
    }
}
