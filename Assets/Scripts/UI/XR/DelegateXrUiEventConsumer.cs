using System;
using UnityEngine;

namespace XRVLC.UI.XR
{
    public sealed class DelegateXrUiEventConsumer : IXrUiEventConsumer
    {
        private readonly Func<XrUiEvent, GameObject, bool> _consume;

        public DelegateXrUiEventConsumer(Func<XrUiEvent, GameObject, bool> consume)
        {
            _consume = consume;
        }

        public bool ConsumeXrUiEvent(XrUiEvent evt, GameObject target)
        {
            return _consume != null && _consume(evt, target);
        }
    }
}
