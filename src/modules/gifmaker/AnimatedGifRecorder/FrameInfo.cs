using System.Drawing;

namespace AnimatedGifRecorder
{
    /// <summary>
    /// Used to store individual frame metadata
    /// </summary>
    public class FrameInfo
    {
        /// <summary>
        /// Frame index
        /// </summary>
        public int Index;

        /// <summary>
        /// Delay to next frame
        /// </summary>
        public int Delay;

        /// <summary>
        /// Path to bitmap
        /// </summary>
        public string Path;

        /// <summary>
        /// Bitmap image
        /// </summary>
        public Image Image;
    }
}
