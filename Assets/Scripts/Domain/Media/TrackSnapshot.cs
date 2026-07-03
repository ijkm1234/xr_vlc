using System;
using System.Collections.Generic;

namespace XRVLC.Media
{
    [Serializable]
    public sealed class TrackSnapshot
    {
        public List<TrackInfo> AudioTracks { get; } = new List<TrackInfo>();
        public List<TrackInfo> SubtitleTracks { get; } = new List<TrackInfo>();
    }
}
