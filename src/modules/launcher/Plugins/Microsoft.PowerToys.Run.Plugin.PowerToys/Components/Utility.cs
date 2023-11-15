// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Input;
using Common.UI;
using interop;
using Microsoft.PowerToys.Run.Plugin.PowerToys.Properties;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.PowerToys.Components
{
    public class Utility
    {
        public UtilityKey Key { get; private set; }

        public string Name { get; private set; }

        public bool Enabled { get; private set; }

        public Func<ActionContext, bool> Action { get; private set; }

        public Utility(UtilityKey key, string name, bool enabled, Func<ActionContext, bool> action)
        {
            Key = key;
            Name = name;
            Enabled = enabled;
            Action = action;
        }

        public Result CreateResult(MatchResult matchResult)
        {
            return new Result
            {
                Title = Name,
                SubTitle = Resources.Subtitle_Powertoys_Utility,
                IcoPath = UtilityHelper.GetIcoPath(Key),
                Action = Action,
                ContextData = this,
                Score = matchResult.Score,
                TitleHighlightData = matchResult.MatchData,
            };
        }

        public List<ContextMenuResult> CreateContextMenuResults()
        {
            var results = new List<ContextMenuResult>();

            if (Key == UtilityKey.Hosts)
            {
                results.Add(new ContextMenuResult
                {
                    Title = Resources.Action_Run_As_Administrator,
                    Glyph = "\xE7EF",
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    AcceleratorKey = System.Windows.Input.Key.Enter,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ =>
                    {
                        using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowHostsAdminSharedEvent());
                        eventHandle.Set();
                        return true;
                    },
                });
            }
            else if (Key == UtilityKey.EnvironmentVariables)
            {
                results.Add(new ContextMenuResult
                {
                    Title = Resources.Action_Run_As_Administrator,
                    Glyph = "\xE7EF",
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    AcceleratorKey = System.Windows.Input.Key.Enter,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ =>
                    {
                        using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowEnvironmentVariablesAdminSharedEvent());
                        eventHandle.Set();
                        return true;
                    },
                });
            }

            var settingsWindow = UtilityHelper.GetSettingsWindow(Key);
            if (settingsWindow.HasValue)
            {
                results.Add(new ContextMenuResult
                {
                    Title = Resources.Action_Open_Settings,
                    Glyph = "\xE713",
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    AcceleratorKey = System.Windows.Input.Key.S,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ =>
                    {
                        SettingsDeepLink.OpenSettings(settingsWindow.Value, false);
                        return true;
                    },
                });
            }

            return results;
        }

        public void Enable(bool enabled)
        {
            Enabled = enabled;
        }
    }
}
