using System;

namespace IthacaLib.Common
{
    [Serializable]
    public struct VideoResolution
    {
        public int width;
        public int height;

        public VideoResolution(int width, int height)
        {
            this.width = width;
            this.height = height;
        }

        public static bool operator ==(VideoResolution vr1, VideoResolution vr2)
        {
            return (vr1.width == vr2.width && vr1.height == vr2.height);
        }

        public static bool operator !=(VideoResolution vr1, VideoResolution vr2)
        {
            return !(vr1 == vr2);
        }
    }
}
