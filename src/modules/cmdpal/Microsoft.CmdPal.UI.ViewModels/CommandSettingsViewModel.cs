// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class CommandSettingsViewModel(ICommandSettings? _unsafeSettings, CommandProviderWrapper provider, TaskScheduler mainThread)
{
    private readonly ExtensionObject<ICommandSettings> _model = new(_unsafeSettings);
    private readonly Lock _settingsPageCommandLock = new();
    private IContentPage? _settingsPageCommand;

    public IContentPage? SettingsPageCommand
    {
        get
        {
            if (_settingsPageCommand is not null)
            {
                return _settingsPageCommand;
            }

            lock (_settingsPageCommandLock)
            {
                if (_settingsPageCommand is not null)
                {
                    return _settingsPageCommand;
                }

                try
                {
                    var settingsPageCommand = _model.Unsafe?.SettingsPage;
                    if (settingsPageCommand is not null)
                    {
                        _settingsPageCommand = settingsPageCommand;
                    }

                    return settingsPageCommand;
                }
                catch (Exception ex)
                {
                    CoreLogger.LogError("Failed to load settings page command", ex);
                    return null;
                }
            }
        }
    }

    public ContentPageViewModel? SettingsPage { get; private set; }

    public bool Initialized { get; private set; }

    public bool HasSettings =>
        _model.Unsafe is not null && // We have a settings model AND
        (!Initialized || SettingsPage is not null); // we weren't initialized, OR we were, and we do have a settings page

    private void UnsafeInitializeProperties()
    {
        if (SettingsPageCommand is not null)
        {
            SettingsPage = new CommandPaletteContentPageViewModel(SettingsPageCommand, mainThread, provider.ExtensionHost, provider.GetProviderContext());
            SettingsPage.InitializeProperties();
        }
    }

    public void SafeInitializeProperties()
    {
        try
        {
            UnsafeInitializeProperties();
        }
        catch (Exception ex)
        {
            CoreLogger.LogError($"Failed to load settings page", ex: ex);
        }

        Initialized = true;
    }

    public void DoOnUiThread(Action action)
    {
        Task.Factory.StartNew(
            action,
            CancellationToken.None,
            TaskCreationOptions.None,
            mainThread);
    }
}
