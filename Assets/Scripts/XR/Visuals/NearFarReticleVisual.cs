using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace XR.Visuals
{
    /// <summary>
    /// Draws a reticle prefab at the far-cast hit point of a <see cref="NearFarInteractor"/>.
    /// Mirrors the functionality of XRInteractorReticleVisual but uses NearFarInteractor's
    /// TryGetCurveEndPoint / TryGetCurveEndNormal instead of XRRayInteractor.TryGetCurrentRaycast.
    /// </summary>
    [AddComponentMenu("XR/Visual/Near Far Reticle Visual")]
    [DisallowMultipleComponent]
    public class NearFarReticleVisual : MonoBehaviour
    {
        [SerializeField]
        GameObject m_ReticlePrefab;
        public GameObject reticlePrefab
        {
            get => m_ReticlePrefab;
            set { m_ReticlePrefab = value; SetupReticlePrefab(); }
        }

        [SerializeField]
        float m_PrefabScalingFactor = 1f;
        public float prefabScalingFactor
        {
            get => m_PrefabScalingFactor;
            set => m_PrefabScalingFactor = value;
        }

        [SerializeField]
        bool m_UndoDistanceScaling = true;
        public bool undoDistanceScaling
        {
            get => m_UndoDistanceScaling;
            set => m_UndoDistanceScaling = value;
        }

        [SerializeField]
        bool m_AlignPrefabWithSurfaceNormal = true;
        public bool alignPrefabWithSurfaceNormal
        {
            get => m_AlignPrefabWithSurfaceNormal;
            set => m_AlignPrefabWithSurfaceNormal = value;
        }

        [SerializeField]
        float m_EndpointSmoothingTime = 0.02f;
        public float endpointSmoothingTime
        {
            get => m_EndpointSmoothingTime;
            set => m_EndpointSmoothingTime = value;
        }

        [SerializeField]
        bool m_DrawWhileSelecting;
        public bool drawWhileSelecting
        {
            get => m_DrawWhileSelecting;
            set => m_DrawWhileSelecting = value;
        }

        [SerializeField]
        bool m_DrawOnNoHit;
        /// <summary>
        /// When true, draws the reticle at the far end of the ray even when nothing is hit.
        /// </summary>
        public bool drawOnNoHit
        {
            get => m_DrawOnNoHit;
            set => m_DrawOnNoHit = value;
        }

        [SerializeField]
        bool m_HideInNearRegion = true;
        /// <summary>
        /// When true, hides the reticle while the interactor is in Near region (poke/grab range).
        /// </summary>
        public bool hideInNearRegion
        {
            get => m_HideInNearRegion;
            set => m_HideInNearRegion = value;
        }

        [SerializeField]
        float m_UiSurfaceOffset = 0.006f;
        /// <summary>
        /// Keeps the reticle just above UI hits while still visually attached to the panel surface.
        /// </summary>
        public float uiSurfaceOffset
        {
            get => m_UiSurfaceOffset;
            set => m_UiSurfaceOffset = Mathf.Max(0f, value);
        }

        const int ReticleRenderQueue = 5000;
        const int ReticleRendererPriority = 100;
        static readonly int ZTestProperty = Shader.PropertyToID("_ZTest");
        static readonly int ZWriteProperty = Shader.PropertyToID("_ZWrite");

        bool m_ReticleActive;
        public bool reticleActive
        {
            get => m_ReticleActive;
            set
            {
                m_ReticleActive = value;
                if (m_ReticleInstance != null)
                    m_ReticleInstance.SetActive(value);
            }
        }

        NearFarInteractor m_Interactor;
        ICurveInteractionDataProvider m_CurveProvider;
        XROrigin m_XROrigin;
        GameObject m_ReticleInstance;

        Vector3 m_TargetEndPoint;
        Vector3 m_TargetEndNormal;
        Vector3 m_SmoothedEndPointVelocity;
        Vector3 m_SmoothedEndNormalVelocity;
        bool m_HasRaycastHit;

        void Awake()
        {
            if (!TryGetComponent(out m_Interactor))
            {
                Debug.LogWarning("[NearFarReticleVisual] No NearFarInteractor found on this GameObject.", this);
                enabled = false;
                return;
            }

            m_CurveProvider = m_Interactor;
            m_Interactor.selectEntered.AddListener(OnSelectEntered);

            m_XROrigin = Object.FindAnyObjectByType<XROrigin>();
            SetupReticlePrefab();
            reticleActive = false;
        }

        void OnDisable()
        {
            reticleActive = false;
        }

        void OnDestroy()
        {
            if (m_Interactor != null)
                m_Interactor.selectEntered.RemoveListener(OnSelectEntered);
        }

        void Update()
        {
            if (m_Interactor != null && UpdateReticleTarget())
                ActivateReticleAtTarget();
            else
                reticleActive = false;
        }

        void SetupReticlePrefab()
        {
            if (m_ReticleInstance != null)
                Destroy(m_ReticleInstance);

            if (m_ReticlePrefab != null)
            {
                m_ReticleInstance = Instantiate(m_ReticlePrefab);
                ConfigureReticleRendering();
            }
        }

        void ConfigureReticleRendering()
        {
            if (m_ReticleInstance == null)
                return;

            Renderer[] renderers = m_ReticleInstance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                renderer.sortingOrder = ReticleRenderQueue;
                renderer.rendererPriority = ReticleRendererPriority;

                Material[] materials = renderer.materials;
                foreach (Material material in materials)
                {
                    if (material == null)
                        continue;

                    material.renderQueue = ReticleRenderQueue;
                    if (material.HasProperty(ZTestProperty))
                        material.SetFloat(ZTestProperty, (float)CompareFunction.Always);
                    if (material.HasProperty(ZWriteProperty))
                        material.SetFloat(ZWriteProperty, 0f);
                }
            }
        }

        bool UpdateReticleTarget()
        {
            if (!m_DrawWhileSelecting && m_Interactor.hasSelection)
                return false;

            if (m_Interactor.disableVisualsWhenBlockedInGroup && m_Interactor.IsBlockedByInteractionWithinGroup())
                return false;

            // Hide reticle during near-field interaction (poke / direct grab range)
            if (m_HideInNearRegion && m_Interactor.selectionRegion.Value == NearFarInteractor.Region.Near)
                return false;

            var raycastPos = Vector3.zero;
            var raycastNormal = Vector3.zero;
            var hasRaycastHit = false;

            var endPointType = m_Interactor.TryGetCurveEndPoint(out var hitPoint);
            var normalType   = m_Interactor.TryGetCurveEndNormal(out var hitNormal);

            switch (endPointType)
            {
                case EndPointType.ValidCastHit:
                case EndPointType.AttachPoint:
                    raycastPos    = hitPoint;
                    raycastNormal = normalType != EndPointType.None ? hitNormal : Vector3.up;
                    hasRaycastHit = true;
                    break;

                case EndPointType.UI:
                    // Flip normal if we're hitting the back of a canvas
                    var rayOrigin = ((IXRRayProvider)m_Interactor).GetOrCreateRayOrigin();
                    var rayDir    = rayOrigin != null ? (hitPoint - rayOrigin.position).normalized : Vector3.forward;
                    var uiNormal  = normalType != EndPointType.None ? hitNormal : Vector3.back;
                    if (Vector3.Dot(rayDir, uiNormal) > 0f)
                        uiNormal *= -1f;
                    raycastPos    = hitPoint + uiNormal * m_UiSurfaceOffset;
                    raycastNormal = uiNormal;
                    hasRaycastHit = true;
                    break;

                case EndPointType.EmptyCastHit:
                    // Ray ended but no interactable — treat as no-hit
                    if (m_DrawOnNoHit)
                        raycastPos = hitPoint;
                    break;

                case EndPointType.None:
                    // Far cast inactive; fall back to sample points tail if requested
                    if (m_DrawOnNoHit && m_CurveProvider != null)
                    {
                        var samples = m_CurveProvider.samplePoints;
                        if (samples.IsCreated && samples.Length > 0)
                            raycastPos = samples[samples.Length - 1];
                    }
                    break;
            }

            m_HasRaycastHit = hasRaycastHit;

            if (hasRaycastHit || (m_DrawOnNoHit && endPointType != EndPointType.None))
            {
                m_TargetEndPoint  = Vector3.SmoothDamp(m_TargetEndPoint,  raycastPos,    ref m_SmoothedEndPointVelocity,  m_EndpointSmoothingTime);
                m_TargetEndNormal = Vector3.SmoothDamp(m_TargetEndNormal, raycastNormal, ref m_SmoothedEndNormalVelocity, m_EndpointSmoothingTime);
                return true;
            }

            return false;
        }

        void ActivateReticleAtTarget()
        {
            if (m_ReticleInstance == null)
                return;

            var relativeUp = (m_XROrigin != null && m_XROrigin.Origin != null)
                ? m_XROrigin.Origin.transform.up
                : Vector3.up;

            if (m_AlignPrefabWithSurfaceNormal && m_HasRaycastHit)
            {
                var vectorToProject = relativeUp;
                var dotProduct = Vector3.Dot(m_TargetEndNormal, vectorToProject);

                // Horizontal surface: align z-axis with interactor forward
                if (Mathf.Approximately(Mathf.Abs(dotProduct), 1f))
                    vectorToProject = m_Interactor.transform.forward * dotProduct;

                var forwardVector = Vector3.ProjectOnPlane(vectorToProject, m_TargetEndNormal);
                if (forwardVector != Vector3.zero)
                    m_ReticleInstance.transform.SetWorldPose(new Pose(m_TargetEndPoint, Quaternion.LookRotation(forwardVector, m_TargetEndNormal)));
                else
                    m_ReticleInstance.transform.position = m_TargetEndPoint;
            }
            else
            {
                var originPos = GetRayOriginPosition();
                m_ReticleInstance.transform.SetWorldPose(new Pose(m_TargetEndPoint,
                    Quaternion.LookRotation(relativeUp, (originPos - m_TargetEndPoint).normalized)));
            }

            var scaleFactor = m_PrefabScalingFactor;
            if (m_UndoDistanceScaling)
                scaleFactor *= Vector3.Distance(GetRayOriginPosition(), m_TargetEndPoint);

            m_ReticleInstance.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
            reticleActive = true;
        }

        Vector3 GetRayOriginPosition()
        {
            var rayOrigin = ((IXRRayProvider)m_Interactor).GetOrCreateRayOrigin();
            return rayOrigin != null ? rayOrigin.position : m_Interactor.transform.position;
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            reticleActive = false;
        }
    }
}
