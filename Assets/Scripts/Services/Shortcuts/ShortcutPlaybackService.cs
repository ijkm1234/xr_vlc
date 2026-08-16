using UnityEngine;
using XRVLC.Media;

namespace XRVLC.Services.Shortcuts
{
    public class ShortcutPlaybackService
    {
        private readonly PlaybackService _playbackService;
        private ShortcutConfigData _mappings = ShortcutConfigData.Defaults();
        private int _seekSeconds = ShortcutSettingsService.DefaultSeekSeconds;
        private int _shortcutPlaybackRateHoldCount;

        public ShortcutPlaybackService(PlaybackService playbackService)
        {
            _playbackService = playbackService;
            ReloadConfig();
        }

        /// <summary>
        /// 重新读取 VLC 共享设置中的跳转秒数和快捷键映射。
        /// </summary>
        public void ReloadConfig()
        {
            _seekSeconds = ShortcutSettingsService.LoadShortcutSeekSeconds();
            _mappings = ShortcutSettingsService.LoadShortcutMappings();
        }

        /// <summary>
        /// 将输入状态机输出的快捷命令转换为播放或幕布操作。
        /// </summary>
        public void Execute(ShortcutCommand command)
        {
            if (_playbackService == null || command.Type == ShortcutCommandType.None)
                return;

            switch (command.Type)
            {
                case ShortcutCommandType.SeekBackward:
                    _playbackService.SeekRelativeSeconds(-_seekSeconds);
                    break;
                case ShortcutCommandType.SeekForward:
                    _playbackService.SeekRelativeSeconds(_seekSeconds);
                    break;
                case ShortcutCommandType.SeekBackward30Seconds:
                    _playbackService.SeekRelativeSeconds(-30);
                    break;
                case ShortcutCommandType.SeekForward30Seconds:
                    _playbackService.SeekRelativeSeconds(30);
                    break;
                case ShortcutCommandType.TogglePlayPause:
                    _playbackService.TogglePlayPause();
                    break;
                case ShortcutCommandType.BeginShortcutFastRate:
                    BeginShortcutPlaybackRate();
                    break;
                case ShortcutCommandType.EndShortcutFastRate:
                    EndShortcutPlaybackRate();
                    break;
                case ShortcutCommandType.ConfigurableAction:
                    ExecuteAction(_mappings.GetAction(command.ButtonId));
                    break;
            }
        }

        /// <summary>
        /// 按配置项执行实际动作，未知或 none 配置会被忽略。
        /// </summary>
        private void ExecuteAction(string actionKey)
        {
            switch (actionKey)
            {
                case ShortcutActions.Toggle2xSpeed:
                    _playbackService.ToggleShortcutPlaybackRate();
                    break;
                case ShortcutActions.ToggleSubtitle:
                    _playbackService.ToggleSubtitleTrack();
                    break;
                case ShortcutActions.ResetScreenTransform:
                    _playbackService.ResetVideoScreenTransform();
                    break;
            }
        }

        public void BeginShortcutPlaybackRate()
        {
            _shortcutPlaybackRateHoldCount++;
            if (_shortcutPlaybackRateHoldCount == 1)
                _playbackService.SetShortcutPlaybackRateHeld(true);
        }

        public void EndShortcutPlaybackRate()
        {
            if (_shortcutPlaybackRateHoldCount <= 0)
                return;

            _shortcutPlaybackRateHoldCount--;
            if (_shortcutPlaybackRateHoldCount == 0)
                _playbackService.SetShortcutPlaybackRateHeld(false);
        }

        public void CancelShortcutPlaybackRateHold()
        {
            if (_shortcutPlaybackRateHoldCount == 0)
                return;

            _shortcutPlaybackRateHoldCount = 0;
            _playbackService?.SetShortcutPlaybackRateHeld(false);
        }

        /// <summary>
        /// 沿玩家视线方向移动当前视频幕布或沉浸球幕。
        /// </summary>
        public void OffsetVideoScreenDistance(float deltaMeters)
        {
            _playbackService?.OffsetVideoScreenDistance(deltaMeters);
        }

        /// <summary>
        /// 记录 Grip 拖动开始时的手柄射线方向。
        /// </summary>
        public void BeginVideoScreenControllerMove(Vector3 controllerRayDirection)
        {
            _playbackService?.BeginVideoScreenControllerMove(controllerRayDirection);
        }

        /// <summary>
        /// 按手柄射线方向变化更新视频幕布位置。
        /// </summary>
        public void UpdateVideoScreenControllerMove(Vector3 controllerRayDirection)
        {
            _playbackService?.UpdateVideoScreenControllerMove(controllerRayDirection);
        }

        /// <summary>
        /// 结束 Grip 拖动并保留当前幕布位置。
        /// </summary>
        public void EndVideoScreenControllerMove()
        {
            _playbackService?.EndVideoScreenControllerMove();
        }
    }
}
