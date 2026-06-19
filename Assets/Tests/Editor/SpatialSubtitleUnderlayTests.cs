using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using XRVLC.Media;
using XRVLC.Subtitles;

namespace XRVLC.Tests
{
    [TestFixture]
    public class SpatialSubtitleUnderlayTests
    {
        private static string ProjectFile(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }

        [Test]
        public void PicoRenderSurface_RebuildLayerUsesUnderlayForEveryProjection()
        {
            string source = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/Rendering/PicoRenderSurface.cs"));

            StringAssert.Contains(
                "_compLayer.overlayType = PXR_CompositionLayer.OverlayType.Underlay;",
                source);
            StringAssert.DoesNotContain(
                "PXR_CompositionLayer.OverlayType.Overlay",
                source);
        }

        [Test]
        public void PicoRenderSurface_RebuildLayerBindsPicoLayerToWorldTransform()
        {
            string source = File.ReadAllText(ProjectFile("Assets/Scripts/Infrastructure/Rendering/PicoRenderSurface.cs"));
            string rebuildLayer = ExtractMethodBody(source, "RebuildLayer");
            string setGeometry = ExtractMethodBody(source, "SetGeometry");

            StringAssert.Contains("BindCompositionLayerPose();", rebuildLayer);
            StringAssert.Contains("BindCompositionLayerPose();", setGeometry);
            StringAssert.Contains("_compLayer.overlayTransform = transform;", source);
            StringAssert.Contains("_compLayer.RefreshCamera(", source);
        }

        [Test]
        public void VideoScreen_FlatModeClearsTransparentEyeBufferForUnderlay()
        {
            string source = File.ReadAllText(ProjectFile("Assets/Scripts/Services/Screen/VideoScreen.cs"));
            string awake = ExtractMethodBody(source, "Awake");
            string setupFlatMode = ExtractMethodBody(source, "SetupFlatMode");
            string setupImmersiveMode = ExtractMethodBody(source, "SetupImmersiveMode");
            string applyTransparentBackground = ExtractMethodBody(source, "ApplyTransparentUnderlayBackground");

            StringAssert.Contains("ApplyTransparentUnderlayBackground();", awake);
            StringAssert.Contains("ApplyTransparentUnderlayBackground();", setupFlatMode);
            StringAssert.Contains("ApplyTransparentUnderlayBackground();", setupImmersiveMode);
            StringAssert.Contains("_mainCamera.clearFlags = CameraClearFlags.SolidColor;", applyTransparentBackground);
            StringAssert.Contains("_mainCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);", applyTransparentBackground);
            StringAssert.Contains("backgroundBoard.gameObject.SetActive(false);", applyTransparentBackground);
            StringAssert.DoesNotContain("_savedClearFlags", source);
            StringAssert.DoesNotContain("_savedBackgroundColor", source);
        }

