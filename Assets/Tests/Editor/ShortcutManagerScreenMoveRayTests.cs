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
    }
}
