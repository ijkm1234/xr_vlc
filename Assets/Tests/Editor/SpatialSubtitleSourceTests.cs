using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace XRVLC.Tests
{
    [TestFixture]
    public class SpatialSubtitleSourceTests
    {
        private static string ProjectFile(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }

        [Test]
        public void LibassPlainTextExtractorKeepsReadableTextFromAnimatedAssPackets()
        {
            string source = File.ReadAllText(ProjectFile("vlc-android/libvlcjni/vlc/modules/codec/libass.c"));

            StringAssert.Contains("AssFindBestTextField", source);
            StringAssert.Contains("AssAppendCleanText", source);
            StringAssert.Contains("b_in_drawing", source);
            StringAssert.Contains("\\\\p0", source);
            StringAssert.Contains("i_candidate_commas", source);
        }

        [Test]
        public void SubtitleRenderingDefaultsUseHighResolutionReadableParameters()
        {
            string libassSource = File.ReadAllText(ProjectFile("vlc-android/libvlcjni/vlc/modules/codec/libass.c"));
            string modelSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/subtitle/SubtitleRenderModels.kt"));
            string rendererSource = File.ReadAllText(ProjectFile("vlc-android/application/vlc-android/src/org/videolan/vlc/bridge/subtitle/XrSubtitleSkiaRenderer.kt"));

            StringAssert.Contains("\\\"fontSize\\\":96", libassSource);
            StringAssert.Contains("fontSize = 96f", modelSource);
            StringAssert.Contains("val outlineStrokeWidth = outlineWidth * 2f", rendererSource);
            StringAssert.Contains("paint.strokeWidth = if (outline) outlineWidth * 2f else 0f", rendererSource);
            StringAssert.Contains("Paint.Join.ROUND", rendererSource);
            StringAssert.Contains("Paint.Cap.ROUND", rendererSource);
            StringAssert.DoesNotContain("paint.strokeWidth = if (outline) (style?.outlineWidth ?: 3f).coerceAtLeast(0f) else 0f", rendererSource);
        }

        [Test]
        public void SpatialSubtitleTextureUsesStableVrSampling()
        {
            string source = File.ReadAllText(ProjectFile("Assets/Scripts/Services/Playback/SpatialSubtitleService.cs"));

            StringAssert.Contains("FilterMode.Trilinear", source);
            StringAssert.Contains("subtitleTextureUseMipMaps", source);
            StringAssert.Contains("_subtitleTexture.SetPixelData(textureData, 0)", source);
            StringAssert.Contains("_subtitleTexture.Apply(subtitleTextureUseMipMaps, false)", source);
            StringAssert.Contains("_subtitleTexture.anisoLevel", source);
            StringAssert.Contains("_subtitleTexture.mipMapBias", source);
            StringAssert.DoesNotContain("_subtitleTexture.LoadRawTextureData(textureData)", source);
        }
    }
}
