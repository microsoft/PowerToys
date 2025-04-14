// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public class ViewModelsFactory
{
    private readonly HotkeyManager _hotkeyManager;
    private readonly AliasManager _aliasManager;
    private readonly SettingsModel _settings;
    private readonly TaskScheduler _taskScheduler;

    public ViewModelsFactory(HotkeyManager hotkeyManager, AliasManager aliasManager, SettingsModel settings, TaskScheduler taskScheduler)
    {
        _hotkeyManager = hotkeyManager;
        _aliasManager = aliasManager;
        _settings = settings;
        _taskScheduler = taskScheduler;
    }

    public TopLevelViewModel CreateTopLevelViewModel(
        CommandItemViewModel command,
        bool fallback,
        CommandPaletteHost host,
        string providerId)
    {
        return new TopLevelViewModel(
            command,
            fallback,
            host,
            providerId,
            _settings,
            _hotkeyManager,
            _aliasManager);
    }

    public CommandProviderWrapper CreateCommandProviderWrapper(ICommandProvider provider)
    {
        return new CommandProviderWrapper(provider, this, _settings, _taskScheduler);
    }

    public CommandProviderWrapper CreateCommandProviderWrapper(IExtensionWrapper extension)
    {
        return new CommandProviderWrapper(extension, this, _settings, _taskScheduler);
    }
}
