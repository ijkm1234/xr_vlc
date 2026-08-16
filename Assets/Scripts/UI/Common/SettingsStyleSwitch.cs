using UnityEngine;

public sealed class SettingsStyleSwitch : MonoBehaviour
{
    [SerializeField] private RoundedRectImage track;
    [SerializeField] private RectTransform knob;
    [SerializeField] private Color onColor = new Color(1f, 0.53333336f, 0f, 1f);
    [SerializeField] private Color offTrackColor = new Color(1f, 1f, 1f, 0.16f);
    [SerializeField] private Color offBorderColor = new Color(1f, 1f, 1f, 0.22f);
    [SerializeField] private float onPosition = 23f;
    [SerializeField] private float offPosition = -23f;

    public void SetState(bool enabled)
    {
        if (track != null)
        {
            track.color = enabled ? onColor : offTrackColor;
            track.borderColor = enabled ? onColor : offBorderColor;
            track.SetVerticesDirty();
        }

        if (knob != null)
            knob.anchoredPosition = new Vector2(enabled ? onPosition : offPosition, 0f);
    }
}
