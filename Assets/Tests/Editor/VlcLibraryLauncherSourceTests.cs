using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace XRVLC.Tests
{
    [TestFixture]
    public class VlcLibraryLauncherSourceTests
    {
        private static string ReadLauncherSource()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Scripts/Infrastructure/VlcBridge/Launcher/VlcLibraryLauncher.cs");
            return File.ReadAllText(path);
        }

        [Test]
        public void SessionFocusState_UsesNullableLastState()
        {
            string source = ReadLauncherSource();

            StringAssert.Contains("private bool? m_LastSessionFocused;", source);
            StringAssert.Contains("m_LastSessionFocused.HasValue", source);
            StringAssert.Contains("focused == m_LastSessionFocused.Value", source);
            Assert.IsFalse(source.Contains("private bool m_SessionWasFocused;"));
        }

        [Test]
        public void StartupLaunch_OpensVlcLibraryAfterInitializationAndPermissionReturn()
        {
            string source = ReadLauncherSource();

            StringAssert.Contains("private bool m_OpenVlcLibraryOnStart = true;", source);
            StringAssert.Contains("StartCoroutine(OpenVlcLibraryOnStart());", source);
            StringAssert.Contains("private IEnumerator OpenVlcLibraryOnStart()", source);
            StringAssert.Contains("m_OpenVlcAfterPermissionGranted = true;", source);
            StringAssert.Contains("private void TryOpenVlcAfterPermissionGranted()", source);
        }

        [Test]
        public void AndroidLaunch_RestoresExistingVlcTaskBeforeStartingFallback()
        {
            string source = ReadLauncherSource();

            StringAssert.Contains("RestoreVlcTaskOrStartFallback(currentActivity);", source);
            StringAssert.Contains("restoreVlcTask", source);
            StringAssert.Contains("if (bridge.CallStatic<bool>(\"restoreVlcTask\", currentActivity))", source);
            StringAssert.Contains("FlagActivityNewTask", source);
            StringAssert.Contains("addFlags\", FlagActivityNewTask", source);
            StringAssert.DoesNotContain("ShowLastVlcActivityOrStartFallback", source);
            StringAssert.DoesNotContain("showLastVlcActivity", source);
        }
    }
}
