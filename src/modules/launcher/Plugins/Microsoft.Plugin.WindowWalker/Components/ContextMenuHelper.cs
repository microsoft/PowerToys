// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Windows.Input;
using Microsoft.Plugin.WindowWalker.Properties;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.Plugin.WindowWalker.Components
{
    public class ContextMenuHelper : IContextMenu
    {
        public static List<ContextMenuResult> LoadContextMenuResults(in Result result)
        {
            if (!(result?.ContextData is Window windowData))
            {
                return new List<ContextMenuResult>(0);
            }

            var contextMenu = new List<ContextMenuResult>()
            {
                new ContextMenuResult
                {
                    AcceleratorKey = Key.F4,
                    AcceleratorModifiers = ModifierKeys.Control,
                    FontFamily = "Segoe MDL2 Assets",
                    Glyph = "\xE8BB",                       // E8B8 => Symbol: ChromeClose
                    Title = $"{Resources.wox_plugin_windowwalker_close} (Ctrl+F4)",
                    Action = _ =>
                    {
                        if (!windowData.IsWindow)
                        {
                            Log.Debug($"Can not close the window {windowData.Hwnd}, because it doesn't exist.", typeof(ContextMenuHelper));
                        }

                        // As a workaround to close PT Run after executing the context menu command, we switch to the window before closing it.
                        // We use the setting OpenAfterKillAndClose to detect if we have to switch.
                        windowData.CloseThisWindow(!WindowWalkerSettings.Instance.OpenAfterKillAndClose);

                        return !WindowWalkerSettings.Instance.OpenAfterKillAndClose;
                    },
                },
            };

            contextMenu.Add(new ContextMenuResult
            {
                AcceleratorKey = Key.Delete,
                AcceleratorModifiers = ModifierKeys.Control,
                FontFamily = "Segoe MDL2 Assets",
                Glyph = "\xE74D",                       // E74D => Symbol: Delete
                Title = $"{Resources.wox_plugin_windowwalker_kill} (Ctrl+Delete)",
                Action = _ =>
                {
                    // ToDo: Code to kill process
                    return false;
                },
            });

            return contextMenu;
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            throw new System.NotImplementedException();
        }

        internal static List<ContextMenuResult> LoadContextMenuResults(Result result, Result selectedResult)
        {
            throw new NotImplementedException();
        }
    }
}
