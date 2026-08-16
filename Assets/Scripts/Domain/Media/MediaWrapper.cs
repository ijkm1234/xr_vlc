using System;
using System.Collections.Generic;

namespace XRVLC.Media
{
    [Serializable]
    public class SlaveDTO
    {
        public int type;
        public int priority;
        public string uri;
    }

    [Serializable]
    public class MediaWrapper
    {
        public string Id { get; set; }
        public string Uri { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string ArtworkUrl { get; set; }
        public long DurationMs { get; set; }

        public long Time { get; set; }
        public bool IsSeen { get; set; }

        public MediaProjectionType Projection { get; set; }
        public string StereoHint { get; set; }

        public string AudioTrack { get; set; } = "-1";
        public string SpuTrack { get; set; } = "-1";
        
        public bool FromStart { get; set; }
        public int PositionInList { get; set; }
        public string Source { get; set; }
        public string MediaType { get; set; }
        public List<SlaveDTO> Slaves { get; set; } = new List<SlaveDTO>();
        
        // 存储从 Android 端传来的原始 JSON，以便在播放时原样透传回底层
        public string RawJson { get; set; }
    }

}
