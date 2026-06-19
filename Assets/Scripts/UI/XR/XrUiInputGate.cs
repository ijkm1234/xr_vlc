using UnityEngine;

namespace XRVLC.UI.XR
{
    public sealed class XrUiInputGate : MonoBehaviour
    {
        private XrUiRaycastTracker _raycastTracker;

        public UiTreeRouter Router { get; } = new UiTreeRouter();

        public bool IsHoveringUi
        {
            get
            {
                return TryGetCurrentUiTarget(out _);
            }
        }

        public bool IsHoveringBlockingUi
        {
            get
            {
                return TryGetCurrentUiTarget(out GameObject target) && Router.BlocksShortcuts(target);
            }
        }

        public bool IsHoveringAutoHideBlockingUi
        {
            get
            {
                return TryGetCurrentUiTarget(out GameObject target) && Router.BlocksAutoHide(target);
            }
        }

        private void Awake()
        {
            EnsureRaycastTracker();
        }

        public void RegisterNode(XrUiNode node)
        {
            Router.Register(node);
        }

        public void ClearNodes()
        {
            Router.Clear();
        }

        public bool TryGetCurrentUiTarget(out GameObject target)
        {
            EnsureRaycastTracker();
            return _raycastTracker.TryGetCurrentTarget(out target) && Router.IsPointerOverUi(target);
        }

        public bool TryConsumeCurrentHover(XrUiEvent evt)
        {
            return TryGetCurrentUiTarget(out GameObject target) && Router.TryConsume(target, evt);
        }

        private void EnsureRaycastTracker()
        {
            if (_raycastTracker != null)
                return;

            _raycastTracker = GetComponent<XrUiRaycastTracker>();
            if (_raycastTracker == null)
                _raycastTracker = gameObject.AddComponent<XrUiRaycastTracker>();
        }
    }
}
