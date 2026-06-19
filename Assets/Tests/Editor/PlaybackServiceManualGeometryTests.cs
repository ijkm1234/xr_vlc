using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace XRVLC.Tests
{
    [TestFixture]
    public class PlaybackServiceManualGeometryTests
    {
        [Test]
        public void LoadAndPlay_ClearsManualGeometryOverrideForNewMedia()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs");
            string source = File.ReadAllText(path);
            string method = ExtractMethod(source, "LoadAndPlay");

            StringAssert.Contains("_hasManualGeometryOverride = false", method);
        }

        [Test]
        public void SetManualVideoGeometry_RebuildsWhenStereoLayoutChanges()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs");
            string source = File.ReadAllText(path);
            string method = ExtractMethod(source, "SetManualVideoGeometry");

            StringAssert.Contains("ShouldRebuildForManualGeometryChange", method);
            StringAssert.Contains("RebuildAndApplyGeometry", method);
        }

        [Test]
        public void SetManualVideoGeometry_CanUpdateExistingLayerWithoutRebuild()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs");
            string source = File.ReadAllText(path);
            string method = ExtractMethod(source, "SetManualVideoGeometry");

            StringAssert.Contains("ApplyManualGeometryWithoutRebuild", method);
        }

        [Test]
        public void ManualGeometryChange_RebuildsWhenProjectionStereoOrCurveChanges()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Playback/PlaybackService.cs");
            string source = File.ReadAllText(path);
            string method = ExtractMethod(source, "ShouldRebuildForManualGeometryChange", "private static bool");

            StringAssert.Contains("previousGeometry.Projection != nextGeometry.Projection", method);
            StringAssert.Contains("previousGeometry.Stereo != nextGeometry.Stereo", method);
            StringAssert.Contains("previousGeometry.CurveMode != nextGeometry.CurveMode", method);
        }

        [Test]
        public void VideoScreen_UsesPicoCylinderScaleSemantics()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreen.cs");
            string source = File.ReadAllText(path);
            string method = ExtractMethod(source, "FitVideoSize");

            StringAssert.Contains("_currentProjection == VideoProjection.Cylinder", method);
            StringAssert.Contains("FlatVideoCurveMetrics.GetCylinderRadius(_currentCurveMode)", method);
            StringAssert.Contains("FlatVideoCurveMetrics.GetCylinderRadius(_currentCurveMode) * _flatZoomScale", method);
            StringAssert.Contains("new Vector3(targetSize.x, targetSize.y, cylinderRadius)", method);
            StringAssert.DoesNotContain("_currentProjection == VideoProjection.Cylinder ||", method);
        }

        [Test]
        public void VideoScreen_DefaultFlatZoomUpperLimitIsThreePointZero()
        {
            string path = Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreen.cs");
            string source = File.ReadAllText(path);

            StringAssert.Contains("public float maxFlatZoomScale = 3.0f", source);
        }

        [Test]
        public void MainScene_SerializesFlatZoomUpperLimitAsThreePointZero()
        {
            string path = Path.Combine(Application.dataPath, "Scenes/MainVRScene.unity");
            string scene = File.ReadAllText(path);

            StringAssert.Contains("maxFlatZoomScale: 3", scene);
        }

        [Test]
        public void MainScene_UsesTransparentSolidColorBackgroundByDefault()
        {
            string path = Path.Combine(Application.dataPath, "Scenes/MainVRScene.unity");
            string scene = File.ReadAllText(path);

            StringAssert.Contains("m_SkyboxMaterial: {fileID: 0}", scene);
            StringAssert.Contains("m_ClearFlags: 2", scene);
            StringAssert.Contains("m_BackGroundColor: {r: 0, g: 0, b: 0, a: 0}", scene);
        }

        [Test]
        public void MainScene_DisablesXriTeleportMode()
        {
            string path = Path.Combine(Application.dataPath, "Scenes/MainVRScene.unity");
            string scene = File.ReadAllText(path);

            Assert.That(Regex.Matches(scene, @"m_TeleportInteractor: \{fileID: 0\}").Count, Is.EqualTo(2));
            Assert.That(Regex.Matches(scene, @"m_TeleportMode: \{fileID: 0\}").Count, Is.EqualTo(2));
            Assert.That(Regex.Matches(scene, @"m_TeleportModeCancel: \{fileID: 0\}").Count, Is.EqualTo(2));
            Assert.That(Regex.Matches(scene, @"m_NearFarEnableTeleportDuringNearInteraction: 0").Count, Is.EqualTo(2));
        }

        [Test]
        public void FitVideoSize_CylinderKeepsFlatWidthHeightAndStoresRadiusInZScale()
        {
            System.Type screenType = System.Type.GetType("XRVLC.VideoScreen, Assembly-CSharp");
            Assert.IsNotNull(screenType, "VideoScreen type should be available in Assembly-CSharp.");

            GameObject root = new GameObject("VideoScreen");
            GameObject anchor = new GameObject("VideoAnchor");
            GameObject background = new GameObject("BackgroundBoard");

            try
            {
                anchor.transform.SetParent(root.transform, false);
                background.transform.SetParent(root.transform, false);
                background.transform.localScale = new Vector3(16f, 9f, 1f);

                Component screen = root.AddComponent(screenType);
                screenType.GetField("videoAnchor").SetValue(screen, anchor.transform);
                screenType.GetField("backgroundBoard").SetValue(screen, background.transform);

                screenType.GetMethod("SetGeometry").Invoke(screen, new object[] { XRVLC.VideoProjection.Cylinder, XRVLC.StereoMode.Mono, XRVLC.FlatVideoCurveMode.Small });
                screenType.GetMethod("FitVideoSize").Invoke(screen, new object[] { 1920u, 1080u, 0f });

                Assert.That(anchor.transform.localScale.x, Is.EqualTo(16f).Within(0.001f));
                Assert.That(anchor.transform.localScale.y, Is.EqualTo(9f).Within(0.001f));
                Assert.That(anchor.transform.localScale.z, Is.EqualTo(32f).Within(0.001f));

                screenType.GetMethod("SetGeometry").Invoke(screen, new object[] { XRVLC.VideoProjection.Cylinder, XRVLC.StereoMode.Mono, XRVLC.FlatVideoCurveMode.Large });
                screenType.GetMethod("FitVideoSize").Invoke(screen, new object[] { 1920u, 1080u, 0f });

                Assert.That(anchor.transform.localScale.x, Is.EqualTo(16f).Within(0.001f));
                Assert.That(anchor.transform.localScale.y, Is.EqualTo(9f).Within(0.001f));
                Assert.That(anchor.transform.localScale.z, Is.EqualTo(16f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SetGeometry_CylinderOffsetsLayerCenterSoSurfaceMatchesFlatPosition()
        {
            System.Type screenType = System.Type.GetType("XRVLC.VideoScreen, Assembly-CSharp");
            Assert.IsNotNull(screenType, "VideoScreen type should be available in Assembly-CSharp.");

            GameObject root = new GameObject("VideoScreen");
            GameObject anchor = new GameObject("VideoAnchor");
            GameObject background = new GameObject("BackgroundBoard");

            try
            {
                root.transform.position = new Vector3(0f, 2.5f, 15f);
                anchor.transform.SetParent(root.transform, false);
                background.transform.SetParent(root.transform, false);
                background.transform.localScale = new Vector3(16f, 9f, 1f);

                Component screen = root.AddComponent(screenType);
                screenType.GetField("videoAnchor").SetValue(screen, anchor.transform);
                screenType.GetField("backgroundBoard").SetValue(screen, background.transform);

                screenType.GetMethod("SetGeometry").Invoke(screen, new object[] { XRVLC.VideoProjection.Cylinder, XRVLC.StereoMode.Mono, XRVLC.FlatVideoCurveMode.Large });

                Vector3 expectedSurfaceCenter = background.transform.position + background.transform.forward * -0.001f;
                Vector3 actualSurfaceCenter = anchor.transform.position + anchor.transform.forward * XRVLC.FlatVideoCurveMetrics.LargeCurveCylinderRadius;
                AssertVector3(expectedSurfaceCenter, actualSurfaceCenter);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyFlatZoomDelta_CylinderScalesRadiusWithArcSize()
        {
            System.Type screenType = System.Type.GetType("XRVLC.VideoScreen, Assembly-CSharp");
            Assert.IsNotNull(screenType, "VideoScreen type should be available in Assembly-CSharp.");

            GameObject root = new GameObject("VideoScreen");
            GameObject anchor = new GameObject("VideoAnchor");
            GameObject background = new GameObject("BackgroundBoard");

            try
            {
                root.transform.position = new Vector3(0f, 1.5f, 17f);
                anchor.transform.SetParent(root.transform, false);
                background.transform.SetParent(root.transform, false);
                background.transform.localScale = new Vector3(16f, 9f, 1f);

                Component screen = root.AddComponent(screenType);
                screenType.GetField("videoAnchor").SetValue(screen, anchor.transform);
                screenType.GetField("backgroundBoard").SetValue(screen, background.transform);

                screenType.GetMethod("SetGeometry").Invoke(screen, new object[] { XRVLC.VideoProjection.Cylinder, XRVLC.StereoMode.Mono, XRVLC.FlatVideoCurveMode.Large });
                screenType.GetMethod("FitVideoSize").Invoke(screen, new object[] { 1920u, 1080u, 0f });
                screenType.GetMethod("ApplyFlatZoomDelta").Invoke(screen, new object[] { -2f });

                Assert.That((float)screenType.GetProperty("FlatZoomScale").GetValue(screen), Is.EqualTo(1.2f).Within(0.001f));
                Assert.That(anchor.transform.localScale.x, Is.EqualTo(19.2f).Within(0.001f));
                Assert.That(anchor.transform.localScale.y, Is.EqualTo(10.8f).Within(0.001f));
                Assert.That(anchor.transform.localScale.z, Is.EqualTo(19.2f).Within(0.001f));
                Vector3 expectedSurfaceCenter = background.transform.position + background.transform.forward * -0.001f;
                Vector3 actualSurfaceCenter = anchor.transform.position + anchor.transform.forward * 9.6f;
                AssertVector3(expectedSurfaceCenter, actualSurfaceCenter);
                Assert.That(root.transform.position.z, Is.EqualTo(17f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyFlatZoomDelta_ScalesFlatFitSizeWithoutMovingRoot()
        {
            System.Type screenType = System.Type.GetType("XRVLC.VideoScreen, Assembly-CSharp");
            Assert.IsNotNull(screenType, "VideoScreen type should be available in Assembly-CSharp.");

            GameObject root = new GameObject("VideoScreen");
            GameObject anchor = new GameObject("VideoAnchor");
            GameObject background = new GameObject("BackgroundBoard");

            try
            {
                root.transform.position = new Vector3(0f, 1.5f, 17f);
                anchor.transform.SetParent(root.transform, false);
                background.transform.SetParent(root.transform, false);
                background.transform.localScale = new Vector3(16f, 9f, 1f);

                Component screen = root.AddComponent(screenType);
                screenType.GetField("videoAnchor").SetValue(screen, anchor.transform);
                screenType.GetField("backgroundBoard").SetValue(screen, background.transform);

                screenType.GetMethod("SetGeometry").Invoke(screen, new object[] { XRVLC.VideoProjection.Flat, XRVLC.StereoMode.Mono, XRVLC.FlatVideoCurveMode.None });
                screenType.GetMethod("FitVideoSize").Invoke(screen, new object[] { 1920u, 1080u, 0f });
                screenType.GetMethod("ApplyFlatZoomDelta").Invoke(screen, new object[] { -2f });

                Assert.That((float)screenType.GetProperty("FlatZoomScale").GetValue(screen), Is.EqualTo(1.2f).Within(0.001f));
                Assert.That(anchor.transform.localScale.x, Is.EqualTo(19.2f).Within(0.001f));
                Assert.That(anchor.transform.localScale.y, Is.EqualTo(10.8f).Within(0.001f));
                Assert.That(root.transform.position.z, Is.EqualTo(17f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static string ExtractMethod(string source, string methodName, string returnType = "public void")
        {
            Match signature = Regex.Match(source, Regex.Escape(returnType) + @" " + methodName + @"\s*\(");
            Assert.IsTrue(signature.Success, $"Could not find {methodName}.");

            int braceStart = source.IndexOf('{', signature.Index);
            Assert.GreaterOrEqual(braceStart, 0, $"Could not find body for {methodName}.");

            int depth = 0;
            for (int i = braceStart; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(signature.Index, i - signature.Index + 1);
                }
            }

            Assert.Fail($"Could not parse body for {methodName}.");
            return string.Empty;
        }

        private static void AssertVector3(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f));
        }
    }
}
