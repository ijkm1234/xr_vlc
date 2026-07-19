using System;
using UnityEngine;

namespace XRVLC
{
    public class VideoScreenTransformService
    {
        private const float Sphere180DistanceSpeedMultiplier = 4f;
        private const float Sphere180MaxPullCloserOffsetMeters = 30f;
        private const float Sphere180MaxPushFartherOffsetMeters = 50f;

        private readonly Transform _screenRoot;
        private readonly Transform _videoAnchor;
        private readonly Func<Transform> _viewerProvider;
        private readonly Func<VideoProjection> _projectionProvider;
        private readonly Action<float> _flatZoomDeltaHandler;
        private readonly Vector3 _screenRootAuthoredPosition;
        private readonly Quaternion _screenRootAuthoredRotation;

        private bool _isControllerMoveActive;
        private Vector3 _controllerMovePivot;
        private Vector3 _controllerMoveStartTargetOffset;
        private Vector3 _controllerMoveStartRayDirection;
        private Quaternion _controllerMoveStartTargetRotation;
        private float _sphere180DistanceOffsetMeters;
        private Transform _activeMoveTarget;

        public VideoScreenTransformService(
            Transform screenRoot,
            Transform videoAnchor,
            Func<Transform> viewerProvider,
            Func<VideoProjection> projectionProvider)
            : this(
                screenRoot,
                videoAnchor,
                viewerProvider,
                projectionProvider,
                null)
        {
        }

        public VideoScreenTransformService(
            Transform screenRoot,
            Transform videoAnchor,
            Func<Transform> viewerProvider,
            Func<VideoProjection> projectionProvider,
            Action<float> flatZoomDeltaHandler = null)
        {
            _screenRoot = screenRoot;
            _videoAnchor = videoAnchor;
            _viewerProvider = viewerProvider;
            _projectionProvider = projectionProvider;
            _flatZoomDeltaHandler = flatZoomDeltaHandler;
            _screenRootAuthoredPosition = screenRoot != null ? screenRoot.position : Vector3.zero;
            _screenRootAuthoredRotation = screenRoot != null ? screenRoot.rotation : Quaternion.identity;

            EnsureMoveTargetState();
        }

        /// <summary>
        /// 将旧的前后偏移输入路由到平面 zoom；180 半球沿球心到屏幕中心的半径方向移动。
        /// </summary>
        public void AddViewerDistanceOffset(float deltaMeters)
        {
            Transform target = GetMoveTarget();
            if (target == null) return;

            EnsureMoveTargetState();
            VideoProjection projection = GetCurrentProjection();
            if (projection == VideoProjection.Flat || projection == VideoProjection.Cylinder)
            {
                _flatZoomDeltaHandler?.Invoke(deltaMeters);
                return;
            }

            if (projection == VideoProjection.Sphere360)
                return;

            if (projection != VideoProjection.Sphere180 && projection != VideoProjection.Fisheye180)
                return;

            if (!TryNormalizeDirection(target.forward, out Vector3 sphereCenterToScreenCenter))
                return;

            float requestedOffset =
                _sphere180DistanceOffsetMeters + deltaMeters * Sphere180DistanceSpeedMultiplier;
            float clampedOffset = Mathf.Clamp(
                requestedOffset,
                -Sphere180MaxPushFartherOffsetMeters,
                Sphere180MaxPullCloserOffsetMeters);
            float appliedOffset = clampedOffset - _sphere180DistanceOffsetMeters;
            if (Mathf.Approximately(appliedOffset, 0f))
                return;

            target.position += sphereCenterToScreenCenter * appliedOffset;
            _sphere180DistanceOffsetMeters = clampedOffset;
        }

        public void ResetImmersiveDistanceOffset()
        {
            _sphere180DistanceOffsetMeters = 0f;
        }

