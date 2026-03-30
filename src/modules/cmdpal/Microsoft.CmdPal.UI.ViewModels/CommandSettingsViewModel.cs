// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class CommandSettingsViewModel(ICommandSettings? _unsafeSettings, CommandProviderWrapper provider, TaskScheduler mainThread)
{
    private readonly ExtensionObject<ICommandSettings> _model = new(_unsafeSettings);
    private readonly Lock _settingsPageCommandLock = new();
    private SettingsPageMetadata? _settingsPageMetadata;

    public IContentPage? SettingsPageCommand => CachedSettingsPageMetadata?.SettingsPageCommand;

    public SettingsPageMetadata? CachedSettingsPageMetadata
    {
        get
        {
            lock (_settingsPageCommandLock)
            {
                if (_settingsPageMetadata is not null)
                {
                    return _settingsPageMetadata;
                }

                try
                {
                    // RPC: fetch the provider's current SettingsPage snapshot.
                    var settingsPageCommand = _model.Unsafe?.SettingsPage;
                    if (settingsPageCommand is null)
                    {
                        return null;
                    }

                    // Cache a signature snapshot instead of trusting object identity.
                    // Toolkit Settings.SettingsPage currently constructs a fresh page
                    // object on each access, and those pages usually have empty Ids.
                    // We intentionally do not observe later page updates here: this
                    // metadata only supports the top-level menu replacement heuristic.
                    //
                    // Existing behavior is already rebuild-driven here: if an extension
                    // changes its settings page/menu shape at runtime, the host menu
                    // will not reflect that until the command/menu gets rebuilt.
                    //
                    // RPC: snapshot Id/Name/type once so later menu matching can stay local.
                    _settingsPageMetadata = new(
                        settingsPageCommand,
                        settingsPageCommand.GetType(),
                        settingsPageCommand.Id ?? string.Empty,
                        settingsPageCommand.Name ?? string.Empty);

                    return _settingsPageMetadata;
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

    public bool HasSettings
    {
        get
        {
            lock (_settingsPageCommandLock)
            {
                return Initialized ? SettingsPage is not null : _settingsPageMetadata is not null || SettingsPage is not null;
            }
        }
    }

    public bool HasOrMayHaveSettings => HasSettings || (!Initialized && _model.Unsafe is not null);

    private void UnsafeInitializeProperties()
    {
        var settingsPageCommand = SettingsPageCommand;
        if (settingsPageCommand is not null)
        {
            SettingsPage = new CommandPaletteContentPageViewModel(settingsPageCommand, mainThread, provider.ExtensionHost, provider.GetProviderContext());
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
            InvalidateSettingsPage();
            CoreLogger.LogError("Failed to load settings page", ex);
        }
        finally
        {
            Initialized = true;
        }
    }

    public void DoOnUiThread(Action action)
    {
        Task.Factory.StartNew(
            action,
            CancellationToken.None,
            TaskCreationOptions.None,
            mainThread);
    }

    private void InvalidateSettingsPage()
    {
        lock (_settingsPageCommandLock)
        {
            _settingsPageMetadata = null;
            SettingsPage = null;
            Initialized = false;
        }
    }

    internal void Cleanup()
    {
        lock (_settingsPageCommandLock)
        {
            _settingsPageMetadata = null;
        }
    }
}
