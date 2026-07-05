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

    public static TrackSnapshot ParseTrackSnapshot(string data)
    {
        var snapshot = new TrackSnapshot();
        if (string.IsNullOrEmpty(data)) return snapshot;

        try
        {
            TrackSnapshotPayload payload = JsonUtility.FromJson<TrackSnapshotPayload>(data);
            AddTracks(snapshot.AudioTracks, payload?.audioTracks);
            AddTracks(snapshot.SubtitleTracks, payload?.subtitleTracks);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VlcPlaybackPayloadParser] Failed to parse track snapshot JSON payload: {ex.Message}");
        }

        return snapshot;
    }

    public static List<PlaylistItemData> ParsePlaylist(string json)
    {
        var result = new List<PlaylistItemData>();
        if (string.IsNullOrWhiteSpace(json))
            return result;

        string trimmed = json.Trim();
        if (trimmed == "[]")
            return result;

        try
        {
            var wrapper = JsonUtility.FromJson<PlaylistJsonWrapper>("{\"items\":" + trimmed + "}");
            if (wrapper?.items != null)
                result.AddRange(wrapper.items);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VlcPlaybackPayloadParser] Failed to parse playlist JSON payload: {ex.Message}");
        }

        return result;
    }

    private static List<TrackInfo> ParseTracksJsonData(string data)
    {
        var list = new List<TrackInfo>();
        try
        {
            TrackListPayload payload = JsonUtility.FromJson<TrackListPayload>(data);
            AddTracks(list, payload?.tracks);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VlcPlaybackPayloadParser] Failed to parse track JSON payload: {ex.Message}");
        }

        return list;
    }

    private static void AddTracks(List<TrackInfo> target, TrackPayload[] tracks)
    {
        if (target == null || tracks == null) return;

        foreach (TrackPayload track in tracks)
        {
            if (track == null || string.IsNullOrEmpty(track.id)) continue;
            target.Add(new TrackInfo
            {
                Id = track.id,
                Name = track.name,
                IsSelected = track.selected,
                Slave = track.slave
            });
        }
    }

    [Serializable]
    private class TrackListPayload
    {
        public TrackPayload[] tracks;
    }

    [Serializable]
    private class TrackSnapshotPayload
    {
        public TrackPayload[] audioTracks;
        public TrackPayload[] subtitleTracks;
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
    public static MediaWrapper ParseStartPlayPayload(string jsonPayload)
    {
        if (string.IsNullOrEmpty(jsonPayload)) return null;

        XRVLC.Bridge.MediaBridgeDTO dto = JsonUtility.FromJson<XRVLC.Bridge.MediaBridgeDTO>(jsonPayload);
        if (dto == null || string.IsNullOrEmpty(dto.uri)) return null;

        string normalizedUri = DecodeBridgeUri(dto.uri);
        dto.uri = normalizedUri;
        string title = BuildTitleFromUri(normalizedUri);

        return new MediaWrapper
        {
            Id = normalizedUri,
            Uri = normalizedUri,
            Title = title,
            Time = 0,
            FromStart = false,
            PositionInList = dto.index,
            Slaves = new List<SlaveDTO>(),
            Projection = MediaProjectionType.Flat2D,
            StereoHint = null,
            RawJson = jsonPayload
        };
    }

    private static string BuildTitleFromUri(string uri)
    {
        if (string.IsNullOrEmpty(uri)) return "未知视频";

        int slash = Math.Max(uri.LastIndexOf('/'), uri.LastIndexOf('\\'));
        string title = slash >= 0 && slash + 1 < uri.Length ? uri.Substring(slash + 1) : uri;
        return string.IsNullOrEmpty(title) ? "未知视频" : title;
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

    private static MediaProjectionType ParseProjectionType(string value) => value switch
    {
        "360" => MediaProjectionType.Sphere360,
        "180" => MediaProjectionType.Sphere180,
        _ => MediaProjectionType.Flat2D
    };
}
