using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace XRVLC.Tests
{
    [TestFixture]
    public class XrReticleVisualSourceTests
    {
        [Test]
        public void NearFarReticleVisual_OffsetsUiReticleAwayFromCanvasSurface()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/XR/Visuals/NearFarReticleVisual.cs"));
            string scene = File.ReadAllText(Path.Combine(Application.dataPath, "Scenes/MainVRScene.unity"));

            StringAssert.Contains("m_UiSurfaceOffset", source);
            StringAssert.Contains("m_UiSurfaceOffset = 0.006f", source);
            StringAssert.Contains("case EndPointType.UI", source);
            StringAssert.Contains("hitPoint + uiNormal * m_UiSurfaceOffset", source);
            StringAssert.Contains("ConfigureReticleRendering();", source);
            StringAssert.Contains("ReticleRenderQueue = 5000", source);
            StringAssert.Contains("CompareFunction.Always", source);
            StringAssert.Contains("material.renderQueue = ReticleRenderQueue", source);
            StringAssert.Contains("material.SetFloat(ZWriteProperty, 0f)", source);
            StringAssert.Contains("m_UiSurfaceOffset: 0.006", scene);
            StringAssert.Contains("m_PrefabScalingFactor: 0.75", scene);
        }
    }
}
