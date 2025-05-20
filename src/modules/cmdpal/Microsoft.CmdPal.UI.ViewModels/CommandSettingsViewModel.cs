﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class CommandSettingsViewModel(ICommandSettings? _unsafeSettings, CommandProviderWrapper provider, TaskScheduler mainThread)
{
    private readonly ExtensionObject<ICommandSettings> _model = new(_unsafeSettings);

    public ContentPageViewModel? SettingsPage { get; private set; }

    public bool Initialized { get; private set; }

    public bool HasSettings =>
        _model.Unsafe != null && // We have a settings model AND
        (!Initialized || SettingsPage != null); // we weren't initialized, OR we were, and we do have a settings page

    private void UnsafeInitializeProperties()
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

    public void SafeInitializeProperties()
    {
        try
        {
            UnsafeInitializeProperties();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load settings page", ex: ex);
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
