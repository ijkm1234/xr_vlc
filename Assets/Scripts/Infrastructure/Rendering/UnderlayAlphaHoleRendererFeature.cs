using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace XRVLC
{
    public sealed class UnderlayAlphaHoleRendererFeature : ScriptableRendererFeature
    {
        private const string ShaderName = "Hidden/XRVLC/UnderlayAlphaHole";

        [SerializeField]
        private Shader m_AlphaHoleShader;

        private Material _material;
        private UnderlayAlphaHolePass _pass;
        private bool _warnedMissingShader;
        private readonly List<UnderlayAlphaHoleEntry> _holes = new();

        public override void Create()
        {
            _pass ??= new UnderlayAlphaHolePass();
            _pass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            _pass.SetMaterial(EnsureMaterial());
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview)
                return;

            UnderlayAlphaHoleRegistry.CollectActiveHoles(_holes);
            if (_holes.Count == 0)
                return;

            Material material = EnsureMaterial();
            if (material == null)
            {
                if (!_warnedMissingShader)
                {
                    _warnedMissingShader = true;
                    Debug.LogWarning($"[UnderlayAlphaHole] Shader '{ShaderName}' was not found; flat underlay alpha hole pass is disabled.");
                }

                return;
            }

            _pass.SetMaterial(material);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }

        private Material EnsureMaterial()
        {
            if (_material != null)
                return _material;

            Shader shader = m_AlphaHoleShader != null ? m_AlphaHoleShader : Shader.Find(ShaderName);
            if (shader == null)
                return null;

            _material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return _material;
        }

        private sealed class UnderlayAlphaHolePass : ScriptableRenderPass
        {
            private Material _material;
            private readonly List<UnderlayAlphaHoleEntry> _holes = new();

            public UnderlayAlphaHolePass()
            {
                profilingSampler = new ProfilingSampler("Underlay Alpha Hole");
            }

            public void SetMaterial(Material material)
            {
                _material = material;
            }

            private sealed class PassData
            {
                public UnderlayAlphaHoleEntry[] holes;
                public Material material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.camera.cameraType == CameraType.Preview)
                    return;

                UnderlayAlphaHoleRegistry.CollectActiveHoles(_holes);
                if (_holes.Count == 0)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (!resourceData.activeColorTexture.IsValid())
                    return;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Underlay Alpha Hole", out PassData passData, profilingSampler))
                {
                    passData.holes = _holes.ToArray();
                    passData.material = _material;

                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        for (int i = 0; i < data.holes.Length; i++)
                        {
                            UnderlayAlphaHoleEntry hole = data.holes[i];
                            context.cmd.DrawMesh(hole.Mesh, hole.Matrix, data.material, 0, 0);
                        }
                    });
                }
            }

#if !UNITY_6000_0_OR_NEWER
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_material == null)
                    return;

                UnderlayAlphaHoleRegistry.CollectActiveHoles(_holes);
                if (_holes.Count == 0)
                    return;

                CommandBuffer cmd = CommandBufferPool.Get("Underlay Alpha Hole");
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    for (int i = 0; i < _holes.Count; i++)
                    {
                        UnderlayAlphaHoleEntry hole = _holes[i];
                        cmd.DrawMesh(hole.Mesh, hole.Matrix, _material, 0, 0);
                    }
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#endif
        }
    }
}
