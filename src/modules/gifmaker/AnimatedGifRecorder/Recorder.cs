using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnimatedGifRecorder
{
    /// <summary>
    /// Minimized version of ScreenToGif DirectImageCapture
    /// https://github.com/NickeManarin/ScreenToGif
    /// </summary>
    public class Recorder
    {
        /// <summary>
        /// Collection of frame metadata in the current session
        /// </summary>
        public List<FrameInfo> Frames
        {
            get
            {
                if (frames == null) frames = new List<FrameInfo>();
                return frames;
            }
        }

        /// <summary>
        /// Get current recording session
        /// </summary>
        /// <param name="conf"></param>
        /// <returns></returns>
        public static Recorder GetInstance(RecorderConfiguration conf)
        {
            if (recorder == null) recorder = new Recorder(conf);
            return recorder;
        }

        /// <summary>
        /// Initializes the recording session internally
        /// </summary>
        /// <param name="conf"></param>
        private Recorder(RecorderConfiguration conf)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// To update configurations in the future
        /// </summary>
        /// <param name="conf"></param>
        public void Configure(RecorderConfiguration conf)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Starts recording session
        /// </summary>
        public void Start()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Stops current recording session
        /// </summary>
        /// <returns></returns>
        public void Stop()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Pauses current recording session
        /// </summary>
        public void Pause()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Saves frame into separate file images;
        /// </summary>
        /// <param name="frameInfo"></param>
        private void Save(FrameInfo frame)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Internal method to capture individual frames
        /// </summary>
        /// <returns></returns>
        private int Capture()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Singleton instance of recorder
        /// </summary>
        private static Recorder recorder;

        private List<FrameInfo> frames;
    }
}
