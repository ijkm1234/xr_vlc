using UnityEngine;

namespace XRVLC.UI.XR
{
    public sealed class XrUiNode
    {
        public XrUiNode(
            string id,
            string parentId,
            GameObject root,
            XrUiNodeLayer layer,
            bool blocksShortcuts,
            bool blocksAutoHide,
            bool consumeTrigger,
            IXrUiEventConsumer consumer = null)
        {
            Id = id;
            ParentId = parentId;
            Root = root;
            Layer = layer;
            BlocksShortcuts = blocksShortcuts;
            BlocksAutoHide = blocksAutoHide;
            ConsumeTrigger = consumeTrigger;
            Consumer = consumer;
        }

        public string Id { get; }
        public string ParentId { get; }
        public GameObject Root { get; }
        public XrUiNodeLayer Layer { get; }
        public bool BlocksShortcuts { get; }
        public bool BlocksAutoHide { get; }
        public bool ConsumeTrigger { get; }
        public IXrUiEventConsumer Consumer { get; }

        public bool Contains(GameObject target)
        {
            return target != null &&
                Root != null &&
                (target == Root || target.transform.IsChildOf(Root.transform));
        }

        public int DepthOf(GameObject target)
        {
            if (!Contains(target))
                return -1;

            int depth = 0;
            Transform current = Root.transform;
            while (current != null)
            {
                depth++;
                current = current.parent;
            }

            return depth;
        }
    }
}
