using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XRVLC;
using XRVLC.Services.Shortcuts;
using XRVLC.XR;

public class ShortcutConfigPanel : MonoBehaviour
{
    [Header("Dropdowns")]
    public XrDropdown leftStickClickDropdown;
    public XrDropdown rightStickClickDropdown;
    public XrDropdown buttonYDropdown;
    public XrDropdown buttonBDropdown;

    [Header("Buttons")]
    public Button saveButton;
    public Button closeButton;

    [Header("Runtime")]
    public ShortcutManager shortcutManager;

    private ShortcutConfigData _mappings = ShortcutConfigData.Defaults();
    private readonly List<string> _actionKeys = new List<string>(ShortcutActions.All);

    /// <summary>
    /// 面板显示时刷新下拉选项和值，确保反映最新共享配置。
    /// </summary>
    private void OnEnable()
    {
        SetupDropdowns();
        LoadValues();
    }

    /// <summary>
    /// 注册保存和关闭按钮事件。
    /// </summary>
    private void Start()
    {
        if (saveButton != null)
            saveButton.onClick.AddListener(Save);
        if (closeButton != null)
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
    }

    /// <summary>
    /// 面板销毁时解除保存按钮监听，避免重复订阅。
    /// </summary>
    private void OnDestroy()
    {
        if (saveButton != null)
            saveButton.onClick.RemoveListener(Save);
    }

    /// <summary>
    /// 从共享配置加载四个可配置按键的当前操作。
    /// </summary>
    public void LoadValues()
    {
        _mappings = ShortcutSettingsService.LoadShortcutMappings();
        SetDropdownValue(leftStickClickDropdown, _mappings.GetAction(ShortcutButtons.LeftStickClick));
        SetDropdownValue(rightStickClickDropdown, _mappings.GetAction(ShortcutButtons.RightStickClick));
        SetDropdownValue(buttonYDropdown, _mappings.GetAction(ShortcutButtons.ButtonY));
        SetDropdownValue(buttonBDropdown, _mappings.GetAction(ShortcutButtons.ButtonB));
    }

    /// <summary>
    /// 将面板上的四个下拉选择写回 SharedPreferences，并通知运行时管理器刷新缓存。
    /// </summary>
    public void Save()
    {
        var mappings = new ShortcutConfigData();
        mappings.SetAction(ShortcutButtons.LeftStickClick, GetDropdownAction(leftStickClickDropdown));
        mappings.SetAction(ShortcutButtons.RightStickClick, GetDropdownAction(rightStickClickDropdown));
        mappings.SetAction(ShortcutButtons.ButtonY, GetDropdownAction(buttonYDropdown));
        mappings.SetAction(ShortcutButtons.ButtonB, GetDropdownAction(buttonBDropdown));

        ShortcutSettingsService.SaveShortcutMappings(mappings);
        if (shortcutManager == null)
            shortcutManager = FindAnyObjectByType<ShortcutManager>();
        shortcutManager?.ReloadConfig();
    }

    /// <summary>
    /// 初始化所有下拉菜单的选项列表。
    /// </summary>
    private void SetupDropdowns()
    {
        SetupDropdown(leftStickClickDropdown);
        SetupDropdown(rightStickClickDropdown);
        SetupDropdown(buttonYDropdown);
        SetupDropdown(buttonBDropdown);
    }

    /// <summary>
    /// 为单个下拉菜单填充当前支持的操作文案。
    /// </summary>
    private void SetupDropdown(XrDropdown dropdown)
    {
        if (dropdown == null) return;

        dropdown.SetItems(new List<XrDropdownItemData>
        {
            new XrDropdownItemData("None"),
            new XrDropdownItemData("Toggle 2x speed"),
            new XrDropdownItemData("Toggle subtitles"),
            new XrDropdownItemData("Reset screen")
        });
    }

    /// <summary>
    /// 根据操作标识设置下拉菜单选中项，未知值回退到“无操作”。
    /// </summary>
    private void SetDropdownValue(XrDropdown dropdown, string actionKey)
    {
        if (dropdown == null) return;
        int index = _actionKeys.IndexOf(actionKey);
        dropdown.SetValueWithoutNotify(index >= 0 ? index : 0);
    }

    /// <summary>
    /// 将下拉菜单索引转换回内部操作标识。
    /// </summary>
    private string GetDropdownAction(XrDropdown dropdown)
    {
        if (dropdown == null || dropdown.Value < 0 || dropdown.Value >= _actionKeys.Count)
            return ShortcutActions.None;
        return _actionKeys[dropdown.Value];
    }
}
