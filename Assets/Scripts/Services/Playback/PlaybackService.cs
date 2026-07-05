using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XRVLC.Media;
using XRVLC.Services.Settings;

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
        private const string SurfaceDebugTag = "XR_SURFACE_DEBUG";

        public static PlaybackService Instance { get; private set; }

        public PlayerStatus CurrentStatus => VlcPlaybackEvents.Snapshot.Status;
        public bool UseHardwareDecoding { get; private set; } = true;
        public SubtitleRenderMode SubtitleRenderMode { get; private set; } = SubtitleRenderMode.Spatial;
        public bool RenderSubtitlesOutsideScreen { get; private set; }
        public float SubtitleDelaySeconds { get; private set; }
        public VideoGeometrySelection CurrentGeometrySelection => _currentGeometrySelection;
        public VideoScaleMode CurrentVideoScaleMode { get; private set; } = VideoScaleMode.Fit;
        public VideoAspectRatio CurrentVideoAspectRatio { get; private set; } = VideoAspectRatio.Source;
        
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
#pragma warning restore 0067

        // --- PlayerController 状态 ---
        private string _pendingUrl = "";
        private long _pendingStartTimeMs = 0;
        private int _currentWidth = 0;
        private int _currentHeight = 0;
        private int _currentVisibleWidth = 0;
        private int _currentVisibleHeight = 0;
        private readonly TrackSelectionService _trackSelectionService = new TrackSelectionService();
        private bool _isShortcutFastRate;
        private VideoScreenGeometryService _geometryService;
        private PlayerStatus _lastNotifiedStatus = PlayerStatus.Idle;
        private bool _hasManualGeometryOverride;
        private VideoGeometrySelection _manualGeometrySelection = new VideoGeometrySelection(VideoProjection.Flat, StereoMode.Mono, FlatVideoCurveMode.None);
        private VideoGeometrySelection _currentGeometrySelection = new VideoGeometrySelection(VideoProjection.Flat, StereoMode.Mono, FlatVideoCurveMode.None);
        private Coroutine _geometryBindingCoroutine;
        private Coroutine _nativeSubtitleSurfaceCoroutine;
        private VideoSurfaceSpec? _boundVideoSurfaceSpec;
        private SubtitleSurfaceSpec? _boundSubtitleSurfaceSpec;
        private bool _videoSurfaceBoundToVlc;
        private bool _subtitleSurfaceBoundToVlc;
        private VlcVideoSize CurrentVideoSize =>
            new VlcVideoSize(_currentWidth, _currentHeight, _currentVisibleWidth, _currentVisibleHeight);

        private static void SurfaceDebug(string message)
        {
            Debug.Log($"[{SurfaceDebugTag}] {message}");
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass log = new AndroidJavaClass("android.util.Log"))
                    log.CallStatic<int>("e", SurfaceDebugTag, message);
            }
            catch
            {
                // Keep diagnostics best-effort; never let logging affect playback.
            }
