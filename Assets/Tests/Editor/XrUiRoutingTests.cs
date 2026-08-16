using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR;
using XRVLC.UI.XR;

namespace XRVLC.Tests
{
    [TestFixture]
    public class XrUiRoutingTests
    {
        [Test]
        public void Router_ConsumesTriggerAtDeepestHoveredNode()
        {
            var router = new UiTreeRouter();
            var root = new GameObject("Root");
            var panel = new GameObject("Panel");
            var button = new GameObject("Button");

            try
            {
                panel.transform.SetParent(root.transform, false);
                button.transform.SetParent(panel.transform, false);

                bool parentConsumed = false;
                bool childConsumed = false;

                router.Register(new XrUiNode(
                    "panel",
                    null,
                    panel,
                    XrUiNodeLayer.Base,
                    true,
                    true,
                    true,
                    new DelegateXrUiEventConsumer((evt, target) =>
                    {
                        parentConsumed = true;
                        return true;
                    })));

                router.Register(new XrUiNode(
                    "button",
                    "panel",
                    button,
                    XrUiNodeLayer.Base,
                    true,
                    true,
                    true,
                    new DelegateXrUiEventConsumer((evt, target) =>
                    {
                        childConsumed = true;
                        return true;
                    })));

                bool consumed = router.TryConsume(
                    button,
                    new XrUiEvent(XrUiEventType.TriggerPressed, XRNode.RightHand));

                Assert.IsTrue(consumed);
                Assert.IsTrue(childConsumed);
                Assert.IsFalse(parentConsumed, "Leaf UI should consume trigger before parent/global handlers.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Router_PopupLayerBeatsDeeperBasePanel()
        {
            var router = new UiTreeRouter();
            var panel = new GameObject("Panel");
            var panelChild = new GameObject("PanelChild");
            var popup = new GameObject("Popup");
            var popupItem = new GameObject("PopupItem");

            try
            {
                panelChild.transform.SetParent(panel.transform, false);
                popup.transform.SetParent(panel.transform, false);
                popupItem.transform.SetParent(popup.transform, false);

                router.Register(new XrUiNode("panel", null, panel, XrUiNodeLayer.Base, true, true, true));
                router.Register(new XrUiNode("panel-child", "panel", panelChild, XrUiNodeLayer.Base, true, true, true));
                router.Register(new XrUiNode("popup", "panel", popup, XrUiNodeLayer.Popup, true, true, true));

                Assert.IsTrue(router.TryGetTopNode(popupItem, out XrUiNode topNode));
                Assert.AreEqual("popup", topNode.Id);
            }
            finally
            {
                Object.DestroyImmediate(panel);
            }
        }

        [Test]
        public void Router_BlocksShortcutsOnlyForRegisteredUi()
        {
            var router = new UiTreeRouter();
            var panel = new GameObject("Panel");
            var button = new GameObject("Button");
            var world = new GameObject("World");

            try
            {
                button.transform.SetParent(panel.transform, false);
                router.Register(new XrUiNode("panel", null, panel, XrUiNodeLayer.Base, true, true, true));

                Assert.IsTrue(router.BlocksShortcuts(button));
                Assert.IsFalse(router.BlocksShortcuts(world));
            }
            finally
            {
                Object.DestroyImmediate(panel);
                Object.DestroyImmediate(world);
            }
        }

        [Test]
        public void Router_ClearRemovesStaleRuntimeDropdownNodes()
        {
            var router = new UiTreeRouter();
            var dropdownList = new GameObject("Dropdown List");

            try
            {
                router.Register(new XrUiNode("audio-list", "audio", dropdownList, XrUiNodeLayer.Popup, true, true, true));

                Assert.IsTrue(router.IsPointerOverUi(dropdownList));

                router.Clear();

                Assert.IsFalse(router.IsPointerOverUi(dropdownList));
            }
            finally
            {
                Object.DestroyImmediate(dropdownList);
            }
        }
    }
}
