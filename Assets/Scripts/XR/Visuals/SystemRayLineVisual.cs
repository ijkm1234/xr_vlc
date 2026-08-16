using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace XR.Visuals
{
    [AddComponentMenu("XR/Visual/System Ray Line Visual")]
    [DisallowMultipleComponent]
    public sealed class SystemRayLineVisual : MonoBehaviour
    {
        [SerializeField]
        LineRenderer m_LineRenderer;

        [SerializeField]
        Transform m_LineOriginTransform;

        [SerializeField]
        float m_MaxLineDistance = 10f;

        [SerializeField]
        float m_StartWidth = 0.0045f;

        [SerializeField]
        float m_EndWidth = 0.0025f;

        [SerializeField]
        Color m_StartColor = new Color(0.95f, 0.985f, 1f, 0.95f);

        [SerializeField]
        Color m_EndColor = new Color(0.58f, 0.73f, 0.86f, 0.08f);

        private Gradient _lineGradient;
        private ICurveInteractionDataProvider _curveProvider;

        private void Awake()
        {
            if (m_LineRenderer == null)
                m_LineRenderer = GetComponent<LineRenderer>();

            if (m_LineOriginTransform == null)
                m_LineOriginTransform = transform;

            ResolveCurveProvider();
            ConfigureLineRenderer();
        }

        private void OnEnable()
        {
            Application.onBeforeRender += DrawLine;
            DrawLine();
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= DrawLine;
            if (m_LineRenderer != null)
                m_LineRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            DrawLine();
        }

        private void ConfigureLineRenderer()
        {
            LineRenderer lineRenderer = m_LineRenderer;
            if (lineRenderer == null)
                return;

            if (_lineGradient == null)
            {
                _lineGradient = new Gradient();
                _lineGradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(m_StartColor, 0f),
                        new GradientColorKey(m_EndColor, 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(m_StartColor.a, 0f),
                        new GradientAlphaKey(m_EndColor.a, 1f)
                    });
            }

            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
            lineRenderer.startWidth = m_StartWidth;
            lineRenderer.endWidth = m_EndWidth;
            lineRenderer.colorGradient = _lineGradient;
        }

        private void DrawLine()
        {
            LineRenderer lineRenderer = m_LineRenderer;
            Transform originTransform = m_LineOriginTransform;
            if (lineRenderer == null || originTransform == null)
                return;

            ConfigureLineRenderer();

            Vector3 origin = originTransform.position;
            Vector3 direction = originTransform.forward;
            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, GetLineEndPoint(origin, direction));
        }

        private Vector3 GetLineEndPoint(Vector3 origin, Vector3 direction)
        {
            ICurveInteractionDataProvider curveProvider = GetCurveProvider();
            if (curveProvider != null && curveProvider.isActive)
            {
                EndPointType endPointType = curveProvider.TryGetCurveEndPoint(out Vector3 endPoint);
                switch (endPointType)
                {
                    case EndPointType.EmptyCastHit:
                    case EndPointType.ValidCastHit:
                    case EndPointType.AttachPoint:
                    case EndPointType.UI:
                        return endPoint;
                }
            }

            return origin + direction * m_MaxLineDistance;
        }

        private ICurveInteractionDataProvider GetCurveProvider()
        {
            if (_curveProvider == null)
                ResolveCurveProvider();

            return _curveProvider;
        }

        private void ResolveCurveProvider()
        {
            MonoBehaviour[] behaviours = GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICurveInteractionDataProvider curveProvider)
                {
                    _curveProvider = curveProvider;
                    return;
                }
            }
        }
    }
}
