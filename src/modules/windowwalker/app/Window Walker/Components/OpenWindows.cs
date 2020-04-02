// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WindowWalker.Components
{
    /// <summary>
    /// Class that represents the state of the desktops windows
    /// </summary>
    internal class OpenWindows
    {
        /// <summary>
        /// Delegate handler for open windows updates
        /// </summary>
        public delegate void OpenWindowsUpdateHandler(object sender, SearchController.SearchResultUpdateEventArgs e);

        /// <summary>
        /// Event raised when there is an update to the list of open windows
        /// </summary>
        public event OpenWindowsUpdateHandler OnOpenWindowsUpdate;

        /// <summary>
        /// List of all the open windows
        /// </summary>
        private readonly List<Window> windows = new List<Window>();

        /// <summary>
        /// An instance of the class OpenWindows
        /// </summary>
        private static OpenWindows instance;

        /// <summary>
        /// Gets the list of all open windows
        /// </summary>
        public List<Window> Windows
        {
            get { return new List<Window>(windows); }
        }

        /// <summary>
        /// Gets an instance property of this class that makes sure that
        /// the first instance gets created and that all the requests
        /// end up at that one instance
        /// </summary>
        public static OpenWindows Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new OpenWindows();
                }

                return instance;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenWindows"/> class.
        /// Private constructor to make sure there is never
        /// more than one instance of this class
        /// </summary>
        private OpenWindows()
        {
        }

        /// <summary>
        /// Updates the list of open windows
        /// </summary>
        public void UpdateOpenWindowsList()
        {
            windows.Clear();

            new Task(() =>
                {
                    InteropAndHelpers.CallBackPtr callbackptr = new InteropAndHelpers.CallBackPtr(WindowEnumerationCallBack);
                    InteropAndHelpers.EnumWindows(callbackptr, 0);
                }).Start();
        }

        /// <summary>
        /// Call back method for window enumeration
        /// </summary>
        /// <param name="hwnd">The handle to the current window being enumerated</param>
        /// <param name="lParam">Value being passed from the caller (we don't use this but might come in handy
        /// in the future</param>
        /// <returns>true to make sure to contiue enumeration</returns>
        public bool WindowEnumerationCallBack(IntPtr hwnd, IntPtr lParam)
        {
            Window newWindow = new Window(hwnd);

            if (windows.Select(x => x.Title).Contains(newWindow.Title))
            {
                if (newWindow.ProcessName.ToLower().Equals("applicationframehost.exe"))
                {
                    windows.Remove(windows.Where(x => x.Title == newWindow.Title).First());
                }

                if (windows.Select(x => x.Hwnd).Contains(newWindow.Hwnd))
                {
                    return true;
                }
            }

            if ((newWindow.Visible && !newWindow.ProcessName.ToLower().Equals("iexplore.exe")) ||
                (newWindow.ProcessName.ToLower().Equals("iexplore.exe") && newWindow.ClassName == "TabThumbnailWindow"))
            {
                windows.Add(newWindow);

                OnOpenWindowsUpdate?.Invoke(this, new SearchController.SearchResultUpdateEventArgs());
            }

            return true;
        }
    }
}
