using System;
using UnityEngine;

namespace XRVLC
{
    public class ShortcutConfigData
    {
        private string _rightStickClick = ShortcutActions.None;
        private string _leftStickClick = ShortcutActions.None;
        private string _buttonB = ShortcutActions.None;
        private string _buttonY = ShortcutActions.None;

        /// <summary>
        /// 构造首次运行时使用的内置默认映射，和设计文档中的默认值保持一致。
        /// </summary>
        public static ShortcutConfigData Defaults()
        {
            var data = new ShortcutConfigData();
            data.SetAction(ShortcutButtons.RightStickClick, ShortcutActions.ResetScreenTransform);
            data.SetAction(ShortcutButtons.LeftStickClick, ShortcutActions.ResetScreenTransform);
            data.SetAction(ShortcutButtons.ButtonB, ShortcutActions.TogglePassthroughBackground);
            data.SetAction(ShortcutButtons.ButtonY, ShortcutActions.TogglePassthroughBackground);
            return data;
        }

        /// <summary>
        /// 返回可直接写入 SharedPreferences 的默认 JSON 字符串。
        /// </summary>
        public static string DefaultJson() => Defaults().ToJson();

        /// <summary>
        /// 从 SharedPreferences 中保存的 JSON 恢复映射；空值或坏数据会回退到默认映射。
        /// </summary>
        public static ShortcutConfigData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Defaults();

            try
            {
                var dto = JsonUtility.FromJson<MappingDto>(json);
                if (dto == null) return Defaults();

                var data = new ShortcutConfigData();
                data.SetAction(ShortcutButtons.RightStickClick, dto.right_stick_click);
                data.SetAction(ShortcutButtons.LeftStickClick, dto.left_stick_click);
                data.SetAction(ShortcutButtons.ButtonB, dto.button_b);
                data.SetAction(ShortcutButtons.ButtonY, dto.button_y);
                return data;
            }
            catch (Exception)
            {
                return Defaults();
            }
        }

        /// <summary>
        /// 获取指定按键当前绑定的操作标识，未知按键按无操作处理。
        /// </summary>
        public string GetAction(string buttonId) => buttonId switch
        {
            ShortcutButtons.RightStickClick => _rightStickClick,
            ShortcutButtons.LeftStickClick => _leftStickClick,
            ShortcutButtons.ButtonB => _buttonB,
            ShortcutButtons.ButtonY => _buttonY,
            _ => ShortcutActions.None
        };

        /// <summary>
        /// 设置单个按键映射；未知操作会被归一化为“无操作”，避免坏配置触发异常。
        /// </summary>
        public void SetAction(string buttonId, string actionKey)
        {
            string action = ShortcutActions.IsKnown(actionKey)
                ? actionKey
                : ShortcutActions.None;

            switch (buttonId)
            {
                case ShortcutButtons.RightStickClick:
                    _rightStickClick = action;
                    break;
                case ShortcutButtons.LeftStickClick:
                    _leftStickClick = action;
                    break;
                case ShortcutButtons.ButtonB:
                    _buttonB = action;
                    break;
                case ShortcutButtons.ButtonY:
                    _buttonY = action;
                    break;
            }
        }

        /// <summary>
        /// 序列化为与 AAR 设置页共享的 xr_button_mappings JSON 结构。
        /// </summary>
        public string ToJson()
        {
            var dto = new MappingDto
            {
                right_stick_click = _rightStickClick,
                left_stick_click = _leftStickClick,
                button_b = _buttonB,
                button_y = _buttonY
            };
            return JsonUtility.ToJson(dto);
        }

        [Serializable]
        private class MappingDto
        {
            public string right_stick_click;
            public string left_stick_click;
            public string button_b;
            public string button_y;
        }
    }
}
