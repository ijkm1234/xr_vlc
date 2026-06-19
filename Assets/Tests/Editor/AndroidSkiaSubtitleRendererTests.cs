using NUnit.Framework;
using XRVLC.Media;
using XRVLC.Subtitles;

namespace XRVLC.Tests
{
    [TestFixture]
    public class AndroidSkiaSubtitleRendererTests
    {
        [Test]
        public void Render_ReturnsEmptyResultInEditor()
        {
            var renderer = new AndroidSkiaSubtitleRenderer();
            var cue = new SubtitleCue
            {
                version = 2,
                text = "Hello",
                styleRuns = new[]
                {
                    new SubtitleCueStyleRun
                    {
                        start = 0,
                        end = 5,
                        fillColor = "#FFFFFFFF",
                        outlineColor = "#FF000000",
                        outlineWidth = 3f
                    }
                }
            };
            cue.Normalize();

            SubtitleBitmap bitmap = renderer.Render(cue, 512, 128);

            Assert.IsFalse(bitmap.IsValid);
            Assert.AreEqual(0, bitmap.Width);
            Assert.AreEqual(0, bitmap.Height);
            Assert.AreEqual(0, bitmap.Rgba.Length);
        }
    }
}
