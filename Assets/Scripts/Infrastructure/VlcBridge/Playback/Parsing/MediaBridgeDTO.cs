using System;

namespace XRVLC.Bridge
{
    /// <summary>
    /// Android 侧通过 UnitySendMessage 传入的媒体播放请求 DTO。
    /// </summary>
    [Serializable]
    public class MediaBridgeDTO
    {
        /// <summary>媒体 URI，作为播放和历史记录查询的主键。</summary>
        public string uri;
        /// <summary>媒体在 VLC 播放列表中的位置。</summary>
        public int index;
    }
}
