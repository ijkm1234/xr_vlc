using System;
using System.Collections.Generic;
using UnityEngine;
using XRVLC.Media;

public static class VlcPlaybackPayloadParser
{
    /// <summary>
    /// 解析 VLC 回调的轨道载荷；音轨兼容 id:name|id:name，字幕优先使用带 slave 的 JSON。
    /// </summary>
    public static List<TrackInfo> ParseTracksData(string data)
    {
        var list = new List<TrackInfo>();
        if (string.IsNullOrEmpty(data)) return list;

        string trimmedData = data.Trim();
        if (trimmedData.StartsWith("{", StringComparison.Ordinal))
            return ParseTracksJsonData(trimmedData);

        string[] parts = data.Split('|');
        foreach (string part in parts)
        {
            int colonIndex = part.IndexOf(':');
            if (colonIndex <= 0) continue;

            string id = part.Substring(0, colonIndex);
            bool isSelected = id.StartsWith("*", StringComparison.Ordinal);
            if (isSelected)
                id = id.Substring(1);

            list.Add(new TrackInfo
            {
                Id = id,
                Name = part.Substring(colonIndex + 1),
                IsSelected = isSelected
            });
        }

        return list;
    }

    private static List<TrackInfo> ParseTracksJsonData(string data)
    {
        var list = new List<TrackInfo>();
        try
        {
            TrackListPayload payload = JsonUtility.FromJson<TrackListPayload>(data);
            if (payload?.tracks == null) return list;

            foreach (TrackPayload track in payload.tracks)
            {
                if (track == null || string.IsNullOrEmpty(track.id)) continue;
                list.Add(new TrackInfo
                {
                    Id = track.id,
                    Name = track.name,
                    IsSelected = track.selected,
                    Slave = track.slave
                });
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VlcPlaybackPayloadParser] Failed to parse track JSON payload: {ex.Message}");
        }

        return list;
    }

    [Serializable]
    private class TrackListPayload
    {
        public TrackPayload[] tracks;
    }

    [Serializable]
    private class TrackPayload
    {
        public string id;
        public string name;
        public bool selected;
        public SlaveDTO slave;
    }

    /// <summary>
    /// 解析 Android 侧 StartPlay JSON，并构建 Unity 播放领域模型。
    /// </summary>
    public static MediaWrapper ParseStartPlayPayload(string jsonPayload, Func<string, long> lastTimeProvider)
    {
        if (string.IsNullOrEmpty(jsonPayload)) return null;

        XRVLC.Bridge.MediaBridgeDTO dto = JsonUtility.FromJson<XRVLC.Bridge.MediaBridgeDTO>(jsonPayload);
        if (dto == null || string.IsNullOrEmpty(dto.uri)) return null;

        string normalizedUri = DecodeBridgeUri(dto.uri);
        dto.uri = normalizedUri;
        string title = string.IsNullOrEmpty(dto.title) ? "未知视频" : dto.title;
        long lastTime = dto.time > 0 ? dto.time : lastTimeProvider?.Invoke(normalizedUri) ?? 0;

        return new MediaWrapper
        {
            Id = normalizedUri,
            Uri = normalizedUri,
            Title = title,
            Time = lastTime,
            FromStart = dto.fromStart,
            PositionInList = dto.positionInList,
            Slaves = dto.slaves ?? new List<SlaveDTO>(),
            Projection = ParseProjectionType(dto.projection),
            StereoHint = dto.stereo,
            RawJson = jsonPayload
        };
    }

    private static string DecodeBridgeUri(string uri)
    {
        if (string.IsNullOrEmpty(uri)) return uri;

        try
        {
            return Uri.UnescapeDataString(uri);
        }
        catch (UriFormatException)
        {
            return uri;
        }
    }

    /// <summary>
    /// 解析媒体预加载结果 JSON，供 geometry service 后续重建视频层。
    /// </summary>
    public static VlcMediaParseResult ParseMediaParseResult(string json)
    {
        return JsonUtility.FromJson<VlcMediaParseResult>(json);
    }

    /// <summary>
    /// 解析 Android/native 侧截流出来的播放时字幕 cue。
    /// </summary>
    public static SubtitleCue ParseSubtitleCuePayload(string json)
    {
        if (string.IsNullOrEmpty(json)) return SubtitleCue.Clear();

        try
        {
            SubtitleCue cue = JsonUtility.FromJson<SubtitleCue>(json);
            if (cue == null) return SubtitleCue.Clear();

            cue.Normalize();
            return cue;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VlcPlaybackPayloadParser] ParseSubtitleCuePayload failed: {e.Message}");
            return SubtitleCue.Clear();
        }
    }

    private static MediaProjectionType ParseProjectionType(string value) => value switch
    {
        "360" => MediaProjectionType.Sphere360,
        "180" => MediaProjectionType.Sphere180,
        _ => MediaProjectionType.Flat2D
    };
}
