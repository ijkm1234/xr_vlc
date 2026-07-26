using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class VlcHomePanelController : MonoBehaviour
{
    private Button m_HomeButton;

    public bool IsVisible => gameObject.activeSelf;

    private void Awake()
    {
        m_HomeButton = GetComponent<Button>();
        m_HomeButton.onClick.AddListener(OnHomeClicked);
    }

    private void OnDestroy()
    {
        if (m_HomeButton != null)
            m_HomeButton.onClick.RemoveListener(OnHomeClicked);
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }

    public void ApplyPlaybackSelectionResult(bool querySucceeded, bool hasActiveSelection)
    {
        SetVisible(querySucceeded && !hasActiveSelection);
    }

    private void OnHomeClicked()
    {
        SetVisible(false);

        VlcLibraryLauncher launcher = VlcLibraryLauncher.Instance;
        if (launcher == null)
        {
            Debug.LogWarning("[VlcHomePanel] VLC launcher is unavailable.");
            return;
        }

        launcher.OpenVLCMediaLibrary();
    }
}
