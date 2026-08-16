using System;

namespace XRVLC.Media
{
    [Serializable]
    public class TrackInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsSelected { get; set; }
        public SlaveDTO Slave { get; set; }
    }
}
