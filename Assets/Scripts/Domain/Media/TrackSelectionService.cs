using System.Collections.Generic;
using XRVLC.Media;

namespace XRVLC.Media
{
    public class TrackSelectionService
    {
        private const string DisabledSubtitleTrack = "-1";

        private string _lastSubtitleTrackId = DisabledSubtitleTrack;

        /// <summary>
        /// 兼容旧调用路径；Unity 不再缓存字幕轨列表，轨道可用性由调用方传入的 VLC 快照决定。
        /// </summary>
        public void UpdateSubtitleTracks(List<TrackInfo> tracks)
        {
        }

        /// <summary>
        /// 切换媒体时重置快捷字幕状态；当前轨道以 VLC 主动查询结果为准。
        /// </summary>
        public void Reset(MediaWrapper media)
        {
            _lastSubtitleTrackId = DisabledSubtitleTrack;
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
        public string FindRestorableSubtitleTrack(List<TrackInfo> tracks)
        {
            if (HasSubtitleTrack(_lastSubtitleTrackId, tracks))
                return _lastSubtitleTrackId;

            if (tracks == null)
                return DisabledSubtitleTrack;

            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].Id != DisabledSubtitleTrack)
                    return tracks[i].Id;
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

        private bool HasSubtitleTrack(string trackId, List<TrackInfo> tracks)
        {
            if (string.IsNullOrEmpty(trackId) || trackId == DisabledSubtitleTrack)
                return false;

            if (tracks == null)
                return false;

            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].Id == trackId)
                    return true;
            }

            return false;
        }
    }
}