        [Test]
        public void SpatialSubtitleFallbackStaysAtAuthoredWorldPositionWhenHeadRotates()
        {
            Type serviceType = FindType("XRVLC.Media.SpatialSubtitleService");
            Assert.IsNotNull(serviceType, "SpatialSubtitleService should be available from Assembly-CSharp.");

            GameObject cameraObject = new GameObject("Subtitle Test Camera");
            GameObject serviceObject = new GameObject("Subtitle Service Under Test");

            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                serviceObject.transform.position = new Vector3(0f, 1.1f, 3.2f);

                Component service = serviceObject.AddComponent(serviceType);
                SetPrivateField(serviceType, service, "viewerCamera", camera);

                Vector3 authoredPosition = serviceObject.transform.position;
                InvokePrivate(serviceType, service, "HandleSubtitleCue", new SubtitleCue
                {
                    text = "Readable fixed subtitle",
                    source = "text",
                    startMs = 0,
                    endMs = 2000
                });

                AssertVector3(authoredPosition, serviceObject.transform.position);

                cameraObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                InvokePrivate(serviceType, service, "LateUpdate");

                AssertVector3(authoredPosition, serviceObject.transform.position);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serviceObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void SpatialSubtitleGeneratedTextUsesReadableVrDefaults()
        {
            Type serviceType = FindType("XRVLC.Media.SpatialSubtitleService");
            Assert.IsNotNull(serviceType, "SpatialSubtitleService should be available from Assembly-CSharp.");

            GameObject serviceObject = new GameObject("Subtitle Service Under Test");

            try
            {
                Component service = serviceObject.AddComponent(serviceType);
                InvokePrivate(serviceType, service, "HandleSubtitleCue", new SubtitleCue
                {
                    text = "A readable line of subtitle text",
                    source = "text",
                    startMs = 0,
                    endMs = 2000
                });

                object subtitleText = GetPrivateField(serviceType, service, "subtitleText");
                Assert.IsNotNull(subtitleText, "SpatialSubtitleService should create a TextMeshPro component.");

                float fontSize = Convert.ToSingle(subtitleText.GetType().GetProperty("fontSize").GetValue(subtitleText));
                Assert.GreaterOrEqual(fontSize, 0.36f);

                var textComponent = (Component)subtitleText;
                RectTransform rectTransform = textComponent.GetComponent<RectTransform>();
                Assert.IsNotNull(rectTransform, "Generated TextMeshPro should have a RectTransform.");
                Assert.GreaterOrEqual(rectTransform.sizeDelta.x, 4.4f);
                Assert.GreaterOrEqual(rectTransform.sizeDelta.y, 0.9f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serviceObject);
            }
        }

        [Test]
        public void SpatialSubtitleUsesVideoScreenAnchorWhenConfigured()
        {
            Type serviceType = FindType("XRVLC.Media.SpatialSubtitleService");
            Type videoScreenType = FindType("XRVLC.VideoScreen");
            Assert.IsNotNull(serviceType, "SpatialSubtitleService should be available from Assembly-CSharp.");
            Assert.IsNotNull(videoScreenType, "VideoScreen should be available from Assembly-CSharp.");

            GameObject screenObject = new GameObject("Video Screen");
            GameObject screenAnchorObject = new GameObject("Screen Anchor");
            GameObject serviceObject = new GameObject("Subtitle Service Under Test");

            try
            {
                screenObject.transform.SetPositionAndRotation(
                    new Vector3(0f, 1.5f, 6f),
                    Quaternion.Euler(0f, 180f, 0f));
                screenAnchorObject.transform.SetParent(screenObject.transform, false);

                Component videoScreen = screenObject.AddComponent(videoScreenType);
                SetPublicField(videoScreenType, videoScreen, "videoAnchor", screenAnchorObject.transform);

                Component service = serviceObject.AddComponent(serviceType);
                SetPrivateField(serviceType, service, "videoScreen", videoScreen);

                InvokePrivate(serviceType, service, "HandleSubtitleCue", new SubtitleCue
                {
                    text = "Screen anchored subtitle",
                    source = "text",
                    startMs = 0,
                    endMs = 2000
                });

                Vector3 expectedLocalOffset = new Vector3(0f, -0.5f + 1f / 15f, -0.08f);
                AssertVector3(
                    screenAnchorObject.transform.position + screenAnchorObject.transform.rotation * expectedLocalOffset,
                    serviceObject.transform.position);
                Assert.AreEqual(screenAnchorObject.transform.rotation, serviceObject.transform.rotation);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serviceObject);
                UnityEngine.Object.DestroyImmediate(screenAnchorObject);
                UnityEngine.Object.DestroyImmediate(screenObject);
            }
        }

