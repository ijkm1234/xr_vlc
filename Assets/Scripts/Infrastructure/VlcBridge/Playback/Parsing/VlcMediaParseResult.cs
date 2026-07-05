using System;

[Serializable]
public class VlcMediaParseResult
{
    /// <summary>解析结果所属媒体 URI。</summary>
    public string uri;
    /// <summary>解析出的原始视频宽度。</summary>
    public int width;
    /// <summary>解析出的原始视频高度。</summary>
    public int height;
    /// <summary>解析出的可见内容宽度；缺失时使用原始宽度。</summary>
    public int visibleWidth;
    /// <summary>解析出的可见内容高度；缺失时使用原始高度。</summary>
    public int visibleHeight;
    /// <summary>解析出的投影类型，取值约定为 flat、360 或 180。</summary>
    public string projection; // "flat"|"360"|"180"
    /// <summary>解析出的媒体总时长，单位毫秒。</summary>
    public long duration;

    public int ContentWidth => visibleWidth > 0 ? visibleWidth : width;
    public int ContentHeight => visibleHeight > 0 ? visibleHeight : height;

    public VlcVideoSize ToVideoSize()
    {
        return new VlcVideoSize(width, height, visibleWidth, visibleHeight);
    }
}

public readonly struct VlcVideoSize
{
    public VlcVideoSize(int width, int height, int visibleWidth = 0, int visibleHeight = 0)
    {
        Width = width;
        Height = height;
        VisibleWidth = visibleWidth;
        VisibleHeight = visibleHeight;
    }

    public int Width { get; }
    public int Height { get; }
    public int VisibleWidth { get; }
    public int VisibleHeight { get; }
    public int ContentWidth => VisibleWidth > 0 ? VisibleWidth : Width;
    public int ContentHeight => VisibleHeight > 0 ? VisibleHeight : Height;
    public bool IsValid => Width > 0 && Height > 0 && ContentWidth > 0 && ContentHeight > 0;

    public bool HasSameDimensions(VlcVideoSize other)
    {
        return Width == other.Width
            && Height == other.Height
            && VisibleWidth == other.VisibleWidth
            && VisibleHeight == other.VisibleHeight;
    }
}
