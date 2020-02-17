// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/

using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace WindowWalker.Components
{
    /// <summary>
    /// Class for managing shortcuts
    /// Example: When you type "i" we actually search for "internet"
    /// </summary>
    internal class SettingsManager
    {
        /// <summary>
        /// The path to the shortcut file
        /// </summary>
        private static readonly string _shortcutsFile = Path.GetTempPath() + "WindowWalkerShortcuts.ini";

        /// <summary>
        /// Reference to a serializer for saving the settings
        /// </summary>
        private static readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        /// <summary>
        /// An instance of the settings class representing the current settings
        /// </summary>
        private static readonly Settings _settingsInstance = new Settings();

        /// <summary>
        /// Instance of the manager itself
        /// </summary>
        private static SettingsManager _instance;

        /// <summary>
        /// Gets implements Singlton pattern
        /// </summary>
        public static SettingsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SettingsManager();
                }

                return _instance;
            }
        }

        /// <summary>
        /// Initializes static members of the <see cref="SettingsManager"/> class.
        /// Static constructor
        /// </summary>
        /// <remarks>Not sure why we have this AND a singlton pattern</remarks>
        static SettingsManager()
        {
            try
            {
                if (File.Exists(_shortcutsFile))
                {
                    using (StreamReader reader = new StreamReader(_shortcutsFile))
                    {
                        string jsonString = reader.ReadToEnd();
                        _settingsInstance = (Settings)_serializer.Deserialize(jsonString, typeof(Settings));
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsManager"/> class.
        /// Contructor that does nothing?
        /// </summary>
        private SettingsManager()
        {
            return;
        }

        /// <summary>
        /// Adds a shortcut to the settings
        /// </summary>
        /// <param name="before">what the user types</param>
        /// <param name="after">what the resulting search string is going to be</param>
        /// <returns>Returns true if it succeeds, false otherwise</returns>
        /// <remarks>Proably not usefull to actually do the true/false return since
        /// we can now have multiple shortcuts</remarks>
        public bool AddShortcut(string before, string after)
        {
            if (!_settingsInstance.Shortcuts.ContainsKey(before))
            {
                _settingsInstance.Shortcuts.Add(before, new List<string>());
            }

            _settingsInstance.Shortcuts[before].Add(after);

            // Write the updated shortcuts list to a file
            SaveSettings();

            return true;
        }

        /// <summary>
        /// Removes a shortcut
        /// </summary>
        /// <param name="input">the input shortcut string</param>
        /// <returns>true if it succeeds, false otherwise</returns>
        /// <remarks>Probably has a bug since you can now a single input
        /// mapping to multiple outputs</remarks>
        public bool RemoveShortcut(string input)
        {
            if (!_settingsInstance.Shortcuts.ContainsKey(input))
            {
                return false;
            }

            _settingsInstance.Shortcuts.Remove(input);

            // Write the updated shortcuts list to a file
            SaveSettings();

            return true;
        }

        /// <summary>
        /// Retrieves a shortcut and returns all possible mappings
        /// </summary>
        /// <param name="input">the input string for the shortcuts</param>
        /// <returns>A list of all the shortcut strings that result from the user input</returns>
        public List<string> GetShortcut(string input)
        {
            return _settingsInstance.Shortcuts.ContainsKey(input) ? _settingsInstance.Shortcuts[input] : new List<string>();
        }

        /// <summary>
        /// Writes the current shortcuts to the shortcuts file.
        /// Note: We are writing the file even if there are no shortcuts. This handles
        /// the case where the user deletes their last shortcut.
        /// </summary>
        public void SaveSettings()
        {
            using (StreamWriter writer = new StreamWriter(_shortcutsFile, false))
            {
                writer.Write(_serializer.Serialize(_settingsInstance));
            }
        }
    }
}
