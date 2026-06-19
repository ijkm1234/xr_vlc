using System;
using XRVLC.Media;

namespace XRVLC
{
    /// <summary>
    /// 两级优先级检测视频投影和立体模式：
    ///   1. MediaWrapper 显式元数据（Android libvlc 传入）
    ///   2. URI/文件名关键字启发式
    /// </summary>
    public static class ProjectionDetector
    {
        public static (VideoProjection projection, StereoMode stereo) Detect(MediaWrapper media)
        {
            // Priority 1: explicit metadata from Android
            if (media != null && media.Projection != MediaProjectionType.Flat2D)
            {
                var proj = MapProjection(media.Projection);
                var st   = ParseStereoHint(media.StereoHint)
                           ?? DetectStereoFromUri(media.Uri)
                           ?? StereoMode.Mono;
                return (proj, st);
            }

            // Priority 2: URI/filename keywords
            string uri = media?.Uri ?? string.Empty;
            var uriResult = DetectFromUri(uri);
            if (uriResult.confident)
                return (uriResult.proj, uriResult.stereo);

            return (VideoProjection.Flat, uriResult.stereo);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static VideoProjection MapProjection(MediaProjectionType t) => t switch
        {
            MediaProjectionType.Sphere360 => VideoProjection.Sphere360,
            MediaProjectionType.Sphere180 => VideoProjection.Sphere180,
            _                             => VideoProjection.Flat
        };

        private static StereoMode? ParseStereoHint(string hint)
        {
            if (string.IsNullOrEmpty(hint)) return null;
            return hint.ToLowerInvariant() switch
            {
                "lr"   => StereoMode.LeftRight,
                "tb"   => StereoMode.TopBottom,
                "mono" => StereoMode.Mono,
                _      => null
            };
        }

        private static StereoMode? DetectStereoFromUri(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return null;
            string name = FilenameLower(uri);

            if (ContainsToken(name, "sbs") ||
                ContainsToken(name, "lr")  ||
                ContainsToken(name, "left_right") ||
                ContainsToken(name, "side"))
                return StereoMode.LeftRight;

            if (ContainsToken(name, "tb")  ||
                ContainsToken(name, "ou")  ||
                ContainsToken(name, "top_bottom") ||
                ContainsToken(name, "overunder"))
                return StereoMode.TopBottom;

            return null;
        }

        private static (VideoProjection proj, StereoMode stereo, bool confident) DetectFromUri(string uri)
        {
            if (string.IsNullOrEmpty(uri))
                return (VideoProjection.Flat, StereoMode.Mono, false);

            string name = FilenameLower(uri);

            VideoProjection? proj = null;
            if (name.Contains("equirect") || name.Contains("vr360") || name.Contains("360vr") ||
                name.Contains("3d360") || ContainsToken(name, "360"))
                proj = VideoProjection.Sphere360;
            else if (name.Contains("fisheye") || name.Contains("vr180") || name.Contains("180vr") ||
                     name.Contains("3d180") || ContainsToken(name, "180"))
                proj = VideoProjection.Sphere180;
            else if (name.Contains("cylinder") || ContainsToken(name, "cyl"))
                proj = VideoProjection.Cylinder;

            StereoMode stereo = DetectStereoFromUri(uri) ?? StereoMode.Mono;

            return (proj ?? VideoProjection.Flat, stereo, proj.HasValue);
        }

        private static string FilenameLower(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return string.Empty;
            int slash = Math.Max(uri.LastIndexOf('/'), uri.LastIndexOf('\\'));
            return (slash >= 0 ? uri.Substring(slash + 1) : uri).ToLowerInvariant();
        }

        private static bool ContainsToken(string name, string token)
        {
            int idx = name.IndexOf(token, StringComparison.Ordinal);
            if (idx < 0) return false;
            bool leftOk  = idx == 0 || !char.IsLetterOrDigit(name[idx - 1]);
            bool rightOk = idx + token.Length >= name.Length
                           || !char.IsLetterOrDigit(name[idx + token.Length]);
            return leftOk && rightOk;
        }
    }
}
