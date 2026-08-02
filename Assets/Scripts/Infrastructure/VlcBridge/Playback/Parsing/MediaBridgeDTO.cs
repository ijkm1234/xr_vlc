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
        /// <summary>来源提供的媒体展示名称。</summary>
        public string title;
        /// <summary>媒体在 VLC 播放列表中的位置。</summary>
        public int index;
        /// <summary>播放请求来源；外部系统关联入口使用 external。</summary>
        public string source;
        /// <summary>来源已确认的媒体类型；外部关联入口固定为 video。</summary>
        public string mediaType;
    }
}
