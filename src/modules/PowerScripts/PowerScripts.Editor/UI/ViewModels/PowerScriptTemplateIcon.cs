// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace PowerScripts.UI.ViewModels;

public sealed class PowerScriptTemplateIcon
{
    public static IReadOnlyList<PowerScriptTemplateIcon> All { get; } = Array.AsReadOnly(new[]
    {
        new PowerScriptTemplateIcon(string.Empty),
        new PowerScriptTemplateIcon("bolt"),
        new PowerScriptTemplateIcon("text"),
        new PowerScriptTemplateIcon("files"),
        new PowerScriptTemplateIcon("image"),
        new PowerScriptTemplateIcon("audio"),
    });

    private PowerScriptTemplateIcon(string id)
    {
        Id = id;
    }

    public string Id { get; }

    public string Name => PowerScriptsViewModel.GetResourceString("PowerScripts_TemplateIcon_" + (HasIcon ? Id : "default"));

    public bool HasIcon => Id.Length > 0;

    public string IconPath => HasIcon ? System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "PowerScripts", "Icons", Id + ".ico") : null;

    public string ImageSource => HasIcon ? new Uri(IconPath).AbsoluteUri : null;

    public string AutomationId => "PowerScriptsTemplateIcon_" + (HasIcon ? Id : "default");
}
