using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XRVLC.Infrastructure.Pico;
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
        private const float SurfaceRebuildPauseTimeoutSeconds = 1.5f;
        private const int CodecBufferAlignmentPixels = 16;

        public static PlaybackService Instance { get; private set; }

        public bool UseHardwareDecoding { get; private set; } = true;
        public SubtitleRenderMode SubtitleRenderMode { get; private set; } = SubtitleRenderMode.Spatial;
        public bool RenderSubtitlesOutsideScreen { get; private set; }
        public float SubtitleDelaySeconds { get; private set; }
        public VideoGeometrySelection CurrentGeometrySelection => _currentGeometrySelection;
        public ChromaKeySettings CurrentChromaKeySettings => _chromaKeySettings;
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
        public event Action<ChromaKeySettings> OnChromaKeySettingsChanged;
        public event Action<bool> OnChromaKeyColorExtractionCompleted;
#pragma warning restore 0067

        // --- PlayerController 状态 ---
        private int _currentWidth = 0;
        private int _currentHeight = 0;
        private int _currentVisibleWidth = 0;
        private int _currentVisibleHeight = 0;
        private readonly TrackSelectionService _trackSelectionService = new TrackSelectionService();
        private bool _isShortcutFastRate;
        private VideoScreenGeometryService _geometryService;
        private bool _hasManualGeometryOverride;
        private FisheyeProjectionFormula _fisheyeProjectionFormula = FisheyeProjectionFormula.Equidistant;
        private ChromaKeySettings _chromaKeySettings = ChromaKeySettings.Default;
        private VideoGeometrySelection _manualGeometrySelection = new VideoGeometrySelection(VideoProjection.Flat, StereoMode.Mono, FlatVideoCurveMode.None);
        private VideoGeometrySelection _currentGeometrySelection = new VideoGeometrySelection(VideoProjection.Flat, StereoMode.Mono, FlatVideoCurveMode.None);
        private Coroutine _geometryBindingCoroutine;
        private Coroutine _nativeSubtitleSurfaceCoroutine;
        private long _nextVideoOutputSwitchToken;
        private long _activeVideoOutputSwitchToken;
        private long _activeMediaRequestId;
        private VideoOutputSwitchState _videoOutputSwitchState;
        private string _videoOutputSwitchFailureReason;
        private InputLayerSpec? _boundInputLayerSpec;
        private MapperSpec? _boundMapperSpec;
        private OutputLayerSpec? _boundOutputLayerSpec;
        private SubtitleSurfaceSpec? _boundSubtitleSurfaceSpec;
        private bool _videoSurfaceBoundToVlc;
        private bool _subtitleSurfaceBoundToVlc;
        private bool _pendingChangeLayer;
        private bool _hasPendingRebuildLayer;
        private VlcVideoSize _pendingRebuildVideoSize;
        private long _pendingRebuildMediaRequestId;
        private bool _resumeAfterInputLayerRecovery;
        private readonly Queue<PendingPlaybackRequest> _pendingPlaybackRequests =
            new Queue<PendingPlaybackRequest>();
        private VlcVideoSize CurrentVideoSize =>
            new VlcVideoSize(_currentWidth, _currentHeight, _currentVisibleWidth, _currentVisibleHeight);

        public bool IsChromaKeyColorExtractionReady =>
            _chromaKeySettings.Enabled
            && _geometryBindingCoroutine == null
            && _videoSurfaceBoundToVlc
            && _boundMapperSpec.HasValue
            && _boundMapperSpec.Value.ChromaKeyEnabled;

        private enum VideoOutputSwitchState
        {
            None,
            AwaitDetached,
            Detached,
            AwaitReady,
            Ready,
            Failed
        }

        private sealed class PendingPlaybackRequest
        {
            public PendingPlaybackRequest(MediaWrapper media, long mediaRequestId)
            {
                Media = media;
                MediaRequestId = mediaRequestId;
            }

            public MediaWrapper Media { get; }
            public long MediaRequestId { get; }
            public VlcMediaParseResult ParseResult { get; set; }
        }

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
            VlcPlaybackEvents.OnChromaKeyColorExtracted += HandleChromaKeyColorExtracted;
            VlcPlaybackEvents.OnVideoOutputSwitch += HandleVideoOutputSwitchEvent;
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
            VlcPlaybackEvents.OnChromaKeyColorExtracted -= HandleChromaKeyColorExtracted;
            VlcPlaybackEvents.OnVideoOutputSwitch -= HandleVideoOutputSwitchEvent;
            VlcPlaybackEvents.OnPlayRequested -= LoadAndPlay;
            VlcPlaybackEvents.OnClearPlaybackSurface -= ClearPlaybackSurfaceForMediaSwitch;
        }

        private void HandleStatusChanged(PlayerStatus status)
        {
            Debug.Log($"[PlaybackService] 当前播放状态: {status}");
            SurfaceDebug($"status_changed value={status} state={FormatSurfaceState()}");
            OnStatusChanged?.Invoke(status);
        }

        // --------------------------------------------------------
        // 播放逻辑 (原 PlayerController 逻辑)
        // --------------------------------------------------------
        public long PlayVideo(string path, long startTimeMs = 0, string extraData = null)
        {
            if (videoScreen == null)
            {
                Debug.LogError("[PlaybackService] 缺少 VideoScreen！");
                return 0L;
            }

            Debug.Log($"[PlaybackService] 准备播放: {XRVLC.Utils.UriUtils.RedactUri(path)}");

            // 获取暂存的 JSON 字符串，如果为空则直接使用 URI。
            string payload = !string.IsNullOrEmpty(extraData) ? extraData : path;
            bool payloadIsJson = payload != null && payload.TrimStart().StartsWith("{", StringComparison.Ordinal);
            Debug.Log($"[PlaybackService] 最终传给 AAR 的 payload: {payload}");
            SurfaceDebug(
                $"play_video preload path={RedactForLog(path)} current={RedactForLog(CurrentMedia?.Uri)} " +
                $"start={startTimeMs} payloadIsJson={payloadIsJson}");
            return VlcPlaybackBridge.PreloadLocation(payload);
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

            PendingPlaybackRequest request = FindPendingPlaybackRequest(result.MediaRequestId);
            SurfaceDebug(
                $"parse_finished received request={result.MediaRequestId} queued={request != null} " +
                $"callback={RedactForLog(result.uri)} raw={result.width}x{result.height} " +
                $"visible={result.visibleWidth}x{result.visibleHeight} projection={result.projection} " +
                $"duration={result.duration} state={FormatSurfaceState()}");

            if (request == null)
            {
                SurfaceDebug($"parse_finished ignored unknown_request request={result.MediaRequestId}");
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

            request.ParseResult = result;
            StartNextParsedPlaybackRequest();
        }

        private PendingPlaybackRequest FindPendingPlaybackRequest(long mediaRequestId)
        {
            foreach (PendingPlaybackRequest request in _pendingPlaybackRequests)
            {
                if (request.MediaRequestId == mediaRequestId)
                    return request;
            }

            return null;
        }

        private void StartNextParsedPlaybackRequest()
        {
            if (_geometryBindingCoroutine != null
                || _activeMediaRequestId != 0L
                || _pendingPlaybackRequests.Count == 0)
                return;

            PendingPlaybackRequest request = _pendingPlaybackRequests.Peek();
            VlcMediaParseResult result = request.ParseResult;
            if (result == null)
                return;

            VlcVideoSize videoSize = result.ToVideoSize();
            if (!videoSize.IsValid)
            {
                SurfaceDebug($"parse_finished queued_request_invalid request={request.MediaRequestId}");
                return;
            }

            _activeMediaRequestId = request.MediaRequestId;
            CurrentMedia = request.Media;
            CurrentMedia.DurationMs = Math.Max(0L, result.duration);
            CurrentMedia.Projection = result.projection switch
            {
                "360" => MediaProjectionType.Sphere360,
                "180" => MediaProjectionType.Sphere180,
                _ => MediaProjectionType.Flat2D
            };
            _hasManualGeometryOverride = false;
            _pendingChangeLayer = false;
            _hasPendingRebuildLayer = false;
            _pendingRebuildMediaRequestId = 0L;
            _resumeAfterInputLayerRecovery = false;
            _fisheyeProjectionFormula = FisheyeProjectionFormula.Equidistant;
            _chromaKeySettings = ChromaKeySettings.Default;
            _manualGeometrySelection = new VideoGeometrySelection(VideoProjection.Flat, StereoMode.Mono, FlatVideoCurveMode.None);
            ResetShortcutPlaybackState(CurrentMedia);
            SetCurrentVideoSize(videoSize);
            OnChromaKeySettingsChanged?.Invoke(_chromaKeySettings);
            videoScreen.EnterPlaybackBackground();
            OnMediaChanged?.Invoke(CurrentMedia, 0);
            SurfaceDebug(
                $"parse_finished start_head request={request.MediaRequestId} uri={RedactForLog(CurrentMedia.Uri)} " +
                $"content={videoSize.ContentWidth}x{videoSize.ContentHeight} duration={CurrentMedia.DurationMs}");
            RequestRebuildLayer(videoSize, request.MediaRequestId);
        }

        private void CompleteActivePlaybackRequest(long mediaRequestId, string reason)
        {
            if (mediaRequestId == 0L || _activeMediaRequestId != mediaRequestId)
                return;

            if (_pendingPlaybackRequests.Count > 0
                && _pendingPlaybackRequests.Peek().MediaRequestId == mediaRequestId)
            {
                _pendingPlaybackRequests.Dequeue();
            }

            SurfaceDebug($"playback_request complete request={mediaRequestId} reason={reason} queued={_pendingPlaybackRequests.Count}");
            _activeMediaRequestId = 0L;
            StartNextParsedPlaybackRequest();
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

            if (_activeMediaRequestId != 0L || _pendingPlaybackRequests.Count > 0)
            {
                SurfaceDebug(
                    $"layout_callback ignored queued_request active={_activeMediaRequestId} " +
                    $"queued={_pendingPlaybackRequests.Count}");
                return;
            }

            videoSize = NormalizeCodecAlignedPadding(videoSize);
            VlcVideoSize previousSize = CurrentVideoSize;
            VideoGeometrySelection nextGeometry = ResolveRequestedGeometry();
            VideoLayerSpec nextVideoSpec = CreateVideoLayerSpec(videoSize, nextGeometry);
            bool contentUnchanged = previousSize.IsValid && HasSameContentDimensions(previousSize, videoSize);
            bool geometryUnchanged = HasSameGeometry(_currentGeometrySelection, nextGeometry);
            bool surfaceAlreadyCurrent = _geometryBindingCoroutine != null || IsVideoLayerCurrent(nextVideoSpec);
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
            RequestRebuildLayer(videoSize);
        }

        private void RequestRebuildLayer(VlcVideoSize videoSize, long mediaRequestId = 0L)
        {
            if (_geometryService == null && videoScreen != null)
                _geometryService = CreateGeometryService(videoScreen);
            if (_geometryService == null)
            {
                SurfaceDebug($"rebuild_layer skipped geometryService=null videoScreenNull={videoScreen == null}");
                return;
            }

            _currentGeometrySelection = ResolveRequestedGeometry();
            if (_geometryBindingCoroutine != null)
            {
                _hasPendingRebuildLayer = true;
                _pendingRebuildVideoSize = videoSize;
                if (mediaRequestId > 0L || _pendingRebuildMediaRequestId == 0L)
                    _pendingRebuildMediaRequestId = mediaRequestId;
                SurfaceDebug(
                    $"rebuild_layer coalesced_while_active mediaRequest={mediaRequestId} " +
                    $"pendingRequest={_pendingRebuildMediaRequestId}");
                return;
            }

            SurfaceDebug(
                $"rebuild_layer start content={videoSize.ContentWidth}x{videoSize.ContentHeight} " +
                $"projection={_currentGeometrySelection.Projection} stereo={_currentGeometrySelection.Stereo} " +
                $"mediaRequest={mediaRequestId} state={FormatSurfaceState()}");
            _geometryBindingCoroutine = StartCoroutine(RebuildLayer(videoSize, mediaRequestId));
        }

        private void RequestChangeLayer()
        {
            if (videoScreen == null || !CurrentVideoSize.IsValid)
                return;
            if (_geometryService == null)
                _geometryService = CreateGeometryService(videoScreen);
            if (_geometryBindingCoroutine != null)
            {
                _pendingChangeLayer = true;
                SurfaceDebug("change_layer coalesced_while_active");
                return;
            }

            if (!_boundInputLayerSpec.HasValue || !_videoSurfaceBoundToVlc)
            {
                RequestRebuildLayer(CurrentVideoSize);
                return;
            }

            _currentGeometrySelection = ResolveRequestedGeometry();
            _geometryBindingCoroutine = StartCoroutine(ChangeLayer(CurrentVideoSize));
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

        private IEnumerator RebuildLayer(VlcVideoSize videoSize, long mediaRequestId)
        {
            CancelNativeSubtitleSurfaceCoroutine();
            _currentGeometrySelection = ResolveRequestedGeometry();
            VideoLayerSpec layerSpec = CreateVideoLayerSpec(videoSize, _currentGeometrySelection);
            bool rebuildInput = VideoLayerPlanner.NeedsInputRebuild(
                _boundInputLayerSpec,
                _videoSurfaceBoundToVlc,
                layerSpec.Input);
            bool rebuildOutput = VideoLayerPlanner.NeedsOutputRebuild(
                _boundOutputLayerSpec,
                videoScreen != null && videoScreen.IsHardwareSurfaceReady(),
                layerSpec.Output);
            bool wasPlaying = VlcPlaybackBridge.IsPlaying()
                || (mediaRequestId == 0L && _resumeAfterInputLayerRecovery);
            SurfaceDebug(
                $"rebuild_layer begin mediaRequest={mediaRequestId} rebuildInput={rebuildInput} rebuildOutput={rebuildOutput} " +
                $"input={layerSpec.Input.ContentWidth}x{layerSpec.Input.ContentHeight}/hw={layerSpec.Input.HardwareDecoding} " +
                $"projection={layerSpec.Projection} stereo={layerSpec.Mapper.Stereo} chroma={layerSpec.Mapper.ChromaKeyEnabled} " +
                $"state={FormatSurfaceState()}");

            if (wasPlaying && (rebuildInput || rebuildOutput))
            {
                Pause();
                yield return WaitForPausedBeforeSurfaceRebuild();
            }

            long token = ++_nextVideoOutputSwitchToken;
            _activeVideoOutputSwitchToken = token;
            _videoOutputSwitchState = VideoOutputSwitchState.AwaitDetached;
            _videoOutputSwitchFailureReason = null;
            SurfaceDebug(
                $"video_layer begin token={token} operation=RebuildLayer mediaRequest={mediaRequestId} " +
                $"rebuildInput={rebuildInput} rebuildOutput={rebuildOutput}");
            VlcPlaybackBridge.BeginRebuildLayer(token, mediaRequestId, rebuildInput, rebuildOutput);
            yield return WaitForVideoOutputSwitch(token, VideoOutputSwitchState.Detached, 1.5f);
            if (_activeVideoOutputSwitchToken != token || _videoOutputSwitchState != VideoOutputSwitchState.Detached)
            {
                SurfaceDebug($"rebuild_layer detach_failed token={token} state={_videoOutputSwitchState}");
                if (!rebuildInput && TryRecoverWithFullInputRebuild(videoSize, mediaRequestId, layerSpec, false, wasPlaying))
                    yield break;
                FinishLayerTransaction(mediaRequestId, "detach-failed");
                yield break;
            }

            if (rebuildInput)
            {
                _boundInputLayerSpec = null;
                _videoSurfaceBoundToVlc = false;
            }
            if (rebuildOutput)
            {
                _boundOutputLayerSpec = null;
                _geometryService.Rebuild(videoSize, layerSpec.Mapper.ChromaKeyEnabled);
                SurfaceDebug($"rebuild_layer output_rebuilt state={FormatSurfaceState()}");
            }
            else
            {
                ApplyLayerGeometryWithoutRebuild(layerSpec.Projection, layerSpec.Mapper.Stereo, layerSpec.CurveMode);
            }

            bool shouldBindSubtitleSurface = false;
            SubtitleSurfaceSpec subtitleSpec = default(SubtitleSurfaceSpec);
            if (rebuildOutput)
            {
                VlcPlaybackBridge.SetSubtitleSurfacePolicy(ShouldStackSubtitlesOutside());
                shouldBindSubtitleSurface = BeginNativeSubtitleSurfaceRebuild(out subtitleSpec, out _);
            }
            SurfaceDebug(
                $"surface_rebuild before_wait shouldBindSubtitle={shouldBindSubtitleSurface} " +
                $"state={FormatSurfaceState()}");

            yield return WaitForVideoAndSubtitleSurfaces(
                rebuildOutput,
                shouldBindSubtitleSurface);
            SurfaceDebug($"rebuild_layer surfaces_ready state={FormatSurfaceState()}");

            if (shouldBindSubtitleSurface)
                BindSubtitleSurface(subtitleSpec);
            if (!AttachRebuildLayer(token, mediaRequestId, layerSpec, wasPlaying))
            {
                FinishLayerTransaction(mediaRequestId, "attach-not-started");
                yield break;
            }
            yield return WaitForVideoOutputSwitch(token, VideoOutputSwitchState.Ready, 5f);
            if (_activeVideoOutputSwitchToken == token && _videoOutputSwitchState == VideoOutputSwitchState.Ready)
            {
                CommitLayerSpec(layerSpec);
                _videoSurfaceBoundToVlc = true;
                _activeVideoOutputSwitchToken = 0L;
                _videoOutputSwitchState = VideoOutputSwitchState.None;
                _videoOutputSwitchFailureReason = null;
                _resumeAfterInputLayerRecovery = false;
                if (mediaRequestId == 0L && wasPlaying && !VlcPlaybackBridge.IsPlaying())
                    Play();
                FinishLayerTransaction(mediaRequestId, "ready");
                yield break;
            }

            SurfaceDebug($"rebuild_layer attach_failed token={token} state={_videoOutputSwitchState}");
            if (!rebuildInput && TryRecoverWithFullInputRebuild(videoSize, mediaRequestId, layerSpec, rebuildOutput, wasPlaying))
                yield break;
            FinishLayerTransaction(mediaRequestId, "attach-failed");
        }

        private IEnumerator ChangeLayer(VlcVideoSize videoSize)
        {
            CancelNativeSubtitleSurfaceCoroutine();
            _currentGeometrySelection = ResolveRequestedGeometry();
            VideoLayerSpec layerSpec = CreateVideoLayerSpec(videoSize, _currentGeometrySelection);
            bool rebuildOutput = VideoLayerPlanner.NeedsOutputRebuild(
                _boundOutputLayerSpec,
                videoScreen != null && videoScreen.IsHardwareSurfaceReady(),
                layerSpec.Output);
            bool wasPlaying = VlcPlaybackBridge.IsPlaying();
            SurfaceDebug(
                $"change_layer begin rebuildOutput={rebuildOutput} projection={layerSpec.Projection} " +
                $"stereo={layerSpec.Mapper.Stereo} chroma={layerSpec.Mapper.ChromaKeyEnabled} state={FormatSurfaceState()}");

            if (rebuildOutput && wasPlaying)
            {
                Pause();
                yield return WaitForPausedBeforeSurfaceRebuild();
            }

            long token = ++_nextVideoOutputSwitchToken;
            _activeVideoOutputSwitchToken = token;
            _videoOutputSwitchState = VideoOutputSwitchState.AwaitDetached;
            _videoOutputSwitchFailureReason = null;
            VlcPlaybackBridge.BeginChangeLayer(token, rebuildOutput);
            yield return WaitForVideoOutputSwitch(token, VideoOutputSwitchState.Detached, 1.5f);
            if (_activeVideoOutputSwitchToken != token || _videoOutputSwitchState != VideoOutputSwitchState.Detached)
            {
                SurfaceDebug($"change_layer detach_failed token={token} state={_videoOutputSwitchState}");
                if (TryRecoverWithFullInputRebuild(videoSize, 0L, layerSpec, false, wasPlaying))
                    yield break;
                FinishLayerTransaction(0L, "change-detach-failed");
                yield break;
            }

            if (rebuildOutput)
            {
                _boundOutputLayerSpec = null;
                _geometryService.Rebuild(videoSize, layerSpec.Mapper.ChromaKeyEnabled);
            }
            else
            {
                ApplyLayerGeometryWithoutRebuild(layerSpec.Projection, layerSpec.Mapper.Stereo, layerSpec.CurveMode);
            }

            bool shouldBindSubtitleSurface = false;
            SubtitleSurfaceSpec subtitleSpec = default(SubtitleSurfaceSpec);
            if (rebuildOutput)
            {
                VlcPlaybackBridge.SetSubtitleSurfacePolicy(ShouldStackSubtitlesOutside());
                shouldBindSubtitleSurface = BeginNativeSubtitleSurfaceRebuild(out subtitleSpec, out _);
            }
            yield return WaitForVideoAndSubtitleSurfaces(rebuildOutput, shouldBindSubtitleSurface);
            if (shouldBindSubtitleSurface)
                BindSubtitleSurface(subtitleSpec);

            if (!AttachChangeLayer(token, layerSpec))
            {
                FinishLayerTransaction(0L, "change-attach-not-started");
                yield break;
            }
            yield return WaitForVideoOutputSwitch(token, VideoOutputSwitchState.Ready, 5f);
            if (_activeVideoOutputSwitchToken == token && _videoOutputSwitchState == VideoOutputSwitchState.Ready)
            {
                CommitLayerSpec(layerSpec);
                _videoSurfaceBoundToVlc = true;
                _activeVideoOutputSwitchToken = 0L;
                _videoOutputSwitchState = VideoOutputSwitchState.None;
                _videoOutputSwitchFailureReason = null;
                _resumeAfterInputLayerRecovery = false;
                if (rebuildOutput && wasPlaying)
                    Play();
                FinishLayerTransaction(0L, "change-ready");
                yield break;
            }

            SurfaceDebug($"change_layer attach_failed token={token} state={_videoOutputSwitchState}");
            if (TryRecoverWithFullInputRebuild(videoSize, 0L, layerSpec, rebuildOutput, wasPlaying))
                yield break;
            FinishLayerTransaction(0L, "change-attach-failed");
        }

        private IEnumerator WaitForPausedBeforeSurfaceRebuild()
        {
            float waitStartedAt = Time.realtimeSinceStartup;
            while (VlcPlaybackBridge.IsPlaying()
                   && Time.realtimeSinceStartup - waitStartedAt < SurfaceRebuildPauseTimeoutSeconds)
            {
                yield return null;
            }

            bool pauseConfirmed = !VlcPlaybackBridge.IsPlaying();
            SurfaceDebug(
                $"surface_rebuild pause_wait complete confirmed={pauseConfirmed} " +
                $"elapsedMs={(long)((Time.realtimeSinceStartup - waitStartedAt) * 1000f)} " +
                $"state={FormatSurfaceState()}");
            if (!pauseConfirmed)
            {
                Debug.LogWarning(
                    "[PlaybackService] Timed out waiting for VLC pause before surface rebuild.");
            }
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

        private bool AttachRebuildLayer(long token, long mediaRequestId, VideoLayerSpec layerSpec, bool resumeCurrentMedia)
        {
            if (videoScreen == null)
                return false;

            IntPtr videoSurfacePtr = videoScreen.GetHardwareSurfaceHandle();
            SurfaceDebug(
                $"video_layer attach operation=RebuildLayer token={token} mediaRequest={mediaRequestId} surface={videoSurfacePtr} " +
                $"spec={layerSpec.Input.ContentWidth}x{layerSpec.Input.ContentHeight}/{layerSpec.Projection}/{layerSpec.Mapper.Stereo}/{layerSpec.CurveMode} " +
                $"resumeCurrent={resumeCurrentMedia} state={FormatSurfaceState()}");
            if (videoSurfacePtr == IntPtr.Zero)
            {
                Debug.LogError("[PlaybackService] Skipping Vout attach because video surface is not ready.");
                SurfaceDebug("video_layer attach_skipped zero_surface");
                VlcPlaybackBridge.CancelVideoOutputSwitch(token);
                return false;
            }

            ApplyVideoSurfaceProcessingParameters();
            _videoOutputSwitchState = VideoOutputSwitchState.AwaitReady;
            VlcPlaybackBridge.AttachRebuildLayer(
                token,
                mediaRequestId,
                videoSurfacePtr,
                layerSpec.Mapper.FisheyeMappingEnabled,
                layerSpec.Mapper.ChromaKeyEnabled,
                mediaRequestId == 0L && resumeCurrentMedia,
                layerSpec.Mapper.Stereo,
                layerSpec.Input.ContentWidth,
                layerSpec.Input.ContentHeight);
            return true;
        }

        private bool AttachChangeLayer(long token, VideoLayerSpec layerSpec)
        {
            if (videoScreen == null)
                return false;

            IntPtr videoSurfacePtr = videoScreen.GetHardwareSurfaceHandle();
            if (videoSurfacePtr == IntPtr.Zero)
            {
                VlcPlaybackBridge.CancelVideoOutputSwitch(token);
                return false;
            }

            ApplyVideoSurfaceProcessingParameters();
            _videoOutputSwitchState = VideoOutputSwitchState.AwaitReady;
            VlcPlaybackBridge.AttachChangeLayer(
                token,
                videoSurfacePtr,
                layerSpec.Mapper.FisheyeMappingEnabled,
                layerSpec.Mapper.ChromaKeyEnabled,
                layerSpec.Mapper.Stereo,
                layerSpec.Input.ContentWidth,
                layerSpec.Input.ContentHeight);
            return true;
        }

        private void CommitLayerSpec(VideoLayerSpec layerSpec)
        {
            _boundInputLayerSpec = layerSpec.Input;
            _boundMapperSpec = layerSpec.Mapper;
            _boundOutputLayerSpec = layerSpec.Output;
        }

        private bool TryRecoverWithFullInputRebuild(
            VlcVideoSize videoSize,
            long mediaRequestId,
            VideoLayerSpec target,
            bool outputWasPrepared,
            bool resumeCurrentMedia)
        {
            if (_videoOutputSwitchState != VideoOutputSwitchState.Failed
                || (_videoOutputSwitchFailureReason != "input-rebuild-required"
                    && _videoOutputSwitchFailureReason != "surface-mapper-runtime-error"))
                return false;

            bool outputMatchesTarget = videoScreen != null
                && videoScreen.IsHardwareSurfaceReady()
                && (outputWasPrepared
                    || (_boundOutputLayerSpec.HasValue
                        && _boundOutputLayerSpec.Value.Equals(target.Output)));
            SurfaceDebug(
                $"video_layer recover_full_input mediaRequest={mediaRequestId} " +
                $"reason={_videoOutputSwitchFailureReason} outputMatchesTarget={outputMatchesTarget} " +
                $"resumeCurrent={resumeCurrentMedia}");

            if (_activeVideoOutputSwitchToken != 0L)
                VlcPlaybackBridge.CancelVideoOutputSwitch(_activeVideoOutputSwitchToken);
            _activeVideoOutputSwitchToken = 0L;
            _videoOutputSwitchState = VideoOutputSwitchState.None;
            _videoOutputSwitchFailureReason = null;
            _boundInputLayerSpec = null;
            _boundMapperSpec = null;
            _boundOutputLayerSpec = outputMatchesTarget ? target.Output : (OutputLayerSpec?)null;
            _videoSurfaceBoundToVlc = false;
            _geometryBindingCoroutine = null;
            if (mediaRequestId == 0L && resumeCurrentMedia)
                _resumeAfterInputLayerRecovery = true;
            RequestRebuildLayer(videoSize, mediaRequestId);
            return true;
        }

        private void FinishLayerTransaction(long mediaRequestId, string reason)
        {
            if (_activeVideoOutputSwitchToken != 0L)
            {
                VlcPlaybackBridge.CancelVideoOutputSwitch(_activeVideoOutputSwitchToken);
                _activeVideoOutputSwitchToken = 0L;
                _videoOutputSwitchState = VideoOutputSwitchState.None;
                _videoOutputSwitchFailureReason = null;
            }
            _resumeAfterInputLayerRecovery = false;
            _geometryBindingCoroutine = null;
            if (mediaRequestId > 0L && reason != "ready")
                VlcPlaybackBridge.CancelPendingMediaRequest(mediaRequestId);
            CompleteActivePlaybackRequest(mediaRequestId, reason);
            StartNextParsedPlaybackRequest();
            if (_geometryBindingCoroutine != null || _activeMediaRequestId != 0L)
                return;

            if (_hasPendingRebuildLayer)
            {
                VlcVideoSize pendingSize = _pendingRebuildVideoSize;
                long pendingMediaRequestId = _pendingRebuildMediaRequestId;
                _hasPendingRebuildLayer = false;
                _pendingRebuildMediaRequestId = 0L;
                RequestRebuildLayer(pendingSize, pendingMediaRequestId);
                return;
            }

            if (_pendingChangeLayer)
            {
                _pendingChangeLayer = false;
                RequestChangeLayer();
            }
        }

        private IEnumerator WaitForVideoOutputSwitch(
            long token,
            VideoOutputSwitchState completedState,
            float timeoutSeconds)
        {
            float waitStartedAt = Time.realtimeSinceStartup;
            while (_activeVideoOutputSwitchToken == token
                   && _videoOutputSwitchState != completedState
                   && _videoOutputSwitchState != VideoOutputSwitchState.Failed
                   && Time.realtimeSinceStartup - waitStartedAt < timeoutSeconds)
            {
                yield return null;
            }

            if (_activeVideoOutputSwitchToken == token
                && _videoOutputSwitchState != completedState
                && _videoOutputSwitchState != VideoOutputSwitchState.Failed)
            {
                SurfaceDebug(
                    $"video_output_switch unity_timeout token={token} expected={completedState} " +
                    $"elapsedMs={(long)((Time.realtimeSinceStartup - waitStartedAt) * 1000f)}");
                VlcPlaybackBridge.CancelVideoOutputSwitch(token);
                _videoOutputSwitchState = VideoOutputSwitchState.Failed;
                _videoOutputSwitchFailureReason = "unity-timeout";
            }
        }

        private void HandleVideoOutputSwitchEvent(string payload)
        {
            string[] parts = (payload ?? string.Empty).Split('|');
            if (parts.Length < 2 || !long.TryParse(parts[0], out long token))
            {
                SurfaceDebug($"video_output_switch ignored malformed_event payload={payload}");
                return;
            }
            if (token != _activeVideoOutputSwitchToken)
            {
                SurfaceDebug($"video_output_switch ignored stale_event token={token} active={_activeVideoOutputSwitchToken} payload={payload}");
                return;
            }

            switch (parts[1])
            {
                case "detached":
                    if (_videoOutputSwitchState == VideoOutputSwitchState.AwaitDetached)
                        _videoOutputSwitchState = VideoOutputSwitchState.Detached;
                    break;
                case "ready":
                    if (_videoOutputSwitchState == VideoOutputSwitchState.AwaitReady)
                        _videoOutputSwitchState = VideoOutputSwitchState.Ready;
                    break;
                case "failed":
                    _videoOutputSwitchState = VideoOutputSwitchState.Failed;
                    _videoOutputSwitchFailureReason = parts.Length > 2 ? parts[2] : "unknown";
                    break;
                default:
                    SurfaceDebug($"video_output_switch ignored unknown_event payload={payload}");
                    return;
            }

            SurfaceDebug(
                $"video_output_switch event token={token} state={parts[1]} reason=" +
                $"{(parts.Length > 2 ? parts[2] : "-")}");
        }

        private void CancelActiveVideoOutputSwitch(string reason)
        {
            if (_activeVideoOutputSwitchToken == 0L)
                return;

            SurfaceDebug($"video_output_switch cancel token={_activeVideoOutputSwitchToken} reason={reason}");
            VlcPlaybackBridge.CancelVideoOutputSwitch(_activeVideoOutputSwitchToken);
            _activeVideoOutputSwitchToken = 0L;
            _videoOutputSwitchState = VideoOutputSwitchState.None;
            _videoOutputSwitchFailureReason = null;
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

        private VideoGeometrySelection? GetManualGeometryOverride()
        {
            return _hasManualGeometryOverride ? _manualGeometrySelection : (VideoGeometrySelection?)null;
        }

        private VideoGeometrySelection ResolveRequestedGeometry()
        {
            if (_hasManualGeometryOverride)
                return _manualGeometrySelection;

            var (projection, stereo) = XRVLC.ProjectionDetector.Detect(CurrentMedia);
            return new VideoGeometrySelection(
                projection,
                stereo,
                FlatVideoCurveMode.None,
                _fisheyeProjectionFormula);
        }

        private void OnStateChanged(string state)
        {
            switch (state)
            {
                case "Opening": HandleStatusChanged(PlayerStatus.Opening); break;
                case "Buffering": HandleStatusChanged(PlayerStatus.Buffering); break;
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
            long totalTimeMs = CurrentMedia?.DurationMs ?? 0L;
            if (totalTimeMs <= 0L)
                totalTimeMs = Math.Max(0L, VlcPlaybackBridge.GetLength());
            SurfaceDebug($"event_time_changed time={timeMs} total={totalTimeMs}");
            OnTimeChanged?.Invoke(timeMs, totalTimeMs);
        }

        private void HandleLengthChanged(long length)
        {
            long duration = Math.Max(0L, length);
            if (CurrentMedia != null)
                CurrentMedia.DurationMs = duration;
            SurfaceDebug($"event_length_changed length={duration}");
        }

        private void HandleBuffering(float buffering)
        {
            OnBuffering?.Invoke(buffering);
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
            CancelActiveVideoOutputSwitch("stop");
            VlcPlaybackBridge.CancelPendingMediaRequests();
            _pendingPlaybackRequests.Clear();
            _activeMediaRequestId = 0L;
            _pendingChangeLayer = false;
            _hasPendingRebuildLayer = false;
            _pendingRebuildMediaRequestId = 0L;
            _resumeAfterInputLayerRecovery = false;
            _currentWidth = 0;
            _currentHeight = 0;
            _currentVisibleWidth = 0;
            _currentVisibleHeight = 0;
            _isShortcutFastRate = false;
            VlcPlaybackBridge.PublishPlaybackRate(1f);
            
            DisableAndDetachSubtitleSurface(false);
            VlcPlaybackBridge.Stop();
            DetachVideoSurfaceFromVlc();

            if (videoScreen != null)
            {
                videoScreen.DestroyFlatSubtitleLayer();
                videoScreen.DestroyLayer();
            }
        }

        private void ClearPlaybackSurfaceForMediaSwitch()
        {
            Debug.Log("[PlaybackService] Clearing Unity playback surfaces before media switch.");
            CancelActiveVideoOutputSwitch("release-only");
            VlcPlaybackBridge.CancelPendingMediaRequests();
            ClearPendingSurfaceBindingCoroutines();
            _currentWidth = 0;
            _currentHeight = 0;
            _currentVisibleWidth = 0;
            _currentVisibleHeight = 0;
            _pendingChangeLayer = false;
            _hasPendingRebuildLayer = false;
            _pendingRebuildMediaRequestId = 0L;
            _resumeAfterInputLayerRecovery = false;

            DisableAndDetachSubtitleSurface(false);
            DetachVideoSurfaceFromVlc();

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
                ClearBoundLayerSpecs();
                _videoSurfaceBoundToVlc = false;
            }
        }

        public void DetachVideoScreen()
        {
            CancelActiveVideoOutputSwitch("detach-video-screen");
            DisableAndDetachSubtitleSurface(false);
            DetachVideoSurfaceFromVlc();
            videoScreen?.DestroyFlatSubtitleLayer();
            videoScreen = null;
            _geometryService = null;
            ClearBoundLayerSpecs();
            _videoSurfaceBoundToVlc = false;
        }

        // --------------------------------------------------------
        // 外部传入新视频
        // --------------------------------------------------------
        public void LoadAndPlay(MediaWrapper item)
        {
            if (item == null || string.IsNullOrEmpty(item.Uri))
                return;

            long mediaRequestId = PlayVideo(item.Uri, item.Time, item.RawJson);
            if (mediaRequestId <= 0L)
            {
                SurfaceDebug($"load_and_play ignored missing_request uri={RedactForLog(item.Uri)}");
                return;
            }

            _pendingPlaybackRequests.Enqueue(new PendingPlaybackRequest(item, mediaRequestId));
            SurfaceDebug(
                $"load_and_play queued request={mediaRequestId} media={RedactForLog(item.Uri)} " +
                $"queued={_pendingPlaybackRequests.Count}");
            StartNextParsedPlaybackRequest();
        }

        // --------------------------------------------------------
        // 基础播放控制
        // --------------------------------------------------------
        public void Play()
        {
            SurfaceDebug($"control_play enter state={FormatSurfaceState()}");
            VlcPlaybackBridge.Play();
            SurfaceDebug("control_play dispatched");
        }

        public void Pause()
        {
            SurfaceDebug($"control_pause enter state={FormatSurfaceState()}");
            VlcPlaybackBridge.Pause();
            SurfaceDebug("control_pause dispatched");
        }

        public void TogglePlayPause()
        {
            SurfaceDebug("control_toggle enter");
            if (VlcPlaybackBridge.IsPlaying()) Pause();
            else Play();
        }

        public PlayerStatus GetLivePlaybackStatus()
        {
            return VlcPlaybackBridge.GetPlayerState() switch
            {
                1 => PlayerStatus.Opening,
                2 => PlayerStatus.Buffering,
                3 => PlayerStatus.Playing,
                4 => PlayerStatus.Paused,
                5 => PlayerStatus.Stopped,
                6 => PlayerStatus.Ended,
                7 => PlayerStatus.Error,
                _ => PlayerStatus.Idle
            };
        }

        public void Stop()
        {
            StopInternal();
        }

        public void SeekTo(long timeMs)
        {
            SurfaceDebug($"control_seek_time enter target={timeMs}");
            VlcPlaybackBridge.SetTime(timeMs);
            SurfaceDebug($"control_seek_time dispatched target={timeMs}");
        }

        /// <summary>
        /// 基于播放器当前时间做相对跳转，并把结果限制在 0ms 之后。
        /// </summary>
        public void SeekRelativeSeconds(int seconds)
        {
            long currentTime = Math.Max(0L, VlcPlaybackBridge.GetTime());
            long target = Math.Max(0L, currentTime + seconds * 1000L);
            SurfaceDebug($"control_seek_relative enter seconds={seconds} current={currentTime} target={target}");
            SeekTo(target);
        }

        public void SeekToPosition(float position)
        {
            SurfaceDebug($"control_seek_position enter position={position}");
            VlcPlaybackBridge.Seek(position);
            SurfaceDebug($"control_seek_position dispatched position={position}");
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

        /// <summary>
        /// 切换 PICO 背景透视，并同步视频幕布的透明背景状态。
        /// 控制栏按钮与 XR 快捷键共用此入口，避免两条路径行为不一致。
        /// </summary>
        public void TogglePassthroughBackground()
        {
            PicoPassthroughModeService passthroughService =
                FindAnyObjectByType<PicoPassthroughModeService>();
            if (passthroughService == null)
                passthroughService = gameObject.AddComponent<PicoPassthroughModeService>();

            passthroughService.Toggle();
            videoScreen?.SetPassthroughBackgroundEnabled(
                passthroughService.IsSupported && passthroughService.IsEnabled);
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

            VideoGeometrySelection nextGeometry = new VideoGeometrySelection(
                projection,
                stereo,
                curveMode,
                _fisheyeProjectionFormula);

            _hasManualGeometryOverride = true;
            _manualGeometrySelection = nextGeometry;

            if (videoScreen == null)
                return;

            if (CurrentVideoSize.IsValid)
            {
                RequestChangeLayer();
                return;
            }

            videoScreen.ChangeLayer(projection, stereo, curveMode);
            _currentGeometrySelection = nextGeometry;
        }

        private void ApplyLayerGeometryWithoutRebuild(VideoProjection projection, StereoMode stereo, FlatVideoCurveMode curveMode)
        {
            videoScreen.ChangeLayer(projection, stereo, curveMode);
            videoScreen.FitVideoSize((uint)CurrentVideoSize.ContentWidth, (uint)CurrentVideoSize.ContentHeight);
            _currentGeometrySelection = new VideoGeometrySelection(
                projection,
                stereo,
                curveMode,
                _fisheyeProjectionFormula);
        }

        public void SetFisheyeProjectionFormula(FisheyeProjectionFormula formula)
        {
            if (!Enum.IsDefined(typeof(FisheyeProjectionFormula), formula))
                formula = FisheyeProjectionFormula.Equidistant;
            if (_fisheyeProjectionFormula == formula)
                return;

            _fisheyeProjectionFormula = formula;
            _currentGeometrySelection.FisheyeProjectionFormula = formula;
            if (_hasManualGeometryOverride)
                _manualGeometrySelection.FisheyeProjectionFormula = formula;
            ApplyVideoSurfaceProcessingParameters();
            SurfaceDebug($"fisheye_formula_update formula={formula} surfaceRebuild=false");
        }

        public void SetChromaKeyEnabled(bool enabled)
        {
            if (_chromaKeySettings.Enabled == enabled)
                return;

            _chromaKeySettings = _chromaKeySettings.WithEnabled(enabled);
            OnChromaKeySettingsChanged?.Invoke(_chromaKeySettings);
            SurfaceDebug(
                $"chroma_key_enabled enabled={enabled} key={_chromaKeySettings.ToHex()} " +
                $"range={_chromaKeySettings.ColorRange:F3} edgeSmooth={_chromaKeySettings.EdgeSmooth:F3} " +
                $"despill={_chromaKeySettings.DespillStrength:F3}");

            if (CurrentVideoSize.IsValid && videoScreen != null)
                RequestChangeLayer();
        }

        public void SetChromaKeyColor(Color keyColor)
        {
            _chromaKeySettings = _chromaKeySettings.WithKeyColor(keyColor);
            NotifyChromaKeyProcessingParametersChanged("key-color");
        }

        public void SetChromaKeyColorRange(float colorRange)
        {
            _chromaKeySettings = _chromaKeySettings.WithColorRange(colorRange);
            NotifyChromaKeyProcessingParametersChanged("color-range");
        }

        public void SetChromaKeyEdgeSmooth(float edgeSmooth)
        {
            _chromaKeySettings = _chromaKeySettings.WithEdgeSmooth(edgeSmooth);
            NotifyChromaKeyProcessingParametersChanged("edge-smooth");
        }

        public void SetChromaKeyDespillStrength(float despillStrength)
        {
            _chromaKeySettings = _chromaKeySettings.WithDespillStrength(despillStrength);
            NotifyChromaKeyProcessingParametersChanged("despill");
        }

        public void RequestChromaKeyColorExtraction()
        {
            VlcPlaybackBridge.RequestVideoSurfaceChromaKeyColorExtraction();
        }

        private void HandleChromaKeyColorExtracted(string payload)
        {
            string[] parts = (payload ?? string.Empty).Split('|');
            ChromaKeySettings extracted = ChromaKeySettings.Default;
            bool success = parts.Length == 2 &&
                string.Equals(parts[0], "ok", StringComparison.Ordinal) &&
                ChromaKeySettings.TryParseHex(parts[1], out extracted);
            if (success)
            {
                SetChromaKeyColor(extracted.KeyColor);
            }
            else
            {
                Debug.LogWarning($"[PlaybackService] Chroma key color extraction failed: {payload}");
            }
            OnChromaKeyColorExtractionCompleted?.Invoke(success);
        }

        private void NotifyChromaKeyProcessingParametersChanged(string reason)
        {
            OnChromaKeySettingsChanged?.Invoke(_chromaKeySettings);
            ApplyVideoSurfaceProcessingParameters();
            SurfaceDebug(
                $"chroma_key_parameters reason={reason} key={_chromaKeySettings.ToHex()} " +
                $"range={_chromaKeySettings.ColorRange:F3} edgeSmooth={_chromaKeySettings.EdgeSmooth:F3} " +
                $"despill={_chromaKeySettings.DespillStrength:F3} " +
                "surfaceRebuild=false");
        }

        private void ApplyVideoSurfaceProcessingParameters()
        {
            VlcPlaybackBridge.SetVideoSurfaceProcessingParameters(
                _fisheyeProjectionFormula,
                _chromaKeySettings.KeyColor,
                _chromaKeySettings.ColorRange,
                _chromaKeySettings.EdgeSmooth,
                _chromaKeySettings.DespillStrength);
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
            if (CurrentVideoSize.IsValid && videoScreen != null)
                RequestRebuildLayer(CurrentVideoSize);
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
                    subtitleSpec.RenderOutsideScreen,
                    subtitleSpec.RenderMode == SubtitleRenderMode.Native))
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
            bool renderOutsideScreen = ShouldRenderSubtitlesOutsideScreen();
            uint surfaceHeight = UsesSingleHeightSubtitleSurface()
                ? contentHeight
                : contentHeight * 2u;

            spec = new SubtitleSurfaceSpec(
                surfaceWidth,
                surfaceHeight,
                contentWidth,
                contentHeight,
                CurrentGeometrySelection.Stereo,
                renderOutsideScreen,
                ShouldStackSubtitlesOutside(),
                SubtitleRenderMode);
            return true;
        }

        private bool IsVideoLayerCurrent(VideoLayerSpec spec)
        {
            return videoScreen != null
                && videoScreen.IsHardwareSurfaceReady()
                && _videoSurfaceBoundToVlc
                && _boundInputLayerSpec.HasValue
                && _boundMapperSpec.HasValue
                && _boundOutputLayerSpec.HasValue
                && _boundInputLayerSpec.Value.Equals(spec.Input)
                && _boundMapperSpec.Value.Equals(spec.Mapper)
                && _boundOutputLayerSpec.Value.Equals(spec.Output);
        }

        private bool IsSubtitleSurfaceCurrent(SubtitleSurfaceSpec spec)
        {
            return videoScreen != null
                && videoScreen.IsFlatSubtitleSurfaceReady()
                && _boundSubtitleSurfaceSpec.HasValue
                && _boundSubtitleSurfaceSpec.Value.Equals(spec);
        }

        private VideoLayerSpec CreateVideoLayerSpec(VlcVideoSize videoSize, VideoGeometrySelection geometry)
        {
            return VideoLayerPlanner.Create(
                (uint)videoSize.ContentWidth,
                (uint)videoSize.ContentHeight,
                UseHardwareDecoding,
                geometry.Projection,
                geometry.Stereo,
                geometry.CurveMode,
                _chromaKeySettings.Enabled);
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

        private VlcVideoSize NormalizeCodecAlignedPadding(VlcVideoSize reportedSize)
        {
            if (!_videoSurfaceBoundToVlc || !_boundInputLayerSpec.HasValue)
                return reportedSize;

            InputLayerSpec boundInput = _boundInputLayerSpec.Value;
            int trustedWidth = (int)boundInput.ContentWidth;
            int trustedHeight = (int)boundInput.ContentHeight;
            bool reportedVisibleEqualsRaw = reportedSize.VisibleWidth == reportedSize.Width
                && reportedSize.VisibleHeight == reportedSize.Height;
            bool matchesAlignedBuffer = reportedSize.Width == AlignUp(trustedWidth, CodecBufferAlignmentPixels)
                && reportedSize.Height == AlignUp(trustedHeight, CodecBufferAlignmentPixels);
            bool differsFromTrustedContent = reportedSize.ContentWidth != trustedWidth
                || reportedSize.ContentHeight != trustedHeight;

            if (!reportedVisibleEqualsRaw || !matchesAlignedBuffer || !differsFromTrustedContent)
                return reportedSize;

            SurfaceDebug(
                $"layout_callback normalized codec_padding raw={reportedSize.Width}x{reportedSize.Height} " +
                $"reportedVisible={reportedSize.VisibleWidth}x{reportedSize.VisibleHeight} " +
                $"trustedVisible={trustedWidth}x{trustedHeight} alignment={CodecBufferAlignmentPixels}");
            return new VlcVideoSize(
                reportedSize.Width,
                reportedSize.Height,
                trustedWidth,
                trustedHeight);
        }

        private static int AlignUp(int value, int alignment)
        {
            return (value + alignment - 1) / alignment * alignment;
        }

        private static bool HasSameGeometry(VideoGeometrySelection a, VideoGeometrySelection b)
        {
            return a.Projection == b.Projection
                && a.Stereo == b.Stereo
                && a.CurveMode == b.CurveMode
                && a.FisheyeProjectionFormula == b.FisheyeProjectionFormula;
        }

        private static bool UsesFlatSubtitleSurfaceMode(SubtitleRenderMode mode)
        {
            return mode == SubtitleRenderMode.Native
                || mode == SubtitleRenderMode.Spatial
                || mode == SubtitleRenderMode.DualDebug;
        }

        private bool UsesSingleHeightSubtitleSurface()
        {
            return (CurrentGeometrySelection.Projection == VideoProjection.Flat
                    || CurrentGeometrySelection.Projection == VideoProjection.Cylinder)
                && !ShouldRenderSubtitlesOutsideScreen();
        }

        private bool ShouldRenderSubtitlesOutsideScreen()
        {
            return SubtitleRenderMode == SubtitleRenderMode.Spatial
                && RenderSubtitlesOutsideScreen;
        }

        private bool ShouldStackSubtitlesOutside()
        {
            if (SubtitleRenderMode != SubtitleRenderMode.Spatial
                && SubtitleRenderMode != SubtitleRenderMode.DualDebug)
                return false;

            if (IsImmersiveProjection(CurrentGeometrySelection.Projection))
                return true;
            return ShouldRenderSubtitlesOutsideScreen();
        }

        private static bool IsImmersiveProjection(VideoProjection projection)
        {
            return projection == VideoProjection.Sphere360 ||
                projection == VideoProjection.Sphere180 ||
                projection == VideoProjection.Fisheye180;
        }

        private static bool IsFisheyeProjection(VideoProjection projection)
        {
            return projection == VideoProjection.Fisheye180;
        }

        private void DetachVideoSurfaceFromVlc()
        {
            VlcPlaybackBridge.DetachSurface();
            VlcPlaybackBridge.SetVideoSurfaceMapping(false, false, StereoMode.Mono, 0, 0);
            _videoSurfaceBoundToVlc = false;
            ClearBoundLayerSpecs();
        }

        private void ClearBoundLayerSpecs()
        {
            _boundInputLayerSpec = null;
            _boundMapperSpec = null;
            _boundOutputLayerSpec = null;
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
