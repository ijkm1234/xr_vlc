using System.Collections.Generic;
using XRVLC.Media;

namespace XRVLC.Media
{
    public class TrackSelectionService
    {
        private const string DisabledSubtitleTrack = "-1";

        private readonly List<TrackInfo> _subtitleTracks = new List<TrackInfo>();
        private string _lastSubtitleTrackId = DisabledSubtitleTrack;

        /// <summary>
        /// 更新 VLC 回调提供的字幕轨列表，供后续字幕开关恢复使用。
        /// </summary>
        public void UpdateSubtitleTracks(List<TrackInfo> tracks)
        {
            _subtitleTracks.Clear();
            if (tracks != null)
                _subtitleTracks.AddRange(tracks);
        }

        /// <summary>
        /// 切换媒体时重置快捷字幕状态，并记录媒体当前字幕轨。
        /// </summary>
        public void Reset(MediaWrapper media)
        {
            _lastSubtitleTrackId = media?.SpuTrack ?? DisabledSubtitleTrack;
        }

        /// <summary>
        /// 关闭字幕前记录当前轨道，便于下一次切换恢复。
        /// </summary>
        public void RememberSubtitleTrack(string trackId)
        {
            if (!string.IsNullOrEmpty(trackId) && trackId != DisabledSubtitleTrack)
                _lastSubtitleTrackId = trackId;
        }

        /// <summary>
        /// 优先恢复关闭前的字幕轨；不可用时选择第一个有效字幕轨。
        /// </summary>
        public string FindRestorableSubtitleTrack()
        {
            if (HasSubtitleTrack(_lastSubtitleTrackId))
                return _lastSubtitleTrackId;

            for (int i = 0; i < _subtitleTracks.Count; i++)
            {
                if (_subtitleTracks[i].Id != DisabledSubtitleTrack)
                    return _subtitleTracks[i].Id;
            }

            return DisabledSubtitleTrack;
        }

        public bool TryFindInitialSubtitleTrack(
            MediaWrapper media,
            SubtitleRenderMode renderMode,
            bool alreadyApplied,
            out string trackId)
        {
            trackId = DisabledSubtitleTrack;
            return false;
        }

        private bool HasSubtitleTrack(string trackId)
        {
            if (string.IsNullOrEmpty(trackId) || trackId == DisabledSubtitleTrack)
                return false;

            for (int i = 0; i < _subtitleTracks.Count; i++)
            {
                if (_subtitleTracks[i].Id == trackId)
                    return true;
            }

            return false;
        }
    }
}
