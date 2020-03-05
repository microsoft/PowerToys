using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowWalker.Components
{
    class WindowResult:Window
    {
        /// <summary>
        /// Number of letters in between constant for when
        /// the result hasn't been set yet
        /// </summary>
        public static const int NoResult = -1; 

        /// <summary>
        /// Properties that signify how many characters (including spaces)
        /// were found when matching the results
        /// </summary>
        public int LettersInBetweenScore
        {
            get;
            set;
        }

        /// <summary>
        /// Constructor for WindowResult
        /// </summary>
        public WindowResult(Window window):base(window.Hwnd)
        {
            LettersInBetweenScore = WindowResult.NoResult;
        }
    }
}
