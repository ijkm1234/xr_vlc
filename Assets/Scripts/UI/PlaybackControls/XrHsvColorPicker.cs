using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(RawImage))]
public sealed class XrHsvColorPicker : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private const int TextureSize = 128;
    private const string IndicatorSpritePath = "UI/chroma-key-hex-indicator";
    private RawImage _image;
    private RectTransform _rect;
    private RectTransform _indicator;
    private Texture2D _texture;
    private float _hue;
    private float _saturation = 1f;
    private float _value = 1f;

    public event Action<Color> ColorChanged;

    public void Initialize()
    {
        _rect = GetComponent<RectTransform>();
        _image = GetComponent<RawImage>();
        _image.raycastTarget = true;

        var indicator = new GameObject("ColorIndicator", typeof(RectTransform), typeof(Image));
        indicator.transform.SetParent(transform, false);
        _indicator = indicator.GetComponent<RectTransform>();
        _indicator.pivot = new Vector2(0.5f, 0.5f);
        _indicator.sizeDelta = new Vector2(24f, 24f);
        Image image = indicator.GetComponent<Image>();
        image.sprite = Resources.Load<Sprite>(IndicatorSpritePath);
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;

        _texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGB24, false, true)
        {
            name = "ChromaKeySaturationValue",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        _image.texture = _texture;
        RebuildTexture();
        UpdateIndicator();
    }

    public void SetHue(float hue, bool notify)
    {
        _hue = Mathf.Repeat(hue, 1f);
        RebuildTexture();
        if (notify)
            ColorChanged?.Invoke(CurrentColor());
    }

    public void SetColorWithoutNotify(Color color)
    {
        Color.RGBToHSV(color, out _hue, out _saturation, out _value);
        RebuildTexture();
        UpdateIndicator();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        UpdateFromPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateFromPointer(eventData);
    }

    private void UpdateFromPointer(PointerEventData eventData)
    {
        if (_rect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 local))
            return;

        Rect bounds = _rect.rect;
        _saturation = Mathf.Clamp01((local.x - bounds.xMin) / Mathf.Max(1f, bounds.width));
        _value = Mathf.Clamp01((local.y - bounds.yMin) / Mathf.Max(1f, bounds.height));
        UpdateIndicator();
        ColorChanged?.Invoke(CurrentColor());
    }

    private Color CurrentColor()
    {
        return Color.HSVToRGB(_hue, _saturation, _value);
    }

    private void RebuildTexture()
    {
        if (_texture == null)
            return;

        var pixels = new Color32[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            float value = y / (TextureSize - 1f);
            for (int x = 0; x < TextureSize; x++)
            {
                float saturation = x / (TextureSize - 1f);
                pixels[y * TextureSize + x] = Color.HSVToRGB(_hue, saturation, value);
            }
        }

        _texture.SetPixels32(pixels);
        _texture.Apply(false, false);
    }

    private void UpdateIndicator()
    {
        if (_indicator == null || _rect == null)
            return;

        Vector2 position = new Vector2(_saturation, _value);
        _indicator.anchorMin = position;
        _indicator.anchorMax = position;
        _indicator.anchoredPosition = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (_texture != null)
            Destroy(_texture);
    }
}
