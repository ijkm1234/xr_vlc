using UnityEngine;

namespace XRVLC.Media
{
    /// <summary>
    /// Legacy Unity cue-rendered spatial subtitles have been retired.
    /// Spatial subtitles are now rendered by VLC into the external subtitle surface.
    /// This component remains as a scene-compatibility shell for existing prefabs.
    /// </summary>
    public sealed class SpatialSubtitleService : MonoBehaviour
    {
        public static void SetHardSuppressed(bool suppressed, string reason)
        {
        }
    }
}
