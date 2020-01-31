// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/

using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace WindowWalker.Components
{
    /// <summary>
    /// Class that represents all the settings and
    /// can be serialized into JSON for easy saving
    /// </summary>
    internal class Settings
    {
        /// <summary>
        /// Gets or sets the version of the settings file
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets a list of all the shortcuts
        /// </summary>
        public Dictionary<string, List<string>> Shortcuts { get; set; }

        /// <summary>
        /// Gets or sets a list of saved window locations catagorized by number of screens
        /// </summary>
        public Dictionary<string, Point> WindowLocations { get; set; }

        /// <summary>
        /// Gets or sets the location of the search windows  (the top left point)
        /// </summary>
        [ScriptIgnore]
        public Point WindowLocation
        {
            get
            {
                if (WindowLocations.ContainsKey(System.Windows.Forms.Screen.AllScreens.Length.ToString()))
                {
                    return WindowLocations[System.Windows.Forms.Screen.AllScreens.Length.ToString()];
                }
                else
                {
                    return new Point() { X = 0, Y = 0 };
                }
            }

            set
            {
                if (WindowLocations == null)
                {
                    WindowLocations = new Dictionary<string, Point>();
                }

                WindowLocations[System.Windows.Forms.Screen.AllScreens.Length.ToString()] = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Settings"/> class.
        /// Constructer to initialize some default values
        /// </summary>
        public Settings()
        {
            Version = string.Empty;
            Shortcuts = new Dictionary<string, List<string>>();
            WindowLocation = new Point() { X = 0, Y = 0 };
            WindowLocations = new Dictionary<string, Point>();
        }
    }
}
