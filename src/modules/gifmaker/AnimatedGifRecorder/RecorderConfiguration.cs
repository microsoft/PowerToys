namespace AnimatedGifRecorder
{
    /// <summary>
    /// Summarizes capture session configurations in a single object
    /// </summary>
    public class RecorderConfiguration
    {
        /// <summary>
        /// Width of recording
        /// </summary>
        public int Width;

        /// <summary>
        /// Height of recording
        /// </summary>
        public int Height;

        /// <summary>
        /// Left most pixel of recording region
        /// </summary>
        public int X;

        /// <summary>
        /// Top most pixel of recording region
        /// </summary>
        public int Y;

        /// <summary>
        /// Determines the time interval between frames (1000ms / framerate)
        /// </summary>
        public double FrameRate;
    }
}
