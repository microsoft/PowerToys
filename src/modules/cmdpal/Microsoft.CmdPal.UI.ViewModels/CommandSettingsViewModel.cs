// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class CommandSettingsViewModel
{
    private readonly ILogger _logger;
    private readonly ExtensionObject<ICommandSettings> _model;
    private readonly CommandProviderWrapper _provider;
    private readonly TaskScheduler _mainThread;

    public CommandSettingsViewModel(
        ICommandSettings? unsafeSettings,
        CommandProviderWrapper provider,
        TaskScheduler mainThread,
        ILogger? logger = null)
    {
        _model = new(unsafeSettings);
        _provider = provider;
        _mainThread = mainThread;
        _logger = logger ?? NullLogger.Instance;
    }

    public ContentPageViewModel? SettingsPage { get; private set; }

    public bool Initialized { get; private set; }

    public bool HasSettings =>
        _model.Unsafe is not null && // We have a settings model AND
        (!Initialized || SettingsPage is not null); // we weren't initialized, OR we were, and we do have a settings page

    private void UnsafeInitializeProperties()
    {
        var model = _model.Unsafe;
        if (model is null)
        {
            return;
        }

        if (model.SettingsPage is not null)
        {
            SettingsPage = new CommandPaletteContentPageViewModel(
                    model.SettingsPage,
                    _mainThread,
                    _provider.ExtensionHost,
                    _provider.GetProviderContext());
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
            Log_FailedToLoadSettingsPage(ex);
        }

        Initialized = true;
    }

    public void DoOnUiThread(Action action)
    {
        Task.Factory.StartNew(
            action,
            CancellationToken.None,
            TaskCreationOptions.None,
            _mainThread);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load settings page")]
    partial void Log_FailedToLoadSettingsPage(Exception ex);
}
