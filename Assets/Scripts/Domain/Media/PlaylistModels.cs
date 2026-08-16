using System;
using System.Collections.Generic;

namespace XRVLC.Media
{
    [Serializable]
    public class PlaylistItemData
    {
        public int index;
        public string title;
        public long length;
        public string uri;
        public bool isCurrent;
    }

    [Serializable]
    public class PlaylistJsonWrapper
    {
        public List<PlaylistItemData> items;
    }
}
