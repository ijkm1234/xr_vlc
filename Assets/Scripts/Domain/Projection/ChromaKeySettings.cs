using UnityEngine;

namespace XRVLC
{
    public readonly struct ChromaKeySettings
    {
        private static readonly Color DefaultKeyColor = new Color32(0x2B, 0xE6, 0x40, 0xFF);

        public ChromaKeySettings(bool enabled, Color keyColor, float colorRange, float falloff)
            : this(enabled, keyColor, colorRange, falloff, false, false, false, false)
        {
        }

        private ChromaKeySettings(
            bool enabled,
            Color keyColor,
            float colorRange,
            float falloff,
            bool edgeSmoothEnabled,
            bool clipBlackEnabled,
            bool clipWhiteEnabled,
            bool despillEnabled)
        {
            Enabled = enabled;
            KeyColor = new Color(keyColor.r, keyColor.g, keyColor.b, 1f);
            ColorRange = Mathf.Clamp01(colorRange);
            Falloff = Mathf.Clamp01(falloff);
            EdgeSmoothEnabled = edgeSmoothEnabled;
            ClipBlackEnabled = clipBlackEnabled;
            ClipWhiteEnabled = clipWhiteEnabled;
            DespillEnabled = despillEnabled;
        }

        public static ChromaKeySettings Default => new ChromaKeySettings(false, DefaultKeyColor, 0.2f, 0.1f);

        public bool Enabled { get; }
        public Color KeyColor { get; }
        public float ColorRange { get; }
        public float Falloff { get; }
        public bool EdgeSmoothEnabled { get; }
        public bool ClipBlackEnabled { get; }
        public bool ClipWhiteEnabled { get; }
        public bool DespillEnabled { get; }

        public ChromaKeySettings WithEnabled(bool enabled)
        {
            return Copy(enabled: enabled);
        }

        public ChromaKeySettings WithKeyColor(Color keyColor)
        {
            return Copy(keyColor: keyColor);
        }

        public ChromaKeySettings WithColorRange(float colorRange)
        {
            return Copy(colorRange: colorRange);
        }

        public ChromaKeySettings WithFalloff(float falloff)
        {
            return Copy(falloff: falloff);
        }

        public ChromaKeySettings WithEdgeSmoothEnabled(bool enabled)
        {
            return Copy(edgeSmoothEnabled: enabled);
        }

        public ChromaKeySettings WithClipBlackEnabled(bool enabled)
        {
            return Copy(clipBlackEnabled: enabled);
        }

        public ChromaKeySettings WithClipWhiteEnabled(bool enabled)
        {
            return Copy(clipWhiteEnabled: enabled);
        }

        public ChromaKeySettings WithDespillEnabled(bool enabled)
        {
            return Copy(despillEnabled: enabled);
        }

        private ChromaKeySettings Copy(
            bool? enabled = null,
            Color? keyColor = null,
            float? colorRange = null,
            float? falloff = null,
            bool? edgeSmoothEnabled = null,
            bool? clipBlackEnabled = null,
            bool? clipWhiteEnabled = null,
            bool? despillEnabled = null)
        {
            return new ChromaKeySettings(
                enabled ?? Enabled,
                keyColor ?? KeyColor,
                colorRange ?? ColorRange,
                falloff ?? Falloff,
                edgeSmoothEnabled ?? EdgeSmoothEnabled,
                clipBlackEnabled ?? ClipBlackEnabled,
                clipWhiteEnabled ?? ClipWhiteEnabled,
                despillEnabled ?? DespillEnabled);
        }

        public string ToHex()
        {
            Color32 color = KeyColor;
            return $"#{color.r:X2}{color.g:X2}{color.b:X2}";
        }

        public static bool TryParseHex(string value, out ChromaKeySettings settings)
        {
            settings = Default;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string normalized = value.Trim();
            if (!normalized.StartsWith("#"))
                normalized = "#" + normalized;
            if (normalized.Length != 7 || !ColorUtility.TryParseHtmlString(normalized, out Color color))
                return false;

            settings = settings.WithKeyColor(color);
            return true;
        }
    }

    public static class ChromaKeyMath
    {
        private const float ClipBlackPoint = 0.08f;
        private const float ClipWhitePoint = 0.92f;

        public static float CalculateYcgcoDistance(Color color, Color keyColor)
        {
            Vector2 chroma = ToYcgcoChroma(color);
            Vector2 keyChroma = ToYcgcoChroma(keyColor);
            return Mathf.Clamp01(Vector2.Distance(chroma, keyChroma));
        }

        public static float CalculateAlpha(float distance, float colorRange, float falloff)
        {
            float range = Mathf.Clamp01(colorRange);
            float transition = Mathf.Clamp01(falloff);
            if (distance <= range)
                return 0f;
            if (transition <= Mathf.Epsilon || distance >= range + transition)
                return 1f;

            float t = Mathf.Clamp01((distance - range) / transition);
            return t * t * (3f - 2f * t);
        }

        public static float ApplyClip(float alpha, bool clipBlackEnabled, bool clipWhiteEnabled)
        {
            float blackPoint = clipBlackEnabled ? ClipBlackPoint : 0f;
            float whitePoint = clipWhiteEnabled ? ClipWhitePoint : 1f;
            return Mathf.Clamp01((Mathf.Clamp01(alpha) - blackPoint) / (whitePoint - blackPoint));
        }

        private static Vector2 ToYcgcoChroma(Color color)
        {
            float cg = -0.25f * color.r + 0.5f * color.g - 0.25f * color.b;
            float co = 0.5f * color.r - 0.5f * color.b;
            return new Vector2(cg, co);
        }
    }
}
