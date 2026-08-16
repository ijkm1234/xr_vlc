using System;
using System.Collections.Generic;
using UnityEngine;
using XRVLC.Localization;
using XRVLC.Media;

/// <summary>
/// 播放列表下拉面板。
/// 从 VLC AAR 拉取播放列表并绑定到统一 XrDropdown，选择项后跳播到对应媒体。
/// </summary>
public class PlaylistPanelController : MonoBehaviour
{
    [Header("Dropdown")]
    public GameObject panelRoot;
    public XrDropdown playlistDropdown;

    [Header("Legacy Scene References")]
    public Transform itemContainer;
    public GameObject itemPrefab;

    private readonly List<PlaylistItemData> _playlist = new List<PlaylistItemData>();
    private int _currentIndex = -1;
    private bool _isBindingDropdown;

    public bool IsVisible =>
        playlistDropdown != null
            ? playlistDropdown.IsOpen
            : panelRoot != null && panelRoot.activeSelf;

    private void Awake()
    {
        EnsureDropdown();
    }

    private void Start()
    {
        VlcPlaybackBridge.OnPlayRequestedEvent += OnMediaChanged;

        EnsureDropdown();
        if (playlistDropdown != null)
        {
            playlistDropdown.showCaption = false;
            playlistDropdown.onValueChanged.AddListener(OnPlaylistItemSelected);
            playlistDropdown.gameObject.SetActive(false);
        }
        else if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        VlcPlaybackBridge.OnPlayRequestedEvent -= OnMediaChanged;
        if (playlistDropdown != null)
            playlistDropdown.onValueChanged.RemoveListener(OnPlaylistItemSelected);
    }

    public void Toggle()
    {
        if (IsVisible)
            Hide();
        else
            Show();
    }

    public void Show()
    {
        EnsureDropdown();
        Refresh();
        if (playlistDropdown != null)
            playlistDropdown.Show();
        else if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (playlistDropdown != null)
        {
            playlistDropdown.CloseImmediately();
            playlistDropdown.gameObject.SetActive(false);
        }
        else if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    public void Refresh()
    {
        string json = VlcPlaybackBridge.GetPlaylist();
        Rebuild(json);
    }

    private void OnMediaChanged(MediaWrapper media)
    {
        _currentIndex = media != null ? media.PositionInList : -1;
        if (playlistDropdown != null && _currentIndex >= 0 && _currentIndex < playlistDropdown.Count)
            playlistDropdown.SetValueWithoutNotify(_currentIndex);
    }

    private void Rebuild(string json)
    {
        EnsureDropdown();

        _playlist.Clear();
        _playlist.AddRange(VlcPlaybackPayloadParser.ParsePlaylist(json));
        int currentFromPlaylist = _playlist.FindIndex(item => item.isCurrent);
        if (currentFromPlaylist >= 0)
            _currentIndex = currentFromPlaylist;

        if (playlistDropdown == null)
            return;

        if (_playlist.Count == 0)
        {
            _isBindingDropdown = true;
            playlistDropdown.SetPlaceholder(XrUiText.Get(XrUiTextKey.PlaylistEmpty));
            _isBindingDropdown = false;
            return;
        }

        var items = new List<XrDropdownItemData>(_playlist.Count);
        for (int i = 0; i < _playlist.Count; i++)
        {
            string title = DecodeDisplayTitle(_playlist[i].title);
            string duration = FormatDurationMs(_playlist[i].length);
            items.Add(new XrDropdownItemData(title, duration, i));
        }

        int selectedIndex = _currentIndex >= 0 && _currentIndex < _playlist.Count ? _currentIndex : 0;
        _isBindingDropdown = true;
        playlistDropdown.SetInteractable(true);
        playlistDropdown.SetItems(items, selectedIndex);
        _isBindingDropdown = false;
    }

    private void OnPlaylistItemSelected(int index)
    {
        if (_isBindingDropdown || index < 0 || index >= _playlist.Count)
            return;

        _currentIndex = index;
        VlcPlaybackBridge.SkipToIndex(index);
    }

    private void EnsureDropdown()
    {
        if (playlistDropdown == null)
            playlistDropdown = GetComponentInChildren<XrDropdown>(true);

        if (panelRoot == null && playlistDropdown != null)
            panelRoot = playlistDropdown.gameObject;
    }

    private static string DecodeDisplayTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return XrUiText.Get(XrUiTextKey.UnknownVideo);

        try
        {
            return Uri.UnescapeDataString(title);
        }
        catch (UriFormatException)
        {
            return title;
        }
    }

    private static string FormatDurationMs(long durationMs)
    {
        if (durationMs <= 0L)
            return "--:--";

        TimeSpan duration = TimeSpan.FromMilliseconds(durationMs);
        if (duration.TotalHours >= 1d)
            return $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}";

        return $"{duration.Minutes}:{duration.Seconds:00}";
    }
}
