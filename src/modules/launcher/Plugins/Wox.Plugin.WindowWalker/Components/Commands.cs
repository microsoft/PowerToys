// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Wox.Plugin.WindowWalker.Components
{
    /// <summary>
    /// A class to handle the commands entered by the user, different
    /// form the user being able to search through their windows
    /// </summary>
    internal class Commands
    {
        /// <summary>
        /// Initializes static members of the <see cref="Commands"/> class.
        /// Constructor primarily used to enforce the creation of tips
        /// and populate the enabled commands list
        /// </summary>
        static Commands()
        {
            _enabledCommands = new Dictionary<string, Command>
            {
                {
                    "quit",
                    new Command()
                    {
                        SearchTexts = new string[]
                        {
                        ":quit",
                        ":q",
                        },
                        Tip = "type \":quit\" to exit",
                    }
                },
                {
                    "launchTerminal",
                    new Command()
                    {
                        SearchTexts = new string[]
                        {
                        ":lterminal",
                        ":lcmd",
                        ":lterm",
                        ":lt",
                        },
                        Tip = "type \":lt\" or \":lcmd\"to launch a new terminal window",
                    }
                },
                {
                    "launchVSCode",
                    new Command()
                    {
                        SearchTexts = new string[]
                        {
                        ":lvscode",
                        ":lcode",
                        },
                        Tip = "type \":lvscode\" or \":lcode\"to launch a new instance of VSCode",
                    }
                },
            };
        }

        /// <summary>
        /// Dictionary containing all the enabled commands
        /// </summary>
        private static readonly Dictionary<string, Command> _enabledCommands;

        /// <summary>
        /// Primary method which executes on the commands that are passed to it
        /// </summary>
        /// <param name="commandText">The search text the user has entered</param>
        public static void ProcessCommand(string commandText)
        {
            LivePreview.DeactivateLivePreview();

            if (_enabledCommands["quit"].SearchTexts.Contains(commandText))
            {
                System.Windows.Application.Current.Shutdown();
            }
            else if (_enabledCommands["launchTerminal"].SearchTexts.Contains(commandText))
            {
                Process.Start(new ProcessStartInfo("cmd.exe")
                { WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) });
            }
            else if (_enabledCommands["launchVSCode"].SearchTexts.Contains(commandText))
            {
                Process.Start("code");
            }
        }

        /// <summary>
        /// Gets the tips for all the enabled commands
        /// </summary>
        public static IEnumerable<string> GetTips()
        {
            return _enabledCommands.Select(x => x.Value.Tip);
        }
    }
}
