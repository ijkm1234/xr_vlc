using UnityEngine;
using XRVLC.Media;

namespace XRVLC.Subtitles
{
    public sealed class AndroidSkiaSubtitleRenderer : ISubtitleTextureRenderer
    {
        private const string RendererClassName = "org.videolan.vlc.bridge.subtitle.XrSubtitleSkiaRenderer";

        public SubtitleBitmap Render(SubtitleCue cue, int widthPx, int maxHeightPx)
        {
            if (cue == null || cue.IsClear || string.IsNullOrEmpty(cue.text))
                return SubtitleBitmap.Empty;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                string cueJson = JsonUtility.ToJson(cue);
                using (var rendererClass = new AndroidJavaClass(RendererClassName))
                using (var result = rendererClass.CallStatic<AndroidJavaObject>("renderCue", cueJson, widthPx, maxHeightPx))
                {
                    if (result == null) return SubtitleBitmap.Empty;

                    int width = result.Call<int>("getWidth");
                    int height = result.Call<int>("getHeight");
                    int stride = result.Call<int>("getStride");
                    byte[] rgba = result.Call<byte[]>("getRgba") ?? System.Array.Empty<byte>();
                    bool hasVisiblePixels = result.Call<bool>("getHasVisiblePixels");
                    return new SubtitleBitmap(width, height, stride, rgba, hasVisiblePixels);
                }
            }
            catch (AndroidJavaException e)
            {
                Debug.LogWarning($"[AndroidSkiaSubtitleRenderer] renderCue failed: {e.Message}");
                return SubtitleBitmap.Empty;
            }
#else
            return SubtitleBitmap.Empty;
#endif
        }
    }
}
