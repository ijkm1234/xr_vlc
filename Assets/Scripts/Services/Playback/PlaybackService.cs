using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XRVLC.Media;

namespace XRVLC.Media
{
    /// <summary>
    /// 核心播放服务，负责调度播放器逻辑、屏幕逻辑。
    /// 列表状态由 Android AAR 维护。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlaybackService : MonoBehaviour, IPlaybackEvents
    {
        private const string DisabledSubtitleTrack = "-1";
        private const float ShortcutFastRate = 2f;

        public static PlaybackService Instance { get; private set; }

        public PlayerStatus CurrentStatus => VlcPlaybackEvents.Snapshot.Status;
        public bool UseHardwareDecoding { get; private set; } = true;
        public SubtitleRenderMode SubtitleRenderMode { get; private set; } = SubtitleRenderMode.Spatial;
        public VideoGeometrySelection CurrentGeometrySelection => _currentGeometrySelection;
        
        public MediaWrapper CurrentMedia { get; private set; }

        [Header("Scene References")]
        public VideoScreen videoScreen;

        // --- IPlaybackEvents 实现 ---
#pragma warning disable 0067
        public event Action<PlayerStatus> OnStatusChanged;
        public event Action<string> OnError;
        public event Action OnPlaylistUpdated;
        public event Action<MediaWrapper, int> OnMediaChanged;
        public event Action<RepeatMode, bool> OnPlaybackModeChanged;
        public event Action<long, long> OnTimeChanged;
        public event Action<float> OnBuffering;
        public event Action<List<TrackInfo>> OnAudioTracksChanged;
        public event Action<List<TrackInfo>> OnSubtitleTracksChanged;
        public event Action<SubtitleCue> OnSubtitleCue;
#pragma warning restore 0067

        // --- PlayerController 状态 ---
        private string _pendingUrl = "";
        private long _pendingStartTimeMs = 0;
        private int _currentWidth = 0;
        private int _currentHeight = 0;
        private readonly TrackSelectionService _trackSelectionService = new TrackSelectionService();
        private bool _isShortcutFastRate;
        private VideoScreenGeometryService _geometryService;
        private PlayerStatus _lastNotifiedStatus = PlayerStatus.Idle;
        private bool _hasManualGeometryOverride;
        private VideoGeometrySelection _manualGeometrySelection = new VideoGeometrySelection(VideoProjection.Flat, StereoMode.Mono, FlatVideoCurveMode.None);
        private VideoGeometrySelection _currentGeometrySelection = new VideoGeometrySelection(VideoProjection.Flat, StereoMode.Mono, FlatVideoCurveMode.None);

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private long _currentTotalTime = 0;
        private long _currentTime = 0;

        private void Start()
        {
            if (videoScreen == null)
            {
                Debug.LogError("[PlaybackService] 缺少 VideoScreen 引用！");
                return;
            }

            // 初始化底层渲染屏幕
            videoScreen.RebuildLayer(UseHardwareDecoding);
            _geometryService = new VideoScreenGeometryService(videoScreen, () => CurrentMedia, () => UseHardwareDecoding, GetManualGeometryOverride);

            // 订阅 Bridge 回调
            VlcPlaybackEvents.OnMediaParseFinished += HandleMediaParseFinished;
            VlcPlaybackEvents.OnVideoSizeChanged += OnVideoSizeChanged;
            VlcPlaybackEvents.OnStateChanged += OnStateChanged;
            VlcPlaybackEvents.OnTimeChanged += HandleTimeChanged;
            VlcPlaybackEvents.OnLengthChanged += HandleLengthChanged;
            VlcPlaybackEvents.OnBuffering += HandleBuffering;
            VlcPlaybackEvents.OnAudioTracksChanged += HandleAudioTracksChanged;
            VlcPlaybackEvents.OnSubtitleTracksChanged += HandleSubtitleTracksChanged;
            VlcPlaybackEvents.OnSubtitleCue += HandleSubtitleCue;
            VlcPlaybackEvents.OnPlayRequested += LoadAndPlay;

            VlcPlaybackBridge.SetSubtitleRenderMode(SubtitleRenderMode);
        }

        private void OnDestroy()
        {
            // 取消订阅
            VlcPlaybackEvents.OnMediaParseFinished -= HandleMediaParseFinished;
            VlcPlaybackEvents.OnVideoSizeChanged -= OnVideoSizeChanged;
            VlcPlaybackEvents.OnStateChanged -= OnStateChanged;
            VlcPlaybackEvents.OnTimeChanged -= HandleTimeChanged;
            VlcPlaybackEvents.OnLengthChanged -= HandleLengthChanged;
            VlcPlaybackEvents.OnBuffering -= HandleBuffering;
            VlcPlaybackEvents.OnAudioTracksChanged -= HandleAudioTracksChanged;
            VlcPlaybackEvents.OnSubtitleTracksChanged -= HandleSubtitleTracksChanged;
            VlcPlaybackEvents.OnSubtitleCue -= HandleSubtitleCue;
            VlcPlaybackEvents.OnPlayRequested -= LoadAndPlay;
        }

        private void HandleStatusChanged(PlayerStatus status)
        {
            if (_lastNotifiedStatus == status) return;
            Debug.Log($"[PlaybackService] 当前播放状态: {status}");
            VlcPlaybackEvents.Snapshot.Status = status;
            _lastNotifiedStatus = status;
            OnStatusChanged?.Invoke(status);
        }

        // --------------------------------------------------------
        // 播放逻辑 (原 PlayerController 逻辑)
        // --------------------------------------------------------
        public void PlayVideo(string path, long startTimeMs = 0, string extraData = null)
        {
            if (videoScreen == null)
            {
                Debug.LogError("[PlaybackService] 缺少 VideoScreen！");
                return;
            }

            Debug.Log($"[PlaybackService] 准备播放: {XRVLC.Utils.UriUtils.RedactUri(path)}");
            HandleStatusChanged(PlayerStatus.Opening);

            // 1. 记录待播放的参数
            _pendingUrl = path;
            _pendingStartTimeMs = startTimeMs;
            _currentWidth = 0;
            _currentHeight = 0;

            // 2. 告诉 AAR 预加载 URL
            // 获取暂存的 JSON 字符串，如果为空则直接使用 URI
            string payload = !string.IsNullOrEmpty(extraData) ? extraData : path;
            Debug.Log($"[PlaybackService] 最终传给 AAR 的 payload: {payload}");
            VlcPlaybackBridge.PreloadLocation(payload);
        }

        /// <summary>
        /// 由 preloadLocation 解析完成后触发。携带 libvlc 读出的 projection，
        /// 是 SetGeometry 的权威来源。替代旧的 OnVideoSizeChanged 首次回调。
        /// </summary>
        private void HandleMediaParseFinished(VlcMediaParseResult result)
        {
            int width  = result.width;
            int height = result.height;

            if (width <= 0 || height <= 0)
            {
                Debug.LogWarning($"[PlaybackService] HandleMediaParseFinished: 无效尺寸 {width}x{height}");
                return;
            }

            _currentWidth  = width;
            _currentHeight = height;

            // 将 Android 解析出的 projection 写回 CurrentMedia，
            // ProjectionDetector 会把它作为 Priority 1 使用。
            if (CurrentMedia != null && result.projection != "flat")
            {
                CurrentMedia.Projection = result.projection switch
                {
                    "360" => MediaProjectionType.Sphere360,
                    "180" => MediaProjectionType.Sphere180,
                    _     => MediaProjectionType.Flat2D
                };
            }

            Debug.Log($"[PlaybackService] OnMediaParseFinished: {width}x{height}, projection={result.projection}, duration={result.duration}ms");
            RebuildAndApplyGeometry((uint)width, (uint)height);
        }

        /// <summary>
        /// 由 onNewVideoLayout 触发，用于解码器实际输出与静态解析不一致时的二次矫正。
        /// 此时 CurrentMedia.Projection 已由 HandleMediaParseFinished 更新，SetGeometry 结果一致。
        /// </summary>
        private void OnVideoSizeChanged(int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            if (_currentWidth == width && _currentHeight == height) return;

            _currentWidth  = width;
            _currentHeight = height;

            Debug.Log($"[PlaybackService] OnVideoSizeChanged (矫正): {width}x{height}");
            RebuildAndApplyGeometry((uint)width, (uint)height);
        }

        private void RebuildAndApplyGeometry(uint width, uint height)
        {
            if (_geometryService == null && videoScreen != null)
                _geometryService = new VideoScreenGeometryService(videoScreen, () => CurrentMedia, () => UseHardwareDecoding, GetManualGeometryOverride);
            if (_geometryService != null)
            {
                _currentGeometrySelection = ResolveRequestedGeometry();
                StartCoroutine(_geometryService.RebuildAndBind(width, height));
            }
        }

        private VideoGeometrySelection? GetManualGeometryOverride()
        {
            return _hasManualGeometryOverride ? _manualGeometrySelection : (VideoGeometrySelection?)null;
        }

        private VideoGeometrySelection ResolveRequestedGeometry()
        {
            if (_hasManualGeometryOverride)
                return _manualGeometrySelection;

            var (projection, stereo) = XRVLC.ProjectionDetector.Detect(CurrentMedia);
            return new VideoGeometrySelection(projection, stereo, FlatVideoCurveMode.None);
        }

        private void OnStateChanged(string state)
        {
            switch (state)
            {
                case "Playing": HandleStatusChanged(PlayerStatus.Playing); break;
                case "Paused": HandleStatusChanged(PlayerStatus.Paused); break;
                case "Stopped": HandleStatusChanged(PlayerStatus.Stopped); break;
                case "Ended":
                    HandleStatusChanged(PlayerStatus.Ended);
                    ClearSubtitleCue();
                    break;
                case "Error":
                    HandleStatusChanged(PlayerStatus.Error);
                    OnError?.Invoke("AAR PlaybackService reported Error");
                    StopInternal();
                    break;
            }
        }

        private void HandleTimeChanged(long timeMs)
        {
            _currentTime = timeMs;
            OnTimeChanged?.Invoke(timeMs, _currentTotalTime);
        }

        private void HandleLengthChanged(long length)
        {
            _currentTotalTime = length;
            // OnMediaParsed?.Invoke(length); // 如果有其他地方依赖可以补充
        }

        private void HandleBuffering(float buffering)
        {
            if (buffering < 100f) HandleStatusChanged(PlayerStatus.Buffering);
            else HandleStatusChanged(PlayerStatus.Playing);
        }

        private void HandleAudioTracksChanged(List<TrackInfo> tracks)
        {
            OnAudioTracksChanged?.Invoke(tracks);
        }

        private void HandleSubtitleTracksChanged(List<TrackInfo> tracks)
        {
            _trackSelectionService.UpdateSubtitleTracks(tracks);
            OnSubtitleTracksChanged?.Invoke(tracks);
        }

        private void HandleSubtitleCue(SubtitleCue cue)
        {
            OnSubtitleCue?.Invoke(cue ?? SubtitleCue.Clear());
        }

        private void ClearSubtitleCue()
        {
            SubtitleCue clearCue = SubtitleCue.Clear();
            VlcPlaybackEvents.Snapshot.SetSubtitleCue(clearCue);
            OnSubtitleCue?.Invoke(clearCue);
        }

        private void StopInternal()
        {
            _currentWidth = 0;
            _currentHeight = 0;
            _isShortcutFastRate = false;
            ClearSubtitleCue();
            
            VlcPlaybackBridge.Stop();
            VlcPlaybackBridge.DetachSurface();

            if (videoScreen != null)
            {
                videoScreen.DestroyLayer();
            }

            HandleStatusChanged(PlayerStatus.Stopped);
        }

        // --------------------------------------------------------
        // 视图与生命周期绑定
        // --------------------------------------------------------
        public void AttachVideoScreen(VideoScreen screen)
        {
            videoScreen = screen;
            if (videoScreen != null)
            {
                _geometryService = new VideoScreenGeometryService(videoScreen, () => CurrentMedia, () => UseHardwareDecoding, GetManualGeometryOverride);
                videoScreen.RebuildLayer(UseHardwareDecoding);
            }
        }

        public void DetachVideoScreen()
        {
            videoScreen = null;
            _geometryService = null;
        }

        public void OnXRFocusChanged(bool hasFocus)
        {
            if (!hasFocus && CurrentStatus == PlayerStatus.Playing)
            {
                Pause();
            }
            else if (hasFocus && CurrentStatus == PlayerStatus.Paused)
            {
                Play();
            }
        }

        // --------------------------------------------------------
        // 外部传入新视频
        // --------------------------------------------------------
        public void LoadAndPlay(MediaWrapper item)
        {
            CurrentMedia = item;
            VlcPlaybackEvents.Snapshot.CurrentMedia = item;
            _hasManualGeometryOverride = false;
            _manualGeometrySelection = new VideoGeometrySelection(VideoProjection.Flat, StereoMode.Mono, FlatVideoCurveMode.None);
            ClearSubtitleCue();
            ResetShortcutPlaybackState(item);
            OnMediaChanged?.Invoke(item, 0); // Position is not accurate but UI doesn't strictly need it

            // Apply projection early (scene layout: hide/show background board, sphere centering)
            // when the Android side has already told us the projection type.
            // OnVideoSizeChanged will do a final authoritative pass once dimensions are known.
            VideoGeometrySelection initialGeometry = ResolveRequestedGeometry();
            _currentGeometrySelection = initialGeometry;
            if (initialGeometry.Projection != VideoProjection.Flat || initialGeometry.Stereo != StereoMode.Mono)
            {
                videoScreen.SetGeometry(initialGeometry.Projection, initialGeometry.Stereo, initialGeometry.CurveMode);
            }

            // Unity no longer manages the playlist. AAR tells us to play.
            PlayVideo(item.Uri, item.Time, item.RawJson);
        }

        // --------------------------------------------------------
        // 基础播放控制
        // --------------------------------------------------------
        public void Play()
        {
            VlcPlaybackBridge.Play();
        }

        public void Pause()
        {
            VlcPlaybackBridge.Pause();
        }

        public void TogglePlayPause()
        {
            if (CurrentStatus == PlayerStatus.Playing) Pause();
            else Play();
        }

        public void Stop()
        {
            StopInternal();
        }

        public void SeekTo(long timeMs)
        {
            ClearSubtitleCue();
            VlcPlaybackBridge.SetTime(timeMs);
        }

        /// <summary>
        /// 基于播放器当前时间做相对跳转，并把结果限制在 0ms 之后。
        /// </summary>
        public void SeekRelativeSeconds(int seconds)
        {
            long target = Math.Max(0L, _currentTime + seconds * 1000L);
            SeekTo(target);
        }

        public void SeekToPosition(float position)
        {
            ClearSubtitleCue();
            VlcPlaybackBridge.Seek(position);
        }

        /// <summary>
        /// 将手柄快捷键产生的前后偏移交给 VideoScreen 处理。
        /// </summary>
        public void OffsetVideoScreenDistance(float deltaMeters)
        {
            videoScreen?.AddViewerDistanceOffset(deltaMeters);
        }

        /// <summary>
        /// 通知 VideoScreen 记录 Grip 拖动开始时的手柄射线方向。
        /// </summary>
        public void BeginVideoScreenControllerMove(Vector3 controllerRayDirection)
        {
            videoScreen?.BeginControllerMove(controllerRayDirection);
        }

        /// <summary>
        /// 通知 VideoScreen 根据手柄射线方向变化更新幕布。
        /// </summary>
        public void UpdateVideoScreenControllerMove(Vector3 controllerRayDirection)
        {
            videoScreen?.UpdateControllerMove(controllerRayDirection);
        }

        /// <summary>
        /// 通知 VideoScreen 结束 Grip 拖动。
        /// </summary>
        public void EndVideoScreenControllerMove()
        {
            videoScreen?.EndControllerMove();
        }

        /// <summary>
        /// 将视频幕布恢复到场景默认位置和默认平面缩放。
        /// </summary>
        public void ResetVideoScreenTransform()
        {
            videoScreen?.ResetTransformToDefault();
        }

        public void SetManualVideoGeometry(VideoProjection projection, StereoMode stereo, FlatVideoCurveMode curveMode)
        {
            if (projection != VideoProjection.Cylinder)
                curveMode = FlatVideoCurveMode.None;

            VideoGeometrySelection previousGeometry = _currentGeometrySelection;
            VideoGeometrySelection nextGeometry = new VideoGeometrySelection(projection, stereo, curveMode);

            _hasManualGeometryOverride = true;
            _manualGeometrySelection = nextGeometry;

            if (videoScreen == null)
                return;

            if (_currentWidth > 0 && _currentHeight > 0)
            {
                if (ShouldRebuildForManualGeometryChange(previousGeometry, nextGeometry))
                {
                    RebuildAndApplyGeometry((uint)_currentWidth, (uint)_currentHeight);
                    return;
                }

                ApplyManualGeometryWithoutRebuild(projection, stereo, curveMode);
                return;
            }

            videoScreen.SetGeometry(projection, stereo, curveMode);
            _currentGeometrySelection = nextGeometry;
        }

        private void ApplyManualGeometryWithoutRebuild(VideoProjection projection, StereoMode stereo, FlatVideoCurveMode curveMode)
        {
            videoScreen.SetGeometry(projection, stereo, curveMode);
            videoScreen.FitVideoSize((uint)_currentWidth, (uint)_currentHeight);
            _currentGeometrySelection = new VideoGeometrySelection(projection, stereo, curveMode);
        }

        private static bool ShouldRebuildForManualGeometryChange(VideoGeometrySelection previousGeometry, VideoGeometrySelection nextGeometry)
        {
            return previousGeometry.Projection != nextGeometry.Projection
                || previousGeometry.Stereo != nextGeometry.Stereo
                || previousGeometry.CurveMode != nextGeometry.CurveMode;
        }

        public void Next(bool forceUserAction = true)
        {
            VlcPlaybackBridge.Next();
        }

        public void Previous()
        {
            VlcPlaybackBridge.Previous();
        }

        public void SkipTo(int index)
        {
            VlcPlaybackBridge.SkipToIndex(index);
        }

        // --------------------------------------------------------
        // 播放模式与高级控制
        // --------------------------------------------------------
        public void SetRepeatMode(RepeatMode mode)
        {
            VlcPlaybackBridge.SetRepeatMode((int)mode);
        }

        public void SetShuffle(bool enable)
        {
            VlcPlaybackBridge.SetShuffle(enable);
        }

        public void ToggleDecodingMode()
        {
            UseHardwareDecoding = !UseHardwareDecoding;
            Debug.Log($"[PlaybackService] 切换解码模式为: {(UseHardwareDecoding ? "硬解" : "软解")}");
            // 需要实现软硬解切换协同 PlayerController，暂时留空
        }

        /// <summary>
        /// 设置播放倍速，并通过 AAR 桥接转发到底层 VLC PlaybackService。
        /// </summary>
        public void SetPlaybackRate(float rate)
        {
            _isShortcutFastRate = Mathf.Approximately(rate, ShortcutFastRate);
            VlcPlaybackBridge.SetRate(rate);
        }

        /// <summary>
        /// 在 1x 和快捷倍速之间切换，播放状态由 PlaybackService 统一维护。
        /// </summary>
        public void ToggleShortcutPlaybackRate()
        {
            SetPlaybackRate(_isShortcutFastRate ? 1f : ShortcutFastRate);
        }

        /// <summary>
        /// Trigger 长按快捷倍速：达到长按阈值后进入 2x，全部松开恢复 1x。
        /// </summary>
        public void SetShortcutPlaybackRateHeld(bool held)
        {
            SetPlaybackRate(held ? ShortcutFastRate : 1f);
        }

        public void SetAudioTrack(string trackId)
        {
            VlcPlaybackBridge.SetAudioTrack(trackId);
            if (CurrentMedia != null) CurrentMedia.AudioTrack = trackId;
        }

        public void SetSubtitleTrack(string trackId)
        {
            ClearSubtitleCue();
            VlcPlaybackBridge.SetSpuTrack(trackId);
            if (CurrentMedia != null) CurrentMedia.SpuTrack = trackId;
        }

        public void SetSubtitleRenderMode(SubtitleRenderMode mode)
        {
            SubtitleRenderMode = mode;
            ClearSubtitleCue();
            VlcPlaybackBridge.SetSubtitleRenderMode(mode);

            if (mode == SubtitleRenderMode.Off && CurrentMedia != null)
            {
                CurrentMedia.SpuTrack = DisabledSubtitleTrack;
            }
        }

        public void ToggleSubtitleRenderMode()
        {
            SubtitleRenderMode nextMode = SubtitleRenderMode switch
            {
                SubtitleRenderMode.Native => SubtitleRenderMode.Spatial,
                SubtitleRenderMode.Spatial => SubtitleRenderMode.Native,
                SubtitleRenderMode.Off => SubtitleRenderMode.Spatial,
                _ => SubtitleRenderMode.Native
            };
            SetSubtitleRenderMode(nextMode);
        }

        /// <summary>
        /// 在当前字幕轨和上次可用字幕轨之间切换，关闭字幕使用 VLC 的 -1 轨道。
        /// </summary>
        public void ToggleSubtitleTrack()
        {
            string currentTrack = CurrentMedia?.SpuTrack ?? DisabledSubtitleTrack;
            bool subtitleEnabled = currentTrack != DisabledSubtitleTrack;

            if (subtitleEnabled)
            {
                _trackSelectionService.RememberSubtitleTrack(currentTrack);
                SetSubtitleTrack(DisabledSubtitleTrack);
                return;
            }

            string trackToRestore = FindRestorableSubtitleTrack();
            if (trackToRestore != DisabledSubtitleTrack)
                SetSubtitleTrack(trackToRestore);
        }

        /// <summary>
        /// 切换媒体时重置快捷播放状态，并记录当前媒体的字幕轨。
        /// </summary>
        private void ResetShortcutPlaybackState(MediaWrapper media)
        {
            _isShortcutFastRate = false;
            _trackSelectionService.Reset(media);
        }

        /// <summary>
        /// 优先恢复关闭前的字幕轨；如果不可用，则选择第一个有效字幕轨。
        /// </summary>
        private string FindRestorableSubtitleTrack()
        {
            return _trackSelectionService.FindRestorableSubtitleTrack();
        }

        public void SetAudioDelay(long delayMs) { /* 需要 AAR 支持 */ }
        public void SetSubtitleDelay(long delayMs) { /* 需要 AAR 支持 */ }
    }
}
