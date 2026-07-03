using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace XRVLC.Tests
{
    [TestFixture]
    public class ShortcutManagerScreenMoveRayTests
    {
        [Test]
        public void GripScreenMove_HidesControllerRayVisualsWhileMoving()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/XR/Shortcuts/ShortcutManager.cs");
            string source = File.ReadAllText(path);

            StringAssert.Contains("rayVisualsToHideWhileMoving", source);
            StringAssert.Contains("SetScreenMoveRayVisualsVisible(false)", source);
            StringAssert.Contains("SetScreenMoveRayVisualsVisible(true)", source);
        }

        [Test]
        public void GripScreenMove_IsProcessedBeforeUiRayHoverGate()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/XR/Shortcuts/ShortcutManager.cs");
            string source = File.ReadAllText(path);

            int processGripMoveIndex = source.IndexOf("ProcessGripMove();", System.StringComparison.Ordinal);
            int uiBlockedIndex = source.IndexOf("if (IsUiInputBlocked())", System.StringComparison.Ordinal);

            Assert.GreaterOrEqual(processGripMoveIndex, 0);
            Assert.GreaterOrEqual(uiBlockedIndex, 0);
            Assert.Less(processGripMoveIndex, uiBlockedIndex);
        }

        [Test]
        public void GripScreenMove_UsesControllerRotationInsteadOfPosition()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/XR/Shortcuts/ShortcutManager.cs");
            string source = File.ReadAllText(path);

            StringAssert.Contains("CommonUsages.deviceRotation", source);
            StringAssert.DoesNotContain("CommonUsages.devicePosition", source);
        }

        [Test]
        public void NearFarLineVisual_UsesSystemStyleFixedStraightRay()
        {
            string leftPath = Path.Combine(
                Application.dataPath,
                "Samples/XR Interaction Toolkit/3.4.0/Starter Assets/Prefabs/Interactors/Left_NearFarInteractor.prefab");
            string rightPath = Path.Combine(
                Application.dataPath,
                "Samples/XR Interaction Toolkit/3.4.0/Starter Assets/Prefabs/Interactors/Right_NearFarInteractor.prefab");
            string visualPath = Path.Combine(
                Application.dataPath,
                "Scripts/XR/Visuals/SystemRayLineVisual.cs");

            string leftPrefab = File.ReadAllText(leftPath);
            string rightPrefab = File.ReadAllText(rightPath);
            string visualSource = File.ReadAllText(visualPath);

            StringAssert.Contains("m_Enabled: 0\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: 80e353695beb436ab39a90d9ecefaee6, type: 3}", leftPrefab);
            StringAssert.Contains("SystemRayLineVisual", leftPrefab);
            StringAssert.Contains("m_LineRenderer: {fileID: 3053154067257784704}", leftPrefab);
            StringAssert.Contains("m_LineOriginTransform: {fileID: 5745700813747042508}", leftPrefab);
            StringAssert.Contains("m_MaxLineDistance: 10", leftPrefab);
            StringAssert.Contains("m_LineDynamicsMode: 0", leftPrefab);
            StringAssert.Contains("m_RetractDelay: 0", leftPrefab);
            StringAssert.Contains("m_RetractDuration: 0", leftPrefab);
            StringAssert.Contains("m_EnableStabilization: 0", leftPrefab);
            StringAssert.DoesNotContain("m_EnableStabilization: 1", leftPrefab);
            StringAssert.DoesNotContain("m_SmoothlyCurveLine: 1", leftPrefab);
            StringAssert.Contains("m_SmoothlyCurveLine: 0", leftPrefab);
            StringAssert.Contains("m_SwapMaterials: 1", leftPrefab);
            StringAssert.Contains("m_BaseLineMaterial: {fileID: 2100000, guid: 6f3d696f7c3365846b6dc2402afb3d3e, type: 2}", leftPrefab);
            StringAssert.Contains("m_EmptyHitMaterial: {fileID: 2100000, guid: 6f3d696f7c3365846b6dc2402afb3d3e, type: 2}", leftPrefab);
            StringAssert.Contains("m_Materials:\n  - {fileID: 2100000, guid: 6f3d696f7c3365846b6dc2402afb3d3e, type: 2}", leftPrefab);
            StringAssert.Contains("m_StartColor: {r: 0.95, g: 0.985, b: 1, a: 0.95}", leftPrefab);
            StringAssert.Contains("m_EndColor: {r: 0.58, g: 0.73, b: 0.86, a: 0.08}", leftPrefab);
            StringAssert.Contains("lineRenderer.positionCount = 2;", visualSource);
            StringAssert.Contains("TryGetCurveEndPoint", visualSource);
            StringAssert.Contains("EndPointType.ValidCastHit", visualSource);
            StringAssert.Contains("EndPointType.EmptyCastHit", visualSource);
            StringAssert.Contains("EndPointType.UI", visualSource);
            StringAssert.Contains("lineRenderer.SetPosition(1, GetLineEndPoint(origin, direction));", visualSource);
            StringAssert.Contains("m_LineRenderer.enabled = false;", visualSource);
            StringAssert.Contains("typeName.IndexOf(\"LineVisual\", StringComparison.OrdinalIgnoreCase) >= 0", File.ReadAllText(Path.Combine(
                Application.dataPath,
                "Scripts/Infrastructure/VlcBridge/Launcher/VlcFocusRestoreHandler.cs")));
            StringAssert.DoesNotContain("propertyPath: m_LineDynamicsMode", rightPrefab);
        }
    }
}
