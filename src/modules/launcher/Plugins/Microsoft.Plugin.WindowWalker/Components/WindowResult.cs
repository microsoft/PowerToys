// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Plugin.WindowWalker.Components
{
    internal class WindowResult : Window
    {
        /// <summary>
        /// Number of letters in between constant for when
        /// the result hasn't been set yet
        /// </summary>
        public const int NoResult = -1;

        /// <summary>
        /// Gets or sets properties that signify how many characters (including spaces)
        /// were found when matching the results
        /// </summary>
        public int LettersInBetweenScore { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowResult"/> class.
        /// Constructor for WindowResult
        /// </summary>
        public WindowResult(Window window)
            : base(window.Hwnd)
        {
            LettersInBetweenScore = NoResult;
        }
    }
}
