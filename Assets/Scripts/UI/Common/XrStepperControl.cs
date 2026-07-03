using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("XRVLC/UI/XR Stepper Control")]
public sealed class XrStepperControl : MonoBehaviour
{
    public Button decrementButton;
    public Button incrementButton;
    public TMP_InputField inputField;
    public TextMeshProUGUI valueText;
    public TextMeshProUGUI placeholderText;
    public Image inputBackground;

    [Header("Layout")]
    public float buttonWidth = 58f;
    public float inputWidth = 132f;
    public float height = 46f;
    public float spacing = 8f;

    public TMP_InputField InputField
    {
        get
        {
            EnsureHierarchy();
            return inputField;
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        EnsureHierarchy();
    }
#endif

    public void Configure(
        string initialValue,
        float fontSize,
        UnityAction onDecrement,
        UnityAction onIncrement,
        UnityAction<string> onSubmit)
    {
        EnsureHierarchy();
        ApplyStyle(fontSize, Color.white, new Color(1f, 1f, 1f, 0.12f));

        SetValueWithoutNotify(initialValue);

        decrementButton.onClick.RemoveAllListeners();
        incrementButton.onClick.RemoveAllListeners();
        inputField.onSubmit.RemoveAllListeners();
        inputField.onEndEdit.RemoveAllListeners();

        if (onDecrement != null)
            decrementButton.onClick.AddListener(onDecrement);
        if (onIncrement != null)
            incrementButton.onClick.AddListener(onIncrement);
        if (onSubmit != null)
        {
            inputField.onSubmit.AddListener(onSubmit);
            inputField.onEndEdit.AddListener(onSubmit);
        }
    }

    public void ApplyStyle(float fontSize, Color textColor, Color inputBackgroundColor)
    {
        EnsureHierarchy();

        ApplyText(valueText, fontSize, textColor, TextAlignmentOptions.Center);
        ApplyText(placeholderText, fontSize, new Color(textColor.r, textColor.g, textColor.b, 0.45f), TextAlignmentOptions.Center);

        TextMeshProUGUI decrementLabel = decrementButton.GetComponentInChildren<TextMeshProUGUI>(true);
        TextMeshProUGUI incrementLabel = incrementButton.GetComponentInChildren<TextMeshProUGUI>(true);
        ApplyText(decrementLabel, fontSize, textColor, TextAlignmentOptions.Center);
        ApplyText(incrementLabel, fontSize, textColor, TextAlignmentOptions.Center);

        inputBackground.color = inputBackgroundColor;
        inputBackground.raycastTarget = true;

        inputField.lineType = TMP_InputField.LineType.SingleLine;
        ConfigureNumericKeyboard(inputField);
        inputField.textComponent = valueText;
        inputField.placeholder = placeholderText;
    }

    public void SetValueWithoutNotify(string value)
    {
        EnsureHierarchy();
        inputField.SetTextWithoutNotify(value);
        if (placeholderText != null)
            placeholderText.text = value;
    }

    public void EnsureHierarchy()
    {
        RectTransform rootRect = AddOrGet<RectTransform>(gameObject);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(buttonWidth * 2f + inputWidth + spacing * 2f, height);

        HorizontalLayoutGroup layout = AddOrGet<HorizontalLayoutGroup>(gameObject);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        LayoutElement rootElement = AddOrGet<LayoutElement>(gameObject);
        rootElement.preferredWidth = rootRect.sizeDelta.x;
        rootElement.preferredHeight = height;
        rootElement.flexibleWidth = 0f;
        rootElement.flexibleHeight = 0f;

        decrementButton = EnsureButton("DecrementButton", "◀", decrementButton);
        inputField = EnsureInputField("ValueInput");
        incrementButton = EnsureButton("IncrementButton", "▶", incrementButton);
    }

    private Button EnsureButton(string name, string label, Button existing)
    {
        GameObject item = existing != null ? existing.gameObject : FindOrCreateChild(name);
        item.name = name;

        AddOrGet<CanvasRenderer>(item);
        Image image = AddOrGet<Image>(item);
        Button button = AddOrGet<Button>(item);
        LayoutElement element = AddOrGet<LayoutElement>(item);
        RectTransform rect = AddOrGet<RectTransform>(item);

        rect.sizeDelta = new Vector2(buttonWidth, height);
        element.preferredWidth = buttonWidth;
        element.preferredHeight = height;
        element.flexibleWidth = 0f;
        element.flexibleHeight = 0f;

        image.color = Color.white;
        button.targetGraphic = image;

        TextMeshProUGUI labelText = EnsureText(item.transform, "Label", label);
        Stretch(labelText.rectTransform, new Vector2(8f, 0f), new Vector2(-8f, 0f));
        return button;
    }

    private TMP_InputField EnsureInputField(string name)
    {
        GameObject item = inputField != null ? inputField.gameObject : FindOrCreateChild(name);
        item.name = name;

        AddOrGet<CanvasRenderer>(item);
        inputBackground = AddOrGet<Image>(item);
        TMP_InputField field = AddOrGet<TMP_InputField>(item);
        LayoutElement element = AddOrGet<LayoutElement>(item);
        RectTransform rect = AddOrGet<RectTransform>(item);

        rect.sizeDelta = new Vector2(inputWidth, height);
        element.preferredWidth = inputWidth;
        element.preferredHeight = height;
        element.flexibleWidth = 0f;
        element.flexibleHeight = 0f;

        valueText = EnsureText(item.transform, "Value", field.text);
        placeholderText = EnsureText(item.transform, "Placeholder", field.text);
        Stretch(valueText.rectTransform, new Vector2(8f, 0f), new Vector2(-8f, 0f));
        Stretch(placeholderText.rectTransform, new Vector2(8f, 0f), new Vector2(-8f, 0f));

        field.textComponent = valueText;
        field.placeholder = placeholderText;
        ConfigureNumericKeyboard(field);
        return field;
    }

    private static void ConfigureNumericKeyboard(TMP_InputField field)
    {
        if (field == null)
            return;

        field.contentType = TMP_InputField.ContentType.Custom;
        field.inputType = TMP_InputField.InputType.Standard;
        field.keyboardType = TouchScreenKeyboardType.EmailAddress;
        field.characterValidation = TMP_InputField.CharacterValidation.None;
    }

    private TextMeshProUGUI EnsureText(Transform parent, string name, string value)
    {
        Transform existing = parent.Find(name);
        GameObject item = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));

        item.transform.SetParent(parent, false);
        TextMeshProUGUI text = AddOrGet<TextMeshProUGUI>(item);
        text.text = value;
        text.enableWordWrapping = false;
        return text;
    }

    private GameObject FindOrCreateChild(string name)
    {
        Transform existing = transform.Find(name);
        if (existing != null)
            return existing.gameObject;

        GameObject item = new GameObject(name, typeof(RectTransform));
        item.transform.SetParent(transform, false);
        return item;
    }

    private static void ApplyText(TextMeshProUGUI text, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        if (text == null)
            return;

        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = false;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static T AddOrGet<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }
}
