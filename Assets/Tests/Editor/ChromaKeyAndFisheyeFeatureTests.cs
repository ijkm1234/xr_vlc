using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace XRVLC.Tests
{
    [TestFixture]
    public class ChromaKeyAndFisheyeFeatureTests
    {
        [Test]
        public void ChromaKeySettings_DefaultsAndHexParsingMatchProductDefaults()
        {
            Type type = typeof(VideoProjection).Assembly.GetType("XRVLC.ChromaKeySettings");
            Assert.NotNull(type, "ChromaKeySettings must live in XRVLC.Shared.");

            object settings = type.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            Assert.NotNull(settings);
            Assert.AreEqual(false, ReadProperty<bool>(settings, "Enabled"));
            Assert.AreEqual(0.2f, ReadProperty<float>(settings, "ColorRange"), 0.0001f);
            Assert.AreEqual(0.1f, ReadProperty<float>(settings, "Falloff"), 0.0001f);
            Assert.AreEqual(false, ReadProperty<bool>(settings, "EdgeSmoothEnabled"));
            Assert.AreEqual(false, ReadProperty<bool>(settings, "ClipBlackEnabled"));
            Assert.AreEqual(false, ReadProperty<bool>(settings, "ClipWhiteEnabled"));
            Assert.AreEqual(false, ReadProperty<bool>(settings, "DespillEnabled"));
            Assert.AreEqual("#2BE640", type.GetMethod("ToHex")?.Invoke(settings, null));

            object[] args = { "#149E59", null };
            Assert.AreEqual(true, type.GetMethod("TryParseHex")?.Invoke(null, args));
            Assert.AreEqual("#149E59", type.GetMethod("ToHex")?.Invoke(args[1], null));
        }

        [Test]
        public void ChromaKeyMath_UsesYcgcoChromaDistanceAndThresholdFalloff()
        {
            Type type = typeof(VideoProjection).Assembly.GetType("XRVLC.ChromaKeyMath");
            Assert.NotNull(type, "ChromaKeyMath must live in XRVLC.Shared.");

            MethodInfo distance = type.GetMethod("CalculateYcgcoDistance", BindingFlags.Public | BindingFlags.Static);
            MethodInfo alpha = type.GetMethod("CalculateAlpha", BindingFlags.Public | BindingFlags.Static);
            MethodInfo clip = type.GetMethod("ApplyClip", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(distance);
            Assert.NotNull(alpha);
            Assert.NotNull(clip);

            Color darkerGreen = new Color(0.1f, 0.7f, 0.1f, 1f);
            Color lighterGreen = new Color(0.2f, 0.8f, 0.2f, 1f);
            float luminanceIndependentDistance = (float)distance.Invoke(
                null,
                new object[] { darkerGreen, lighterGreen });
            Assert.AreEqual(0f, luminanceIndependentDistance, 0.0001f);
            Assert.Greater(
                (float)distance.Invoke(null, new object[] { darkerGreen, Color.red }),
                0.25f);

            Assert.AreEqual(0f, (float)alpha.Invoke(null, new object[] { 0.2f, 0.2f, 0.1f }), 0.0001f);
            Assert.AreEqual(1f, (float)alpha.Invoke(null, new object[] { 0.3f, 0.2f, 0.1f }), 0.0001f);
            Assert.That((float)alpha.Invoke(null, new object[] { 0.25f, 0.2f, 0.1f }), Is.InRange(0.45f, 0.55f));

            Assert.AreEqual(0f, (float)clip.Invoke(null, new object[] { 0.05f, true, false }), 0.0001f);
            Assert.AreEqual(1f, (float)clip.Invoke(null, new object[] { 0.95f, false, true }), 0.0001f);
            Assert.AreEqual(0.5f, (float)clip.Invoke(null, new object[] { 0.5f, true, true }), 0.0001f);
            Assert.AreEqual(0.37f, (float)clip.Invoke(null, new object[] { 0.37f, false, false }), 0.0001f);
        }

        [TestCase(FisheyeProjectionFormula.Equidistant, 0.5f, 0.5f)]
        [TestCase(FisheyeProjectionFormula.EquisolidAngle, 0.5f, 0.5411961f)]
        [TestCase(FisheyeProjectionFormula.Stereographic, 0.5f, 0.41421356f)]
        [TestCase(FisheyeProjectionFormula.Orthographic, 0.5f, 0.70710678f)]
        public void FisheyeProjectionFormula_MapsHalfAngleUsingStandardFormula(
            FisheyeProjectionFormula formula,
            float normalizedTheta,
            float expectedRadius)
        {
            Assert.AreEqual(expectedRadius, FisheyeProjectionMath.Radius(formula, normalizedTheta), 0.0001f);
        }

        [Test]
        public void PlaybackAndNativeBridge_CacheProcessingSettingsAndUpdateMapperWithoutSurfaceRebuild()
        {
            string playback = Read("Assets/Scripts/Services/Playback/PlaybackService.cs");
            string unityBridge = Read("Assets/Scripts/Infrastructure/VlcBridge/Playback/VlcPlaybackBridge.cs");
            string androidBridge = Read("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/PlaybackServiceBridge.kt");
            string mapper = Read("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/XrSurfaceMapper.kt");

            StringAssert.Contains("CurrentChromaKeySettings", playback);
            StringAssert.Contains("SetChromaKeyEnabled", playback);
            StringAssert.Contains("SetChromaKeyColor", playback);
            StringAssert.Contains("SetChromaKeyColorRange", playback);
            StringAssert.Contains("SetChromaKeyFalloff", playback);
            StringAssert.Contains("SetChromaKeyEdgeSmoothEnabled", playback);
            StringAssert.Contains("SetChromaKeyClipBlackEnabled", playback);
            StringAssert.Contains("SetChromaKeyClipWhiteEnabled", playback);
            StringAssert.Contains("SetChromaKeyDespillEnabled", playback);
            StringAssert.Contains("RequestChromaKeyColorExtraction", playback);
            StringAssert.DoesNotContain("ShouldEnableChromaKey", playback);
            StringAssert.Contains("SetVideoSurfaceProcessingParameters", unityBridge);
            StringAssert.Contains("RequestVideoSurfaceChromaKeyColorExtraction", unityBridge);
            StringAssert.Contains("OnChromaKeyColorExtracted", unityBridge);
            StringAssert.Contains("setVideoSurfaceProcessingParameters", androidBridge);
            StringAssert.Contains("requestVideoSurfaceChromaKeyColorExtraction", androidBridge);
            StringAssert.Contains("updateProcessingParameters", androidBridge);
            StringAssert.Contains("fun updateProcessingParameters", mapper);
            StringAssert.Contains("requestDominantColor", mapper);
        }

        [Test]
        public void MapperShader_UsesFourFisheyeFormulasYcgcoKeyingAndPostProcessing()
        {
            string mapper = Read("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/XrSurfaceMapper.kt");

            StringAssert.Contains("uniform int uFisheyeProjectionFormula", mapper);
            StringAssert.Contains("sin(theta * 0.5)", mapper);
            StringAssert.Contains("tan(theta * 0.5)", mapper);
            StringAssert.Contains("sin(theta)", mapper);
            StringAssert.Contains("vec3 rgbToYcgco", mapper);
            StringAssert.Contains("float chromaDistance", mapper);
            StringAssert.DoesNotContain("vec3 rgbToHsv", mapper);
            StringAssert.Contains("vec3 uChromaKeyColor", mapper);
            StringAssert.Contains("float uChromaKeyRange", mapper);
            StringAssert.Contains("float uChromaKeyFalloff", mapper);
            StringAssert.Contains("uChromaKeyEdgeSmoothEnabled", mapper);
            StringAssert.Contains("uChromaKeyClipBlackEnabled", mapper);
            StringAssert.Contains("uChromaKeyClipWhiteEnabled", mapper);
            StringAssert.Contains("uChromaKeyDespillEnabled", mapper);
            StringAssert.Contains("EDGE_SMOOTH_BLEND", mapper);
            StringAssert.Contains("CLIP_BLACK_POINT", mapper);
            StringAssert.Contains("CLIP_WHITE_POINT", mapper);
            StringAssert.Contains("DESPILL_STRENGTH", mapper);
            StringAssert.Contains("float pitch = (localUv.y - 0.5) * PI;", mapper);
            StringAssert.DoesNotContain("float pitch = (0.5 - localUv.y) * PI;", mapper);
            StringAssert.Contains("vec2 inputUv = vec2(sampledUv.x, 1.0 - sampledUv.y)", mapper);
            StringAssert.Contains("gl_FragColor = vec4(rgb, alpha)", mapper);
        }

        private static T ReadProperty<T>(object value, string name)
        {
            return (T)value.GetType().GetProperty(name)?.GetValue(value);
        }

        private static string Read(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string basePath = relativePath.StartsWith("vlc-android/", StringComparison.Ordinal)
                ? Directory.GetParent(projectRoot).FullName
                : projectRoot;
            return File.ReadAllText(Path.Combine(basePath, relativePath));
        }
    }
}
