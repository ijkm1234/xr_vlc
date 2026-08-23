using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("XRVLC/UI/XR Themed Button")]
public sealed class XrThemedButton : MonoBehaviour
{
    private const string DefaultIconResourcePath = "UI/IconPark/";

    [Header("Theme")]
    public XrButtonTheme theme;
    public Button button;
    public Image background;
    public Image icon;

    [Header("Editor Configuration")]
    public bool hoverEnabled = true;
    public bool selected;
    public bool hideLegacyText = true;
    public string iconName;
    public float iconSize = 32f;
    public string iconResourcePath = DefaultIconResourcePath;

    public void ApplyTheme()
    {
        ResolveReferences();

        if (theme != null)
            theme.ApplyTo(button, background, selected, hoverEnabled);

        ApplyIcon();
        if (hideLegacyText && icon != null)
            HideLegacyText(icon.transform);
    }

    public void SetSelected(bool value)
    {
        selected = value;
        if (theme != null)
            theme.ApplyTo(button, background, selected, hoverEnabled);
    }

    public void SetIconName(string value)
    {
        iconName = value;
        ResolveReferences();
        ApplyIcon();
        if (hideLegacyText && icon != null)
            HideLegacyText(icon.transform);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveReferences();
        ApplyTheme();
    }

    private void OnValidate()
    {
        ResolveReferences();
        ApplyTheme();
    }
#endif

    private void ResolveReferences()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (background == null)
            background = GetComponent<Image>();
        if (icon == null)
        {
            Transform existingIcon = transform.Find("Icon");
            if (existingIcon != null)
                icon = existingIcon.GetComponent<Image>();
        }
    }

    private void ApplyIcon()
    {
        if (icon == null)
            return;

        icon.raycastTarget = false;
        icon.preserveAspect = true;

        RectTransform iconRect = icon.GetComponent<RectTransform>();
        if (iconRect != null)
        {
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
        }

        if (string.IsNullOrWhiteSpace(iconName))
        {
            icon.sprite = null;
            return;
        }

        string safePath = string.IsNullOrWhiteSpace(iconResourcePath) ? DefaultIconResourcePath : iconResourcePath;
        icon.sprite = Resources.Load<Sprite>(safePath + iconName);
    }

    private void HideLegacyText(Transform iconTransform)
    {
        TextMeshProUGUI[] labels = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI label in labels)
        {
            if (label.transform == iconTransform)
                continue;

            label.enabled = false;
        }
    }
}
