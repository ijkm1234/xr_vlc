using UnityEngine;

namespace XRVLC
{
    public readonly struct ChromaKeySettings
    {
        private static readonly Color DefaultKeyColor = new Color32(0x2B, 0xE6, 0x40, 0xFF);
        public const float ColorRangeMax = 0.25f;
        public const float EdgeSmoothMax = 0.25f;
        public const float DespillStrengthMax = 0.1f;

        public ChromaKeySettings(
            bool enabled,
            Color keyColor,
            float colorRange,
            float edgeSmooth,
            float despillStrength = 0f)
        {
            Enabled = enabled;
            KeyColor = new Color(keyColor.r, keyColor.g, keyColor.b, 1f);
            ColorRange = Mathf.Clamp(colorRange, 0f, ColorRangeMax);
            EdgeSmooth = Mathf.Clamp(edgeSmooth, 0f, EdgeSmoothMax);
            DespillStrength = Mathf.Clamp(despillStrength, 0f, DespillStrengthMax);
        }

        public static ChromaKeySettings Default =>
            new ChromaKeySettings(
                false,
                DefaultKeyColor,
                ColorRangeMax * 0.5f,
                EdgeSmoothMax * 0.5f,
                DespillStrengthMax * 0.5f);

        public bool Enabled { get; }
        public Color KeyColor { get; }
        public float ColorRange { get; }
        public float EdgeSmooth { get; }
        public float DespillStrength { get; }

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

        public ChromaKeySettings WithEdgeSmooth(float edgeSmooth)
        {
            return Copy(edgeSmooth: edgeSmooth);
        }

        public ChromaKeySettings WithDespillStrength(float despillStrength)
        {
            return Copy(despillStrength: despillStrength);
        }

        private ChromaKeySettings Copy(
            bool? enabled = null,
            Color? keyColor = null,
            float? colorRange = null,
            float? edgeSmooth = null,
            float? despillStrength = null)
        {
            return new ChromaKeySettings(
                enabled ?? Enabled,
                keyColor ?? KeyColor,
                colorRange ?? ColorRange,
                edgeSmooth ?? EdgeSmooth,
                despillStrength ?? DespillStrength);
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
        public static float CalculateYcgcoDistance(Color color, Color keyColor)
        {
            Vector2 chroma = ToYcgcoChroma(color);
            Vector2 keyChroma = ToYcgcoChroma(keyColor);
            return Mathf.Clamp01(Vector2.Distance(chroma, keyChroma));
        }

        public static float CalculateAlpha(float distance, float colorRange, float edgeSmooth)
        {
            float range = Mathf.Clamp(colorRange, 0f, ChromaKeySettings.ColorRangeMax);
            float transition = Mathf.Clamp(edgeSmooth, 0f, ChromaKeySettings.EdgeSmoothMax);
            if (distance <= range)
                return 0f;
            if (transition <= Mathf.Epsilon || distance >= range + transition)
                return 1f;

            float t = Mathf.Clamp01((distance - range) / transition);
            return t * t * (3f - 2f * t);
        }

        private static Vector2 ToYcgcoChroma(Color color)
        {
            float cg = -0.25f * color.r + 0.5f * color.g - 0.25f * color.b;
            float co = 0.5f * color.r - 0.5f * color.b;
            return new Vector2(cg, co);
        }
    }
}
