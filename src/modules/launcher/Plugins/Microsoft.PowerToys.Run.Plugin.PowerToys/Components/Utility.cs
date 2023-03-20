// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Windows.Input;
using Common.UI;
using interop;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.PowerToys.Components
{
    public class Utility
    {
        public UtilityKey Key { get; }

        public string Name { get; }

        public bool Enabled { get; set; }

        public Utility(UtilityKey key, string name, bool enabled)
        {
            Key = key;
            Name = name;
            Enabled = enabled;
        }

        public Result CreateResult(MatchResult matchResult)
        {
            return new Result
            {
                Title = Name,
                SubTitle = "PowerToys Utility",
                IcoPath = UtilityHelper.GetIcoPath(Key),
                Action = UtilityHelper.GetAction(Key),
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
                    Title = "Run as administrator (Ctrl+Shift+Enter)",
                    Glyph = "\xE7EF",
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = System.Windows.Input.Key.Enter,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ =>
                    {
                        using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowHostsAdminSharedEvent()))
                        {
                            eventHandle.Set();
                        }

                        return true;
                    },
                });
            }

            var settingsWindow = UtilityHelper.GetSettingsWindow(Key);
            if (settingsWindow.HasValue)
            {
                results.Add(new ContextMenuResult
                {
                    Title = "Open settings (Ctrl+Shift+S)",
                    Glyph = "\xE713",
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = System.Windows.Input.Key.S,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ =>
                    {
                        SettingsDeepLink.OpenSettings(settingsWindow.Value);
                        return true;
                    },
                });
            }

            return results;
        }
    }
}
