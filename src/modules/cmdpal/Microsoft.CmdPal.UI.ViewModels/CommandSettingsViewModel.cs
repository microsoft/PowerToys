// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class CommandSettingsViewModel(ICommandSettings _unsafeSettings, CommandProviderWrapper provider, TaskScheduler mainThread)
{
    private readonly ExtensionObject<ICommandSettings> _model = new(_unsafeSettings);

    public ContentPageViewModel? SettingsPage { get; private set; }

    public void InitializeProperties()
    {
        var model = _model.Unsafe;
        if (model == null)
        {
            return;
        }

        if (model.SettingsPage is IContentPage page)
        {
            SettingsPage = new(page, mainThread, provider.ExtensionHost);
            SettingsPage.InitializeProperties();
        }
    }
}
