using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "XrButtonTheme", menuName = "XRVLC/UI/XR Button Theme")]
public sealed class XrButtonTheme : ScriptableObject
{
    private static readonly Color HoverListColor = new Color(1f, 1f, 1f, 0.16f);
    private static readonly Color SelectedListColor = new Color(1f, 1f, 1f, 0.24f);
    private static readonly Color SelectedListHoverColor = new Color(1f, 1f, 1f, 0.32f);

    [Header("Button States")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private Color hoverColor = HoverListColor;
    [SerializeField] private Color selectedColor = SelectedListColor;
    [SerializeField] private Color selectedHoverColor = SelectedListHoverColor;
    [SerializeField] private Color pressedColor = SelectedListColor;
    [SerializeField] private Color disabledColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private float fadeDuration = 0.08f;
    [SerializeField] private float colorMultiplier = 1f;

    [Header("Background Graphic")]
    [SerializeField] private Color backgroundBaseColor = Color.white;
    [SerializeField] private bool backgroundRaycastTarget = true;

    public Color NormalColor => normalColor;
    public Color HoverColor => hoverColor;
    public Color SelectedColor => selectedColor;
    public Color SelectedHoverColor => selectedHoverColor;
    public Color PressedColor => pressedColor;
    public Color DisabledColor => disabledColor;
    public Color BackgroundBaseColor => backgroundBaseColor;

    public void ApplyTo(Button button, Image background, bool selected, bool hoverEnabled)
    {
        if (button == null)
            return;

        ColorBlock colors = button.colors;
        colors.normalColor = ResolveNormalColor(selected);
        colors.highlightedColor = ResolveHighlightedColor(selected, hoverEnabled);
        colors.selectedColor = ResolveNormalColor(selected);
        colors.pressedColor = pressedColor;
        colors.disabledColor = disabledColor;
        colors.colorMultiplier = colorMultiplier;
        colors.fadeDuration = fadeDuration;

        button.transition = Selectable.Transition.ColorTint;
        button.colors = colors;

        if (background == null)
            return;

        background.color = backgroundBaseColor;
        background.raycastTarget = backgroundRaycastTarget;
        button.targetGraphic = background;
    }

    public Color ResolveNormalColor(bool selected)
    {
        return selected ? selectedColor : normalColor;
    }

    public Color ResolveHighlightedColor(bool selected, bool hoverEnabled)
    {
        if (!hoverEnabled)
            return ResolveNormalColor(selected);

        return selected ? selectedHoverColor : hoverColor;
    }
}