        [Test]
        public void SpatialSubtitleFlatModePlacesSubtitleOneLineAboveScreenBottom()
        {
            Type serviceType = FindType("XRVLC.Media.SpatialSubtitleService");
            Type videoScreenType = FindType("XRVLC.VideoScreen");
            Assert.IsNotNull(serviceType, "SpatialSubtitleService should be available from Assembly-CSharp.");
            Assert.IsNotNull(videoScreenType, "VideoScreen should be available from Assembly-CSharp.");

            GameObject screenObject = new GameObject("Video Screen");
            GameObject screenAnchorObject = new GameObject("Screen Anchor");
            GameObject backgroundObject = new GameObject("Background Board");
            GameObject serviceObject = new GameObject("Subtitle Service Under Test");

            try
            {
                screenAnchorObject.transform.SetParent(screenObject.transform, false);
                backgroundObject.transform.SetParent(screenObject.transform, false);
                screenAnchorObject.transform.localScale = new Vector3(16f, 9f, 1f);
                backgroundObject.transform.localScale = new Vector3(16f, 9f, 1f);

                Component videoScreen = screenObject.AddComponent(videoScreenType);
                SetPublicField(videoScreenType, videoScreen, "videoAnchor", screenAnchorObject.transform);
                SetPublicField(videoScreenType, videoScreen, "backgroundBoard", backgroundObject.transform);

                Component service = serviceObject.AddComponent(serviceType);
                SetPrivateField(serviceType, service, "videoScreen", videoScreen);
                SetPrivateField(serviceType, service, "anchor", screenAnchorObject.transform);

                InvokePrivate(serviceType, service, "HandleSubtitleCue", new SubtitleCue
                {
                    text = "Bottom anchored subtitle",
                    source = "text",
                    startMs = 0,
                    endMs = 2000
                });

                float expectedLineHeight = 9f / 15f;
                Vector3 expectedLocalOffset = new Vector3(0f, -9f * 0.5f + expectedLineHeight, -0.08f);
                AssertVector3(
                    screenAnchorObject.transform.position + screenAnchorObject.transform.rotation * expectedLocalOffset,
                    serviceObject.transform.position);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serviceObject);
                UnityEngine.Object.DestroyImmediate(backgroundObject);
                UnityEngine.Object.DestroyImmediate(screenAnchorObject);
                UnityEngine.Object.DestroyImmediate(screenObject);
            }
        }

        [Test]
        public void SpatialSubtitleTextureVisibleLineHeightTracksScreenHeightFraction()
        {
            Type serviceType = FindType("XRVLC.Media.SpatialSubtitleService");
            Type videoScreenType = FindType("XRVLC.VideoScreen");
            Assert.IsNotNull(serviceType, "SpatialSubtitleService should be available from Assembly-CSharp.");
            Assert.IsNotNull(videoScreenType, "VideoScreen should be available from Assembly-CSharp.");

            GameObject screenObject = new GameObject("Video Screen");
            GameObject screenAnchorObject = new GameObject("Screen Anchor");
            GameObject backgroundObject = new GameObject("Background Board");
            GameObject serviceObject = new GameObject("Subtitle Service Under Test");

            try
            {
                screenAnchorObject.transform.SetParent(screenObject.transform, false);
                backgroundObject.transform.SetParent(screenObject.transform, false);
                backgroundObject.transform.localScale = new Vector3(16f, 9f, 1f);

                Component videoScreen = screenObject.AddComponent(videoScreenType);
                SetPublicField(videoScreenType, videoScreen, "videoAnchor", screenAnchorObject.transform);
                SetPublicField(videoScreenType, videoScreen, "backgroundBoard", backgroundObject.transform);

                Component service = serviceObject.AddComponent(serviceType);
                SetPrivateField(serviceType, service, "videoScreen", videoScreen);
                SetPrivateField(serviceType, service, "anchor", screenAnchorObject.transform);
                SetPrivateField(serviceType, service, "_textureRenderer", new FixedBitmapRenderer(960, 240));

                InvokePrivate(serviceType, service, "HandleSubtitleCue", new SubtitleCue
                {
                    text = "Readable texture subtitle",
                    source = "text",
                    startMs = 0,
                    endMs = 2000,
                    styleRuns = new[]
                    {
                        new SubtitleCueStyleRun
                        {
                            start = 0,
                            end = 25,
                            fontSize = 96f
                        }
                    }
                });

                var renderer = (MeshRenderer)GetPrivateField(serviceType, service, "subtitleTextureRenderer");
                Assert.IsNotNull(renderer, "Texture subtitle should create a MeshRenderer.");

                float expectedLineHeightMeters = 9f / 15f;
                float estimatedVisibleLinePx = 96f * 1.25f;
                float actualVisibleLineHeightMeters = renderer.transform.localScale.y * estimatedVisibleLinePx / 240f;
                Assert.AreEqual(expectedLineHeightMeters, actualVisibleLineHeightMeters, 0.0001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serviceObject);
                UnityEngine.Object.DestroyImmediate(backgroundObject);
                UnityEngine.Object.DestroyImmediate(screenAnchorObject);
                UnityEngine.Object.DestroyImmediate(screenObject);
            }
        }