#endif
        }

        private string FormatSurfaceState()
        {
            if (videoScreen == null)
                return "videoScreen=null";

            return $"videoReady={videoScreen.IsHardwareSurfaceReady()}, videoSurface={videoScreen.GetHardwareSurfaceHandle()}, " +
                   $"subtitleReady={videoScreen.IsFlatSubtitleSurfaceReady()}, subtitleSurface={videoScreen.GetFlatSubtitleSurfaceHandle()}, " +
                   $"videoBound={_videoSurfaceBoundToVlc}, subtitleBound={_subtitleSurfaceBoundToVlc}";
        }

        private static string RedactForLog(string uri)
        {
            return string.IsNullOrEmpty(uri) ? "<empty>" : XRVLC.Utils.UriUtils.RedactUri(uri);
        }

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

            CurrentVideoScaleMode = VideoScaleMode.Fit;
            CurrentVideoAspectRatio = PlaybackUiSettingsService.LoadVideoAspectRatio();
            videoScreen.SetVideoLayout(CurrentVideoScaleMode, CurrentVideoAspectRatio);

            // 初始化底层渲染屏幕
            videoScreen.RebuildLayer(UseHardwareDecoding);
            _geometryService = CreateGeometryService(videoScreen);

            // 订阅 Bridge 回调
            VlcPlaybackEvents.OnMediaParseFinished += HandleMediaParseFinished;
            VlcPlaybackEvents.OnVideoSizeChanged += OnVideoSizeChanged;
            VlcPlaybackEvents.OnStateChanged += OnStateChanged;
            VlcPlaybackEvents.OnTimeChanged += HandleTimeChanged;
            VlcPlaybackEvents.OnLengthChanged += HandleLengthChanged;
            VlcPlaybackEvents.OnBuffering += HandleBuffering;
            VlcPlaybackEvents.OnAudioTracksChanged += HandleAudioTracksChanged;
            VlcPlaybackEvents.OnSubtitleTracksChanged += HandleSubtitleTracksChanged;
            VlcPlaybackEvents.OnPlayRequested += LoadAndPlay;
            VlcPlaybackEvents.OnClearPlaybackSurface += ClearPlaybackSurfaceForMediaSwitch;

            VlcPlaybackBridge.SetSubtitleRenderMode(SubtitleRenderMode);
            VlcPlaybackBridge.SetSubtitleSurfacePolicy(ShouldStackSubtitlesOutside());
        }

        private void OnDestroy()
        {
            DisableAndDetachSubtitleSurface(false);
            if (videoScreen != null)
                videoScreen.DestroyFlatSubtitleLayer();

            // 取消订阅
            VlcPlaybackEvents.OnMediaParseFinished -= HandleMediaParseFinished;
            VlcPlaybackEvents.OnVideoSizeChanged -= OnVideoSizeChanged;
            VlcPlaybackEvents.OnStateChanged -= OnStateChanged;
            VlcPlaybackEvents.OnTimeChanged -= HandleTimeChanged;
            VlcPlaybackEvents.OnLengthChanged -= HandleLengthChanged;
            VlcPlaybackEvents.OnBuffering -= HandleBuffering;
            VlcPlaybackEvents.OnAudioTracksChanged -= HandleAudioTracksChanged;
            VlcPlaybackEvents.OnSubtitleTracksChanged -= HandleSubtitleTracksChanged;
            VlcPlaybackEvents.OnPlayRequested -= LoadAndPlay;
            VlcPlaybackEvents.OnClearPlaybackSurface -= ClearPlaybackSurfaceForMediaSwitch;
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
            _currentVisibleWidth = 0;
            _currentVisibleHeight = 0;

            // 2. 告诉 AAR 预加载 URL
            // 获取暂存的 JSON 字符串，如果为空则直接使用 URI
            string payload = !string.IsNullOrEmpty(extraData) ? extraData : path;
            bool payloadIsJson = payload != null && payload.TrimStart().StartsWith("{", StringComparison.Ordinal);
            Debug.Log($"[PlaybackService] 最终传给 AAR 的 payload: {payload}");
            SurfaceDebug(
                $"play_video preload path={RedactForLog(path)} current={RedactForLog(CurrentMedia?.Uri)} " +
                $"start={startTimeMs} payloadIsJson={payloadIsJson}");
            VlcPlaybackBridge.PreloadLocation(payload);
        }

        /// <summary>
        /// 由 preloadLocation 解析完成后触发。携带 libvlc 读出的 projection，
        /// 是 SetGeometry 的权威来源。替代旧的 OnVideoSizeChanged 首次回调。
        /// </summary>
        private void HandleMediaParseFinished(VlcMediaParseResult result)
        {
            if (result == null)
            {
                SurfaceDebug("parse_finished ignored: result=null");
                return;
            }

            string normalizedCallbackUri = NormalizeMediaUri(result.uri);
            string normalizedCurrentUri = NormalizeMediaUri(CurrentMedia?.Uri);
            bool isCurrentUri = string.Equals(normalizedCurrentUri, normalizedCallbackUri, StringComparison.Ordinal);
            SurfaceDebug(
                $"parse_finished received uriMatch={isCurrentUri} callback={RedactForLog(result.uri)} " +
                $"current={RedactForLog(CurrentMedia?.Uri)} raw={result.width}x{result.height} " +
                $"visible={result.visibleWidth}x{result.visibleHeight} projection={result.projection} " +
                $"duration={result.duration} state={FormatSurfaceState()}");

            if (!isCurrentUri)
            {
                Debug.Log(
                    $"[PlaybackService] Ignoring media parse callback for stale uri: callback={result.uri}, current={CurrentMedia?.Uri}");
                SurfaceDebug(
                    $"parse_finished ignored stale normalizedCallback={RedactForLog(normalizedCallbackUri)} " +
                    $"normalizedCurrent={RedactForLog(normalizedCurrentUri)}");
                return;
            }

            VlcVideoSize videoSize = result.ToVideoSize();
            int width = videoSize.Width;
            int height = videoSize.Height;

            if (!videoSize.IsValid)
            {
                Debug.LogWarning($"[PlaybackService] HandleMediaParseFinished: 无效尺寸 raw={width}x{height}, visible={videoSize.VisibleWidth}x{videoSize.VisibleHeight}");
                SurfaceDebug($"parse_finished ignored invalid_size raw={width}x{height} visible={videoSize.VisibleWidth}x{videoSize.VisibleHeight}");
                return;
            }

            SetCurrentVideoSize(videoSize);

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

            Debug.Log($"[PlaybackService] OnMediaParseFinished: raw={width}x{height}, content={videoSize.ContentWidth}x{videoSize.ContentHeight}, projection={result.projection}, duration={result.duration}ms");
            SurfaceDebug($"parse_finished accepted content={videoSize.ContentWidth}x{videoSize.ContentHeight} before_rebuild state={FormatSurfaceState()}");
            RebuildAndApplyGeometry(videoSize);
        }

        /// <summary>
        /// 由 onNewVideoLayout 触发，用于解码器实际输出与静态解析不一致时的二次矫正。
        /// 此时 CurrentMedia.Projection 已由 HandleMediaParseFinished 更新，SetGeometry 结果一致。
        /// </summary>
        private void OnVideoSizeChanged(VlcVideoSize videoSize)
        {
            SurfaceDebug(
                $"layout_callback received valid={videoSize.IsValid} raw={videoSize.Width}x{videoSize.Height} " +
                $"visible={videoSize.VisibleWidth}x{videoSize.VisibleHeight} content={videoSize.ContentWidth}x{videoSize.ContentHeight} " +
                $"state={FormatSurfaceState()}");

            if (!videoSize.IsValid)
            {
                SurfaceDebug("layout_callback ignored invalid_size");
                return;
            }

            VlcVideoSize previousSize = CurrentVideoSize;
            VideoGeometrySelection nextGeometry = ResolveRequestedGeometry();
            VideoSurfaceSpec nextVideoSpec = CreateVideoSurfaceSpec(videoSize, nextGeometry);
            bool contentUnchanged = previousSize.IsValid && HasSameContentDimensions(previousSize, videoSize);
            bool geometryUnchanged = HasSameGeometry(_currentGeometrySelection, nextGeometry);
            bool surfaceAlreadyCurrent = _geometryBindingCoroutine != null || IsVideoSurfaceCurrent(nextVideoSpec);
            SurfaceDebug(
                $"layout_callback decision contentUnchanged={contentUnchanged} geometryUnchanged={geometryUnchanged} " +
                $"surfaceAlreadyCurrent={surfaceAlreadyCurrent} coroutineActive={_geometryBindingCoroutine != null} " +
                $"nextProjection={nextGeometry.Projection} nextStereo={nextGeometry.Stereo} state={FormatSurfaceState()}");

            SetCurrentVideoSize(videoSize);

            if (contentUnchanged && geometryUnchanged && surfaceAlreadyCurrent)
            {
                if (!previousSize.HasSameDimensions(videoSize))
                {
                    Debug.Log(
                        $"[PlaybackService] OnVideoSizeChanged ignored raw layout padding change: " +
                        $"raw={videoSize.Width}x{videoSize.Height}, content={videoSize.ContentWidth}x{videoSize.ContentHeight}");
                }
                SurfaceDebug("layout_callback ignored already_current");
                return;
            }

            Debug.Log($"[PlaybackService] OnVideoSizeChanged (矫正): raw={videoSize.Width}x{videoSize.Height}, content={videoSize.ContentWidth}x{videoSize.ContentHeight}");
            SurfaceDebug("layout_callback accepted before_rebuild");
            RebuildAndApplyGeometry(videoSize);
        }

        private void RebuildAndApplyGeometry(VlcVideoSize videoSize)
        {
            if (_geometryService == null && videoScreen != null)
                _geometryService = CreateGeometryService(videoScreen);
            if (_geometryService != null)
            {
                _currentGeometrySelection = ResolveRequestedGeometry();
                if (_geometryBindingCoroutine != null)
                {
                    SurfaceDebug("rebuild_geometry stopping_previous_coroutine");
                    StopCoroutine(_geometryBindingCoroutine);
                }
                SurfaceDebug(
                    $"rebuild_geometry start content={videoSize.ContentWidth}x{videoSize.ContentHeight} " +
                    $"projection={_currentGeometrySelection.Projection} stereo={_currentGeometrySelection.Stereo} " +
                    $"state={FormatSurfaceState()}");
                _geometryBindingCoroutine = StartCoroutine(RebuildGeometryAndSubtitleSurface(videoSize));
            }
            else
            {
                SurfaceDebug($"rebuild_geometry skipped geometryService=null videoScreenNull={videoScreen == null}");
            }
        }

        private VideoScreenGeometryService CreateGeometryService(VideoScreen screen)
        {
            return new VideoScreenGeometryService(
                screen,
                () => CurrentMedia,
                () => UseHardwareDecoding,
                GetManualGeometryOverride,
                () => CurrentVideoScaleMode,
                () => CurrentVideoAspectRatio);
        }

        private IEnumerator RebuildGeometryAndSubtitleSurface(VlcVideoSize videoSize)
        {
            CancelNativeSubtitleSurfaceCoroutine();
            _currentGeometrySelection = ResolveRequestedGeometry();
            VideoSurfaceSpec videoSpec = CreateVideoSurfaceSpec(videoSize, _currentGeometrySelection);
            bool shouldRebuildVideoSurface = ShouldRebuildVideoSurface(videoSpec);
            SurfaceDebug(
                $"surface_rebuild begin shouldRebuildVideo={shouldRebuildVideoSurface} " +
                $"videoSpec={videoSpec.ContentWidth}x{videoSpec.ContentHeight}/{videoSpec.Projection}/{videoSpec.Stereo}/{videoSpec.CurveMode} " +
                $"state={FormatSurfaceState()}");
            if (shouldRebuildVideoSurface)
            {
                if (_videoSurfaceBoundToVlc)
                {
                    SurfaceDebug("surface_rebuild detaching_previous_video_surface");
                    VlcPlaybackBridge.DetachSurface();
                    _videoSurfaceBoundToVlc = false;
                }

                _geometryService.Rebuild(videoSize);
                SurfaceDebug($"surface_rebuild after_geometry_rebuild state={FormatSurfaceState()}");
            }
            else
            {
                Debug.Log(
                    $"[PlaybackService] Video surface rebuild skipped: content={videoSpec.ContentWidth}x{videoSpec.ContentHeight}, " +
                    $"projection={videoSpec.Projection}, stereo={videoSpec.Stereo}, curve={videoSpec.CurveMode}");
            }

            VlcPlaybackBridge.SetSubtitleSurfacePolicy(ShouldStackSubtitlesOutside());
            bool shouldBindSubtitleSurface = BeginNativeSubtitleSurfaceRebuild(out SubtitleSurfaceSpec subtitleSpec, out _);
            SurfaceDebug(
                $"surface_rebuild before_wait shouldBindSubtitle={shouldBindSubtitleSurface} " +
                $"state={FormatSurfaceState()}");

            yield return WaitForVideoAndSubtitleSurfaces(shouldRebuildVideoSurface, shouldBindSubtitleSurface);
            SurfaceDebug($"surface_rebuild after_wait state={FormatSurfaceState()}");

            BindVideoSurface(videoSpec);
            if (shouldBindSubtitleSurface)
                BindSubtitleSurface(subtitleSpec);

            _geometryBindingCoroutine = null;
        }

        private IEnumerator WaitForVideoAndSubtitleSurfaces(bool shouldWaitForVideoSurface, bool shouldBindSubtitleSurface)
        {
            const int maxRetries = 50;
            int retries = 0;

            while (videoScreen != null
                   && retries < maxRetries
                   && ((shouldWaitForVideoSurface && !videoScreen.IsHardwareSurfaceReady())
                       || (shouldBindSubtitleSurface && !videoScreen.IsFlatSubtitleSurfaceReady())))
            {
                retries++;
                if (retries % 10 == 0)
                {
                    Debug.Log(
                        $"[PlaybackService] Waiting for video/subtitle surfaces... retry: {retries}/{maxRetries}, " +
                        $"videoRequired={shouldWaitForVideoSurface}, videoReady={videoScreen.IsHardwareSurfaceReady()}, " +
                        $"subtitleRequired={shouldBindSubtitleSurface}, " +
                        $"subtitleReady={(!shouldBindSubtitleSurface || videoScreen.IsFlatSubtitleSurfaceReady())}");
                }
                yield return null;
            }

            if (videoScreen == null)
                yield break;

            if (shouldWaitForVideoSurface && !videoScreen.IsHardwareSurfaceReady())
                Debug.LogError($"[PlaybackService] Video surface is not ready after parallel wait. retries={retries}");

            if (shouldBindSubtitleSurface && !videoScreen.IsFlatSubtitleSurfaceReady())
                Debug.LogError($"[PlaybackService] Subtitle surface is not ready after parallel wait. retries={retries}");

            SurfaceDebug(
                $"surface_wait finished retries={retries} videoRequired={shouldWaitForVideoSurface} " +
                $"subtitleRequired={shouldBindSubtitleSurface} state={FormatSurfaceState()}");
        }

        private void BindVideoSurface(VideoSurfaceSpec videoSpec)
        {
            if (videoScreen == null)
                return;

            IntPtr videoSurfacePtr = videoScreen.GetHardwareSurfaceHandle();
            SurfaceDebug(
                $"bind_video enter surface={videoSurfacePtr} spec={videoSpec.ContentWidth}x{videoSpec.ContentHeight}/" +
                $"{videoSpec.Projection}/{videoSpec.Stereo}/{videoSpec.CurveMode} state={FormatSurfaceState()}");
            if (videoSurfacePtr == IntPtr.Zero)
            {
                Debug.LogError("[PlaybackService] Skipping VLC surface bind because video surface is not ready.");
                SurfaceDebug("bind_video skipped zero_surface");
                return;
            }

            VlcPlaybackBridge.SetSurface(videoSurfacePtr);
            _boundVideoSurfaceSpec = videoSpec;
            _videoSurfaceBoundToVlc = true;
            Debug.Log($"[PlaybackService] Bound video surface: surface={videoSurfacePtr}, content={videoSpec.ContentWidth}x{videoSpec.ContentHeight}, projection={videoSpec.Projection}");
            SurfaceDebug($"bind_video done surface={videoSurfacePtr} state={FormatSurfaceState()}");
        }

        private void BindSubtitleSurface(SubtitleSurfaceSpec subtitleSpec)
        {
            if (videoScreen == null)
                return;

            if (_subtitleSurfaceBoundToVlc
                && _boundSubtitleSurfaceSpec.HasValue
                && _boundSubtitleSurfaceSpec.Value.Equals(subtitleSpec)
                && videoScreen.IsFlatSubtitleSurfaceReady())
            {
                Debug.Log($"[PlaybackService] Subtitle surface bind skipped; current spec already bound: surface={videoScreen.GetFlatSubtitleSurfaceHandle()}");
                return;
            }

            IntPtr subtitleSurfacePtr = videoScreen.GetFlatSubtitleSurfaceHandle();
            SurfaceDebug(
                $"bind_subtitle enter surface={subtitleSurfacePtr} spec={subtitleSpec.SurfaceWidth}x{subtitleSpec.SurfaceHeight} " +
                $"mode={subtitleSpec.RenderMode} state={FormatSurfaceState()}");
            if (subtitleSurfacePtr == IntPtr.Zero)
            {
                Debug.LogError("[PlaybackService] Subtitle surface was requested but is not ready.");
                SurfaceDebug("bind_subtitle skipped zero_surface");
                DisableAndDetachSubtitleSurface(false);
                return;
            }

            VlcPlaybackBridge.SetSubtitleSurface(subtitleSurfacePtr);
            VlcPlaybackBridge.SetSubtitleSurfaceEnabled(true);
            _boundSubtitleSurfaceSpec = subtitleSpec;
            _subtitleSurfaceBoundToVlc = true;
            Debug.Log($"[PlaybackService] Bound subtitle surface: surface={subtitleSurfacePtr}, mode={subtitleSpec.RenderMode}, content={subtitleSpec.ContentWidth}x{subtitleSpec.ContentHeight}");
            SurfaceDebug($"bind_subtitle done surface={subtitleSurfacePtr} state={FormatSurfaceState()}");
        }

        private void SetCurrentVideoSize(VlcVideoSize videoSize)
        {
            _currentWidth = videoSize.Width;
            _currentHeight = videoSize.Height;
            _currentVisibleWidth = videoSize.VisibleWidth;
            _currentVisibleHeight = videoSize.VisibleHeight;
        }

        private bool IsCurrentMediaUri(string callbackUri)
        {
            return CurrentMedia != null
                && string.Equals(NormalizeMediaUri(CurrentMedia.Uri), NormalizeMediaUri(callbackUri), StringComparison.Ordinal);
        }

        private static string NormalizeMediaUri(string uri)
        {
            if (string.IsNullOrEmpty(uri))
                return string.Empty;

            try
            {
                return Uri.UnescapeDataString(uri).Trim();
            }
            catch (UriFormatException)
            {
                return uri.Trim();
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
                case "Ended": HandleStatusChanged(PlayerStatus.Ended); break;
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
            if (CurrentStatus == PlayerStatus.Paused)
                return;

            OnBuffering?.Invoke(buffering);

            if (buffering < 100f) HandleStatusChanged(PlayerStatus.Buffering);
            else HandleStatusChanged(PlayerStatus.Playing);
        }

        private void HandleAudioTracksChanged(List<TrackInfo> tracks)
        {
            OnAudioTracksChanged?.Invoke(null);
        }

        private void HandleSubtitleTracksChanged(List<TrackInfo> tracks)
        {
            OnSubtitleTracksChanged?.Invoke(null);
        }

        private void StopInternal()
        {
            _currentWidth = 0;
            _currentHeight = 0;
            _currentVisibleWidth = 0;
            _currentVisibleHeight = 0;
            _isShortcutFastRate = false;
            VlcPlaybackBridge.PublishPlaybackRate(1f);
            
            DisableAndDetachSubtitleSurface(false);
            VlcPlaybackBridge.Stop();
            VlcPlaybackBridge.DetachSurface();
            _videoSurfaceBoundToVlc = false;
            _boundVideoSurfaceSpec = null;

            if (videoScreen != null)
            {
                videoScreen.DestroyFlatSubtitleLayer();
                videoScreen.DestroyLayer();
            }

            HandleStatusChanged(PlayerStatus.Stopped);
        }

        private void ClearPlaybackSurfaceForMediaSwitch()
        {
            Debug.Log("[PlaybackService] Clearing Unity playback surfaces before media switch.");
            ClearPendingSurfaceBindingCoroutines();
            _currentWidth = 0;
            _currentHeight = 0;
            _currentVisibleWidth = 0;
            _currentVisibleHeight = 0;

            DisableAndDetachSubtitleSurface(false);
            VlcPlaybackBridge.DetachSurface();
            _videoSurfaceBoundToVlc = false;
            _boundVideoSurfaceSpec = null;

            if (videoScreen != null)
            {
                videoScreen.DestroyFlatSubtitleLayer();
                videoScreen.DestroyLayer();
            }
        }

        private void ClearPendingSurfaceBindingCoroutines()
        {
            if (_geometryBindingCoroutine != null)
            {
                StopCoroutine(_geometryBindingCoroutine);
                _geometryBindingCoroutine = null;
            }

            CancelNativeSubtitleSurfaceCoroutine();
        }

        // --------------------------------------------------------
        // 视图与生命周期绑定
        // --------------------------------------------------------
        public void AttachVideoScreen(VideoScreen screen)
        {
            videoScreen = screen;
            if (videoScreen != null)
            {
                videoScreen.SetVideoLayout(CurrentVideoScaleMode, CurrentVideoAspectRatio);
                _geometryService = CreateGeometryService(videoScreen);
                videoScreen.RebuildLayer(UseHardwareDecoding);
                _boundVideoSurfaceSpec = null;
                _videoSurfaceBoundToVlc = false;
            }
        }

        public void DetachVideoScreen()
        {
            DisableAndDetachSubtitleSurface(false);
            videoScreen?.DestroyFlatSubtitleLayer();
            videoScreen = null;
            _geometryService = null;
            _boundVideoSurfaceSpec = null;
            _videoSurfaceBoundToVlc = false;
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
            ResetShortcutPlaybackState(item);
            OnMediaChanged?.Invoke(item, 0); // Position is not accurate but UI doesn't strictly need it

            // Apply projection early (scene layout: hide/show background board, sphere centering)
            // when the Android side has already told us the projection type.
            // OnMediaParseFinished will do the authoritative pass once dimensions are known.
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

        public void SetVideoScaleMode(VideoScaleMode mode)
        {
            CurrentVideoScaleMode = mode;
            PlaybackUiSettingsService.SaveVideoScaleMode(mode);
            VlcPlaybackBridge.SetVideoScaleOrdinal(PlaybackUiSettingsService.ToLegacyScaleOrdinal(mode));
            videoScreen?.SetVideoLayout(CurrentVideoScaleMode, CurrentVideoAspectRatio);
        }

        public void SetVideoAspectRatio(VideoAspectRatio aspectRatio)
        {
            CurrentVideoScaleMode = VideoScaleMode.Fit;
            CurrentVideoAspectRatio = aspectRatio;
            PlaybackUiSettingsService.SaveVideoAspectRatio(aspectRatio);
            videoScreen?.SetVideoLayout(CurrentVideoScaleMode, CurrentVideoAspectRatio);
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

            if (CurrentVideoSize.IsValid)
            {
                if (ShouldRebuildForManualGeometryChange(previousGeometry, nextGeometry))
                {
                    RebuildAndApplyGeometry(CurrentVideoSize);
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
            videoScreen.FitVideoSize((uint)CurrentVideoSize.ContentWidth, (uint)CurrentVideoSize.ContentHeight);
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
            rate = NormalizePlaybackRate(rate);
            _isShortcutFastRate = Mathf.Approximately(rate, ShortcutFastRate);
            VlcPlaybackBridge.SetRate(rate);
            VlcPlaybackBridge.PublishPlaybackRate(rate);
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

        public TrackSnapshot GetTrackSnapshotFromVlc()
        {
            return VlcPlaybackBridge.GetTrackSnapshot();
        }

        public TrackSnapshot GetAudioTrackSnapshotFromVlc()
        {
            return VlcPlaybackBridge.GetAudioTrackSnapshot();
        }

        public TrackSnapshot GetSubtitleTrackSnapshotFromVlc()
        {
            return VlcPlaybackBridge.GetSubtitleTrackSnapshot();
        }

        public bool HasCurrentMediaOrPlaylistItems()
        {
            if (CurrentMedia != null)
                return true;

            string playlistJson = VlcPlaybackBridge.GetPlaylist();
            return VlcPlaybackPayloadParser.ParsePlaylist(playlistJson).Count > 0;
        }

        public TrackSnapshot SetAudioTrack(string trackId)
        {
            return VlcPlaybackBridge.SetAudioTrackAndGetSnapshot(trackId);
        }

        public TrackSnapshot SetSubtitleTrack(string trackId)
        {
            return VlcPlaybackBridge.SetSpuTrackAndGetSnapshot(trackId);
        }

        public void SetSubtitleRenderMode(SubtitleRenderMode mode)
        {
            SubtitleRenderMode = mode;
            VlcPlaybackBridge.SetSubtitleRenderMode(mode);
            UpdateNativeSubtitleSurfaceBinding();

        }

        private void UpdateNativeSubtitleSurfaceBinding()
        {
            Debug.Log(
                $"[PlaybackService] Flat subtitle surface binding requested: mode={SubtitleRenderMode}, " +
                $"projection={CurrentGeometrySelection.Projection}, rawSize={_currentWidth}x{_currentHeight}, contentSize={CurrentVideoSize.ContentWidth}x{CurrentVideoSize.ContentHeight}, " +
                $"outside={RenderSubtitlesOutsideScreen}, videoScreenNull={videoScreen == null}, media={(CurrentMedia != null ? CurrentMedia.Title : "null")}");

            CancelNativeSubtitleSurfaceCoroutine();
            _nativeSubtitleSurfaceCoroutine = StartCoroutine(UpdateNativeSubtitleSurfaceBindingRoutine());
        }

        private IEnumerator UpdateNativeSubtitleSurfaceBindingRoutine()
        {
            Debug.Log("[PlaybackService] Flat subtitle surface binding start");
            VlcPlaybackBridge.SetSubtitleSurfacePolicy(ShouldStackSubtitlesOutside());
            bool shouldBindSubtitleSurface = BeginNativeSubtitleSurfaceRebuild(out SubtitleSurfaceSpec subtitleSpec, out _);
            if (!shouldBindSubtitleSurface)
            {
                _nativeSubtitleSurfaceCoroutine = null;
                yield break;
            }

            yield return WaitForSubtitleSurfaceReady();

            BindSubtitleSurface(subtitleSpec);
            _nativeSubtitleSurfaceCoroutine = null;
        }

        private bool BeginNativeSubtitleSurfaceRebuild(out SubtitleSurfaceSpec subtitleSpec, out bool rebuiltSurface)
        {
            subtitleSpec = default(SubtitleSurfaceSpec);
            rebuiltSurface = false;
            bool usesFlatSubtitleSurface = TryCreateSubtitleSurfaceSpec(out subtitleSpec);

            Debug.Log(
                $"[PlaybackService] Flat subtitle surface binding state: renderModeAllowed={UsesFlatSubtitleSurfaceMode(SubtitleRenderMode)}, " +
                $"usesFlatSubtitleSurface={usesFlatSubtitleSurface}, outside={RenderSubtitlesOutsideScreen}, " +
                $"videoScreenNull={videoScreen == null}, projection={CurrentGeometrySelection.Projection}, rawSize={_currentWidth}x{_currentHeight}, contentSize={CurrentVideoSize.ContentWidth}x{CurrentVideoSize.ContentHeight}");

            if (videoScreen == null
                || !usesFlatSubtitleSurface
                || !CurrentVideoSize.IsValid)
            {
                Debug.LogWarning(
                    $"[PlaybackService] Flat subtitle surface binding skipped: videoScreenNull={videoScreen == null}, " +
                    $"usesFlatSubtitleSurface={usesFlatSubtitleSurface}, mode={SubtitleRenderMode}, " +
                    $"projection={CurrentGeometrySelection.Projection}, rawSize={_currentWidth}x{_currentHeight}, contentSize={CurrentVideoSize.ContentWidth}x{CurrentVideoSize.ContentHeight}");
                DisableAndDetachSubtitleSurface(false);
                videoScreen?.DestroyFlatSubtitleLayer();
                return false;
            }

            if (IsSubtitleSurfaceCurrent(subtitleSpec))
            {
                Debug.Log(
                    $"[PlaybackService] Flat subtitle layer rebuild skipped: surfaceSize={subtitleSpec.SurfaceWidth}x{subtitleSpec.SurfaceHeight}, " +
                    $"contentSize={subtitleSpec.ContentWidth}x{subtitleSpec.ContentHeight}, mode={subtitleSpec.RenderMode}, outside={subtitleSpec.RenderOutsideScreen}");
                return true;
            }

            VlcPlaybackBridge.SetSubtitleSurfaceEnabled(false);
            VlcPlaybackBridge.DetachSubtitleSurface();
            _subtitleSurfaceBoundToVlc = false;
            _boundSubtitleSurfaceSpec = null;
            Debug.Log(
                $"[PlaybackService] Flat subtitle layer rebuild requested: rawSize={_currentWidth}x{_currentHeight}, contentSize={CurrentVideoSize.ContentWidth}x{CurrentVideoSize.ContentHeight}, " +
                $"mode={SubtitleRenderMode}, projection={CurrentGeometrySelection.Projection}, outside={RenderSubtitlesOutsideScreen}");

            if (!videoScreen.RebuildFlatSubtitleLayer(
                    subtitleSpec.SurfaceWidth,
                    subtitleSpec.SurfaceHeight,
                    subtitleSpec.ContentWidth,
                    subtitleSpec.ContentHeight,
                    subtitleSpec.RenderOutsideScreen))
            {
                Debug.LogError(
                    $"[PlaybackService] Flat subtitle layer rebuild failed: mode={SubtitleRenderMode}, " +
                    $"projection={CurrentGeometrySelection.Projection}, size={subtitleSpec.SurfaceWidth}x{subtitleSpec.SurfaceHeight}, " +
                    $"outside={RenderSubtitlesOutsideScreen}");
                return false;
            }

            rebuiltSurface = true;
            return true;
        }

        private IEnumerator WaitForSubtitleSurfaceReady()
        {
            const int maxRetries = 50;
            int retries = 0;
            while (videoScreen != null && !videoScreen.IsFlatSubtitleSurfaceReady() && retries < maxRetries)
            {
                retries++;
                if (retries % 10 == 0)
                    Debug.Log($"[PlaybackService] Waiting for flat subtitle surface... retry: {retries}/{maxRetries}");
                yield return null;
            }

            if (videoScreen == null || !videoScreen.IsFlatSubtitleSurfaceReady())
                Debug.LogError($"[PlaybackService] Flat subtitle overlay surface is not ready. retries={retries}");
        }

        private void CancelNativeSubtitleSurfaceCoroutine()
        {
            if (_nativeSubtitleSurfaceCoroutine == null)
                return;

            Debug.Log("[PlaybackService] Flat subtitle surface binding: stopping previous binding coroutine");
            StopCoroutine(_nativeSubtitleSurfaceCoroutine);
            _nativeSubtitleSurfaceCoroutine = null;
        }

        private bool TryCreateSubtitleSurfaceSpec(out SubtitleSurfaceSpec spec)
        {
            spec = default(SubtitleSurfaceSpec);
            if (videoScreen == null
                || !UsesFlatSubtitleSurfaceMode(SubtitleRenderMode)
                || !CurrentVideoSize.IsValid)
                return false;

            uint contentWidth = (uint)CurrentVideoSize.ContentWidth;
            uint contentHeight = (uint)CurrentVideoSize.ContentHeight;
            uint surfaceWidth = contentWidth;
            uint surfaceHeight = UsesSingleHeightSubtitleSurface()
                ? contentHeight
                : contentHeight * 2u;

            spec = new SubtitleSurfaceSpec(
                surfaceWidth,
                surfaceHeight,
                contentWidth,
                contentHeight,
                CurrentGeometrySelection.Stereo,
                RenderSubtitlesOutsideScreen,
                ShouldStackSubtitlesOutside(),
                SubtitleRenderMode);
            return true;
        }

        private bool ShouldRebuildVideoSurface(VideoSurfaceSpec spec)
        {
            return !IsVideoSurfaceCurrent(spec);
        }

        private bool IsVideoSurfaceCurrent(VideoSurfaceSpec spec)
        {
            return videoScreen != null
                && videoScreen.IsHardwareSurfaceReady()
                && _boundVideoSurfaceSpec.HasValue
                && _boundVideoSurfaceSpec.Value.Equals(spec);
        }

        private bool IsSubtitleSurfaceCurrent(SubtitleSurfaceSpec spec)
        {
            return videoScreen != null
                && videoScreen.IsFlatSubtitleSurfaceReady()
                && _boundSubtitleSurfaceSpec.HasValue
                && _boundSubtitleSurfaceSpec.Value.Equals(spec);
        }

        private VideoSurfaceSpec CreateVideoSurfaceSpec(VlcVideoSize videoSize, VideoGeometrySelection geometry)
        {
            return new VideoSurfaceSpec(
                (uint)videoSize.ContentWidth,
                (uint)videoSize.ContentHeight,
                geometry.Projection,
                geometry.Stereo,
                geometry.CurveMode,
                UseHardwareDecoding);
        }

        private void DisableAndDetachSubtitleSurface(bool destroyLayer)
        {
            VlcPlaybackBridge.SetSubtitleSurfaceEnabled(false);
            VlcPlaybackBridge.DetachSubtitleSurface();
            _subtitleSurfaceBoundToVlc = false;
            _boundSubtitleSurfaceSpec = null;
            if (destroyLayer)
                videoScreen?.DestroyFlatSubtitleLayer();
        }

        private static bool HasSameContentDimensions(VlcVideoSize a, VlcVideoSize b)
        {
            return a.ContentWidth == b.ContentWidth
                && a.ContentHeight == b.ContentHeight;
        }

        private static bool HasSameGeometry(VideoGeometrySelection a, VideoGeometrySelection b)
        {
            return a.Projection == b.Projection
                && a.Stereo == b.Stereo
                && a.CurveMode == b.CurveMode;
        }

        private static bool UsesFlatSubtitleSurfaceMode(SubtitleRenderMode mode)
        {
            return mode == SubtitleRenderMode.Spatial;
        }

        private bool UsesSingleHeightSubtitleSurface()
        {
            return (CurrentGeometrySelection.Projection == VideoProjection.Flat
                    || CurrentGeometrySelection.Projection == VideoProjection.Cylinder)
                && !RenderSubtitlesOutsideScreen;
        }

        private bool ShouldStackSubtitlesOutside()
        {
            if (IsImmersiveProjection(CurrentGeometrySelection.Projection))
                return true;
            return RenderSubtitlesOutsideScreen;
        }

        private static bool IsImmersiveProjection(VideoProjection projection)
        {
            return projection == VideoProjection.Sphere360 || projection == VideoProjection.Sphere180;
        }

        public void SetRenderSubtitlesOutsideScreen(bool enabled)
        {
            if (RenderSubtitlesOutsideScreen == enabled)
            {
                VlcPlaybackBridge.SetSubtitleSurfacePolicy(ShouldStackSubtitlesOutside());
                return;
            }

            RenderSubtitlesOutsideScreen = enabled;
            VlcPlaybackBridge.SetSubtitleSurfacePolicy(ShouldStackSubtitlesOutside());
            UpdateNativeSubtitleSurfaceBinding();
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
            TrackSnapshot snapshot = GetSubtitleTrackSnapshotFromVlc();
            string currentTrack = FindSelectedTrackId(snapshot.SubtitleTracks, DisabledSubtitleTrack);
            bool subtitleEnabled = currentTrack != DisabledSubtitleTrack;

            if (subtitleEnabled)
            {
                _trackSelectionService.RememberSubtitleTrack(currentTrack);
                SetSubtitleTrack(DisabledSubtitleTrack);
                return;
            }

            string trackToRestore = FindRestorableSubtitleTrack(snapshot.SubtitleTracks);
            if (trackToRestore != DisabledSubtitleTrack)
                SetSubtitleTrack(trackToRestore);
        }

        /// <summary>
        /// 切换媒体时重置快捷播放状态，并记录当前媒体的字幕轨。
        /// </summary>
        private void ResetShortcutPlaybackState(MediaWrapper media)
        {
            _isShortcutFastRate = false;
            VlcPlaybackBridge.PublishPlaybackRate(1f);
            _trackSelectionService.Reset(media);
        }

        private static float NormalizePlaybackRate(float rate)
        {
            return float.IsNaN(rate) || float.IsInfinity(rate) || rate <= 0f ? 1f : rate;
        }

        public void SetSubtitleDelaySeconds(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds))
                seconds = 0f;

            float snapped = Mathf.Round(seconds * 2f) * 0.5f;
            SubtitleDelaySeconds = snapped;
            VlcPlaybackBridge.SetSubtitleDelayMicroseconds((long)(snapped * 1000000f));
        }

        /// <summary>
        /// 优先恢复关闭前的字幕轨；如果不可用，则选择第一个有效字幕轨。
        /// </summary>
        private string FindRestorableSubtitleTrack(List<TrackInfo> tracks)
        {
            return _trackSelectionService.FindRestorableSubtitleTrack(tracks);
        }

        private static string FindSelectedTrackId(List<TrackInfo> tracks, string fallback)
        {
            if (tracks == null)
                return fallback;

            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].IsSelected)
                    return tracks[i].Id;
            }

            return fallback;
        }

        public void SetAudioDelay(long delayMs) { /* Audio delay is not exposed in the Unity settings surface. */ }
        public void SetSubtitleDelay(long delayMs)
        {
            SetSubtitleDelaySeconds(delayMs / 1000f);
        }

        private readonly struct VideoSurfaceSpec : IEquatable<VideoSurfaceSpec>
        {
            public VideoSurfaceSpec(
                uint contentWidth,
                uint contentHeight,
                VideoProjection projection,
                StereoMode stereo,
                FlatVideoCurveMode curveMode,
                bool hardwareDecoding)
            {
                ContentWidth = contentWidth;
                ContentHeight = contentHeight;
                Projection = projection;
                Stereo = stereo;
                CurveMode = curveMode;
                HardwareDecoding = hardwareDecoding;
            }

            public uint ContentWidth { get; }
            public uint ContentHeight { get; }
            public VideoProjection Projection { get; }
            public StereoMode Stereo { get; }
            public FlatVideoCurveMode CurveMode { get; }
            public bool HardwareDecoding { get; }

            public bool Equals(VideoSurfaceSpec other)
            {
                return ContentWidth == other.ContentWidth
                    && ContentHeight == other.ContentHeight
                    && Projection == other.Projection
                    && Stereo == other.Stereo
                    && CurveMode == other.CurveMode
                    && HardwareDecoding == other.HardwareDecoding;
            }
        }

        private readonly struct SubtitleSurfaceSpec : IEquatable<SubtitleSurfaceSpec>
        {
            public SubtitleSurfaceSpec(
                uint surfaceWidth,
                uint surfaceHeight,
                uint contentWidth,
                uint contentHeight,
                StereoMode stereo,
                bool renderOutsideScreen,
                bool stackOutside,
                SubtitleRenderMode renderMode)
            {
                SurfaceWidth = surfaceWidth;
                SurfaceHeight = surfaceHeight;
                ContentWidth = contentWidth;
                ContentHeight = contentHeight;
                Stereo = stereo;
                RenderOutsideScreen = renderOutsideScreen;
                StackOutside = stackOutside;
                RenderMode = renderMode;
            }

            public uint SurfaceWidth { get; }
            public uint SurfaceHeight { get; }
            public uint ContentWidth { get; }
            public uint ContentHeight { get; }
            public StereoMode Stereo { get; }
            public bool RenderOutsideScreen { get; }
            public bool StackOutside { get; }
            public SubtitleRenderMode RenderMode { get; }

            public bool Equals(SubtitleSurfaceSpec other)
            {
                return SurfaceWidth == other.SurfaceWidth
                    && SurfaceHeight == other.SurfaceHeight
                    && ContentWidth == other.ContentWidth
                    && ContentHeight == other.ContentHeight
                    && Stereo == other.Stereo
                    && RenderOutsideScreen == other.RenderOutsideScreen
                    && StackOutside == other.StackOutside
                    && RenderMode == other.RenderMode;
            }
        }
    }
}
