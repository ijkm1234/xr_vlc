using XRVLC.Media;

namespace XRVLC.Subtitles
{
    public interface ISubtitleTextureRenderer
    {
        SubtitleBitmap Render(SubtitleCue cue, int widthPx, int maxHeightPx);
    }
}