        /// <summary>
        /// 记录 Grip 拖动开始时的手柄射线方向、目标位置和目标旋转。
        /// </summary>
        public void BeginControllerMove(Vector3 controllerRayDirection)
        {
            Transform target = GetMoveTarget();
            Transform viewer = _viewerProvider?.Invoke();
            if (target == null) return;
            if (viewer == null) return;
            if (!TryNormalizeDirection(controllerRayDirection, out Vector3 startRayDirection))
                return;

            EnsureMoveTargetState();
            _isControllerMoveActive = true;
            _controllerMovePivot = viewer.position;
            _controllerMoveStartTargetOffset = target.position - _controllerMovePivot;
            _controllerMoveStartRayDirection = startRayDirection;
            _controllerMoveStartTargetRotation = target.rotation;
        }

        /// <summary>
        /// 根据手柄射线方向变化，平面幕布绕相机移动，全景锚点跟随拖拽旋转。
        /// </summary>
        public void UpdateControllerMove(Vector3 controllerRayDirection)
        {
            Transform target = GetMoveTarget();
            if (target == null) return;
            if (!TryNormalizeDirection(controllerRayDirection, out Vector3 currentRayDirection))
                return;

            if (!_isControllerMoveActive)
                BeginControllerMove(controllerRayDirection);
            if (!_isControllerMoveActive)
                return;

            if (_controllerMoveStartRayDirection.sqrMagnitude < 0.0001f)
                return;

            Quaternion orbitRotation = Quaternion.FromToRotation(
                _controllerMoveStartRayDirection,
                currentRayDirection);

            if (IsImmersive())
            {
                target.rotation = orbitRotation * _controllerMoveStartTargetRotation;
                return;
            }

            if (_controllerMoveStartTargetOffset.sqrMagnitude < 0.0001f)
                return;

            target.position = _controllerMovePivot + orbitRotation * _controllerMoveStartTargetOffset;

            FacePoint(target, _controllerMovePivot);
        }

        /// <summary>
        /// 结束 Grip 拖动并保留当前目标位置。
        /// </summary>
        public void EndControllerMove()
        {
            _isControllerMoveActive = false;
        }

        /// <summary>
        /// 恢复场景 authored 默认位置，并重新朝向当前玩家视角。
        /// </summary>
        public void ResetToDefault()
        {
            if (IsImmersive())
                return;

            Transform target = GetMoveTarget();
            if (target == null) return;

            _isControllerMoveActive = false;

            if (target == _screenRoot)
            {
                target.SetPositionAndRotation(_screenRootAuthoredPosition, _screenRootAuthoredRotation);
            }

            Transform viewer = _viewerProvider?.Invoke();
            if (viewer != null && target == _screenRoot)
                FaceViewer(_screenRoot, viewer);
        }

        private bool IsImmersive()
        {
            VideoProjection projection = GetCurrentProjection();
            return projection == VideoProjection.Sphere360 ||
                projection == VideoProjection.Sphere180 ||
                projection == VideoProjection.Fisheye180;
        }

        private VideoProjection GetCurrentProjection()
        {
            return _projectionProvider != null
                ? _projectionProvider()
                : VideoProjection.Flat;
        }

        private Transform GetMoveTarget()
        {
            return IsImmersive() ? _videoAnchor : _screenRoot;
        }

        private void EnsureMoveTargetState()
        {
            Transform target = GetMoveTarget();
            if (target == null || target == _activeMoveTarget) return;

            _activeMoveTarget = target;

            Transform viewer = _viewerProvider?.Invoke();
            if (viewer != null && !IsImmersive())
                FaceViewer(_screenRoot, viewer);
        }

        private static void FaceViewer(Transform target, Transform viewer)
        {
            if (target == null || viewer == null) return;
            FacePoint(target, viewer.position);
        }

        private static void FacePoint(Transform target, Vector3 point)
        {
            if (target == null) return;
            target.LookAt(point);
            target.Rotate(0f, 180f, 0f);
        }

        private static bool TryNormalizeDirection(Vector3 direction, out Vector3 normalized)
        {
            normalized = Vector3.zero;

            if (float.IsNaN(direction.x) || float.IsNaN(direction.y) || float.IsNaN(direction.z) ||
                float.IsInfinity(direction.x) || float.IsInfinity(direction.y) || float.IsInfinity(direction.z))
                return false;

            float sqrMagnitude = direction.sqrMagnitude;
            if (sqrMagnitude < 0.0001f)
                return false;

            normalized = direction / Mathf.Sqrt(sqrMagnitude);
            return true;
        }
    }
}
