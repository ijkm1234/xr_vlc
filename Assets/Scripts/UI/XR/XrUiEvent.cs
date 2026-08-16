using UnityEngine;
using UnityEngine.XR;

namespace XRVLC.UI.XR
{
    public enum XrUiEventType
    {
        TriggerPressed,
        TriggerReleased,
        PrimaryButtonPressed,
        SecondaryButtonPressed,
        StickClicked,
        GripPressed,
        StickAxisChanged
    }

    public readonly struct XrUiEvent
    {
        public XrUiEvent(XrUiEventType type, XRNode hand = XRNode.RightHand, Vector2 axis = default)
        {
            Type = type;
            Hand = hand;
            Axis = axis;
        }

        public XrUiEventType Type { get; }
        public XRNode Hand { get; }
        public Vector2 Axis { get; }

        public bool IsTrigger =>
            Type == XrUiEventType.TriggerPressed ||
            Type == XrUiEventType.TriggerReleased;
    }
}
