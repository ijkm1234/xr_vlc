using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace XRVLC.UI.XR
{
    public sealed class XrUiRaycastTracker : MonoBehaviour
    {
        public bool TryGetCurrentTarget(out GameObject target)
        {
            target = null;

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IUIInteractor interactor &&
                    interactor.TryGetUIModel(out TrackedDeviceModel model) &&
                    model.currentRaycast.gameObject != null)
                {
                    target = model.currentRaycast.gameObject;
                    return true;
                }
            }

            return false;
        }
    }
}