        [Test]
        public void SpatialSubtitleTextureDefaultsPreferCrispVrRendering()
        {
            Type serviceType = FindType("XRVLC.Media.SpatialSubtitleService");
            Assert.IsNotNull(serviceType, "SpatialSubtitleService should be available from Assembly-CSharp.");

            GameObject serviceObject = new GameObject("Subtitle Service Under Test");

            try
            {
                Component service = serviceObject.AddComponent(serviceType);

                int widthPx = Convert.ToInt32(GetPrivateField(serviceType, service, "subtitleTextureWidthPx"));
                int maxHeightPx = Convert.ToInt32(GetPrivateField(serviceType, service, "subtitleTextureMaxHeightPx"));
                FilterMode filterMode = (FilterMode)GetPrivateField(serviceType, service, "subtitleTextureFilterMode");
                bool useMipMaps = Convert.ToBoolean(GetPrivateField(serviceType, service, "subtitleTextureUseMipMaps"));
                int anisoLevel = Convert.ToInt32(GetPrivateField(serviceType, service, "subtitleTextureAnisoLevel"));
                float mipMapBias = Convert.ToSingle(GetPrivateField(serviceType, service, "subtitleTextureMipMapBias"));
                Vector3 anchorLocalOffset = (Vector3)GetPrivateField(serviceType, service, "anchorLocalOffset");
                float fallbackDistanceMeters = Convert.ToSingle(GetPrivateField(serviceType, service, "fallbackDistanceMeters"));
                float lineHeightFraction = Convert.ToSingle(GetPrivateField(serviceType, service, "subtitleLineHeightScreenFraction"));

                Assert.GreaterOrEqual(widthPx, 4096);
                Assert.GreaterOrEqual(maxHeightPx, 1024);
                Assert.AreEqual(FilterMode.Trilinear, filterMode);
                Assert.IsTrue(useMipMaps);
                Assert.GreaterOrEqual(anisoLevel, 2);
                Assert.LessOrEqual(mipMapBias, 0f);
                Assert.AreEqual(0f, anchorLocalOffset.y, 0.0001f);
                Assert.LessOrEqual(anchorLocalOffset.z, -0.02f);
                Assert.GreaterOrEqual(fallbackDistanceMeters, 3.6f);
                Assert.AreEqual(1f / 15f, lineHeightFraction, 0.0001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serviceObject);
            }
        }

        private static string ExtractMethodBody(string source, string methodName)
        {
            int methodIndex = source.IndexOf(methodName, StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodIndex, 0, $"Could not find method {methodName}.");

            int openBrace = source.IndexOf('{', methodIndex);
            Assert.GreaterOrEqual(openBrace, 0, $"Could not find body for method {methodName}.");

            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                if (source[i] == '}') depth--;
                if (depth == 0)
                    return source.Substring(openBrace, i - openBrace + 1);
            }

            Assert.Fail($"Could not parse body for method {methodName}.");
            return string.Empty;
        }

        private static void SetPrivateField(Type type, object instance, string fieldName, object value)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Could not find field {fieldName}.");
            field.SetValue(instance, value);
        }

        private static void SetPublicField(Type type, object instance, string fieldName, object value)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(field, $"Could not find field {fieldName}.");
            field.SetValue(instance, value);
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null) return type;
            }

            return null;
        }

        private static object GetPrivateField(Type type, object instance, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Could not find field {fieldName}.");
            return field.GetValue(instance);
        }

        private static void InvokePrivate(Type type, object instance, string methodName, params object[] args)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Could not find method {methodName}.");
            method.Invoke(instance, args);
        }

        private static void AssertVector3(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.0001f);
            Assert.AreEqual(expected.y, actual.y, 0.0001f);
            Assert.AreEqual(expected.z, actual.z, 0.0001f);
        }

        private sealed class FixedBitmapRenderer : ISubtitleTextureRenderer
        {
            private readonly int _width;
            private readonly int _height;

            public FixedBitmapRenderer(int width, int height)
            {
                _width = width;
                _height = height;
            }

            public SubtitleBitmap Render(SubtitleCue cue, int widthPx, int maxHeightPx)
            {
                byte[] rgba = new byte[_width * _height * 4];
                rgba[3] = 255;
                return new SubtitleBitmap(_width, _height, _width * 4, rgba, true);
            }
        }
    }
}
