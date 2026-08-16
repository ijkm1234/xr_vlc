using System.Collections.Generic;
using UnityEngine;

namespace XRVLC.UI.XR
{
    public sealed class UiTreeRouter
    {
        private readonly Dictionary<string, XrUiNode> _nodesById = new Dictionary<string, XrUiNode>();
        private readonly List<XrUiNode> _matches = new List<XrUiNode>();

        public void Register(XrUiNode node)
        {
            if (node == null || string.IsNullOrEmpty(node.Id) || node.Root == null)
                return;

            _nodesById[node.Id] = node;
        }

        public void Clear()
        {
            _nodesById.Clear();
            _matches.Clear();
        }

        public bool IsPointerOverUi(GameObject target)
        {
            return TryGetTopNode(target, out _);
        }

        public bool BlocksShortcuts(GameObject target)
        {
            return TryGetTopNode(target, out XrUiNode node) && node.BlocksShortcuts;
        }

        public bool BlocksAutoHide(GameObject target)
        {
            return TryGetTopNode(target, out XrUiNode node) && node.BlocksAutoHide;
        }

        public bool TryConsume(GameObject target, XrUiEvent evt)
        {
            CollectMatches(target);
            for (int i = 0; i < _matches.Count; i++)
            {
                XrUiNode node = _matches[i];
                if (node.Consumer != null && node.Consumer.ConsumeXrUiEvent(evt, target))
                    return true;

                if (node.ConsumeTrigger && evt.IsTrigger)
                    return true;
            }

            return false;
        }

        public bool TryGetTopNode(GameObject target, out XrUiNode node)
        {
            CollectMatches(target);
            if (_matches.Count == 0)
            {
                node = null;
                return false;
            }

            node = _matches[0];
            return true;
        }

        private void CollectMatches(GameObject target)
        {
            _matches.Clear();
            if (target == null)
                return;

            foreach (XrUiNode node in _nodesById.Values)
            {
                if (node.Contains(target))
                    _matches.Add(node);
            }

            _matches.Sort((left, right) =>
            {
                int layerCompare = right.Layer.CompareTo(left.Layer);
                if (layerCompare != 0)
                    return layerCompare;

                int depthCompare = right.DepthOf(target).CompareTo(left.DepthOf(target));
                if (depthCompare != 0)
                    return depthCompare;

                return string.CompareOrdinal(left.Id, right.Id);
            });
        }
    }
}
