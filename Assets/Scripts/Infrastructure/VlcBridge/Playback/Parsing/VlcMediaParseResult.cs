using System;

[Serializable]
public class VlcMediaParseResult
{
    /// <summary>解析出的原始视频宽度。</summary>
    public int width;
    /// <summary>解析出的原始视频高度。</summary>
    public int height;
    /// <summary>解析出的投影类型，取值约定为 flat、360 或 180。</summary>
    public string projection; // "flat"|"360"|"180"
    /// <summary>解析出的媒体总时长，单位毫秒。</summary>
    public long duration;
}
