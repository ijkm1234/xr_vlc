using UnityEngine;

namespace XRVLC.UI.XR
{
    public interface IXrUiEventConsumer
    {
        bool ConsumeXrUiEvent(XrUiEvent evt, GameObject target);
    }
}
