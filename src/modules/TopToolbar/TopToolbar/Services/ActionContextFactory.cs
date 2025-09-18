// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Globalization;
using TopToolbar.Actions;
using TopToolbar.Models;

namespace TopToolbar.Services
{
    public sealed class ActionContextFactory
    {
        public ActionContext CreateForDiscovery(ButtonGroup group)
        {
            var context = CreateBaseContext();

            if (group != null)
            {
                context.EnvironmentVariables["TOPTOOLBAR_GROUP_ID"] = group.Id ?? string.Empty;
                context.EnvironmentVariables["TOPTOOLBAR_GROUP_NAME"] = group.Name ?? string.Empty;
                context.EnvironmentVariables["TOPTOOLBAR_GROUP_FILTER"] = group.Filter ?? string.Empty;
            }

            return context;
        }

        public ActionContext CreateForInvocation(ButtonGroup group, ToolbarButton button)
        {
            var context = CreateForDiscovery(group);

            if (button != null)
            {
                context.EnvironmentVariables["TOPTOOLBAR_BUTTON_ID"] = button.Id ?? string.Empty;
                context.EnvironmentVariables["TOPTOOLBAR_BUTTON_NAME"] = button.Name ?? string.Empty;
                context.EnvironmentVariables["TOPTOOLBAR_BUTTON_DESCRIPTION"] = button.Description ?? string.Empty;
            }

            return context;
        }

        private static ActionContext CreateBaseContext()
        {
            var context = new ActionContext
            {
                Locale = CultureInfo.CurrentUICulture?.Name ?? string.Empty,
                NowUtcIso = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            };

            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                var key = entry.Key?.ToString();
                if (string.IsNullOrEmpty(key) || context.EnvironmentVariables.ContainsKey(key))
                {
                    continue;
                }

                context.EnvironmentVariables[key] = entry.Value?.ToString() ?? string.Empty;
            }

            return context;
        }
    }
}
