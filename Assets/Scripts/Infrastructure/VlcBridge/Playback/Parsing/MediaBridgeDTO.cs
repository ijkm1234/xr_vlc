using System;
using System.Collections.Generic;
using XRVLC.Media;

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
        /// <summary>媒体标题，缺省时由解析器回退到默认文案。</summary>
        public string title;
        /// <summary>Android 侧传入的起播时间，单位毫秒。</summary>
        public long time;
        /// <summary>是否要求从头播放。</summary>
        public bool fromStart;
        /// <summary>媒体在 VLC 播放列表中的位置。</summary>
        public int positionInList;
        /// <summary>外挂字幕等附属资源列表。</summary>
        public List<SlaveDTO> slaves;
        /// <summary>Android/libvlc 识别出的投影类型。</summary>
        public string projection;
        /// <summary>Android/libvlc 识别出的左右或上下分屏提示。</summary>
        public string stereo;
    }
}
