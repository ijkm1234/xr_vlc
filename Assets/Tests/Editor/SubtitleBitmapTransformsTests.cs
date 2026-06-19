using NUnit.Framework;
using XRVLC.Subtitles;

namespace XRVLC.Tests
{
    [TestFixture]
    public class SubtitleBitmapTransformsTests
    {
        [Test]
        public void ToUnityTextureRgba_RotatesRgbaBitmapBy180Degrees()
        {
            byte[] rgba =
            {
                1, 0, 0, 255, 2, 0, 0, 255,
                3, 0, 0, 255, 4, 0, 0, 255
            };
            var bitmap = new SubtitleBitmap(2, 2, 8, rgba, hasVisiblePixels: true);

            byte[] transformed = SubtitleBitmapTransforms.ToUnityTextureRgba(bitmap, rotate180: true);

            byte[] expected =
            {
                4, 0, 0, 255, 3, 0, 0, 255,
                2, 0, 0, 255, 1, 0, 0, 255
            };
            CollectionAssert.AreEqual(expected, transformed);
        }

        [Test]
        public void ToUnityTextureRgba_CompactsRowsWhenStrideHasPadding()
        {
            byte[] rgba =
            {
                1, 0, 0, 255, 2, 0, 0, 255, 99, 99, 99, 99,
                3, 0, 0, 255, 4, 0, 0, 255, 88, 88, 88, 88
            };
            var bitmap = new SubtitleBitmap(2, 2, 12, rgba, hasVisiblePixels: true);

            byte[] transformed = SubtitleBitmapTransforms.ToUnityTextureRgba(bitmap, rotate180: false);

            byte[] expected =
            {
                1, 0, 0, 255, 2, 0, 0, 255,
                3, 0, 0, 255, 4, 0, 0, 255
            };
            CollectionAssert.AreEqual(expected, transformed);
        }

        [Test]
        public void ToUnityTextureRgba_FlipsRowsVerticallyWithoutMirroringColumns()
        {
            byte[] rgba =
            {
                1, 0, 0, 255, 2, 0, 0, 255,
                3, 0, 0, 255, 4, 0, 0, 255
            };
            var bitmap = new SubtitleBitmap(2, 2, 8, rgba, hasVisiblePixels: true);

            byte[] transformed = SubtitleBitmapTransforms.ToUnityTextureRgba(
                bitmap,
                flipVertical: true,
                flipHorizontal: false);

            byte[] expected =
            {
                3, 0, 0, 255, 4, 0, 0, 255,
                1, 0, 0, 255, 2, 0, 0, 255
            };
            CollectionAssert.AreEqual(expected, transformed);
        }
    }
}
