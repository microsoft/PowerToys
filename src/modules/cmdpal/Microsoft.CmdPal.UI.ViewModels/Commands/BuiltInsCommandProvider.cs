// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation.Collections;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

/// <summary>
/// Built-in Provider for a top-level command which can quit the application. Invokes the <see cref="QuitCommand"/>, which sends a <see cref="QuitMessage"/>.
/// </summary>
public sealed partial class BuiltInsCommandProvider : CommandProvider, IExtendedAttributesProvider
{
    private readonly OpenSettingsCommand openSettings = new();
    private readonly QuitCommand quitCommand = new();
    private readonly FallbackReloadItem _fallbackReloadItem = new();
    private readonly FallbackLogItem _fallbackLogItem = new();
    private readonly NewExtensionPage _newExtension = new();

    private readonly AppStateModel _appState;
    private readonly TopLevelCommandManager _topLevelCommandManager;

    public override ICommandItem[] TopLevelCommands() =>
        [
            new CommandItem(openSettings) { },
            new CommandItem(_newExtension) { Title = _newExtension.Title, Subtitle = Properties.Resources.builtin_new_extension_subtitle },
        ];

    public override IFallbackCommandItem[] FallbackCommands() =>
        [
            new FallbackCommandItem(quitCommand, displayTitle: Properties.Resources.builtin_quit_subtitle) { Subtitle = Properties.Resources.builtin_quit_subtitle },
            _fallbackReloadItem,
            _fallbackLogItem,
        ];

    public BuiltInsCommandProvider(AppStateModel appState, TopLevelCommandManager tlcManager)
    {
        Id = "com.microsoft.cmdpal.builtin.core";
        DisplayName = Properties.Resources.builtin_display_name;
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.scale-200.png");

        _appState = appState;
        _topLevelCommandManager = tlcManager;
        _appState.StateChanged += AppState_StateChanged;
    }

    public IDictionary<string, object> GetProperties()
    {
        var bands = _appState.TopLevelCommandBands;
        var bandItems = new List<ICommandItem>();
        foreach (var band in bands)
        {
            var item = _topLevelCommandManager.LookupCommand(band.Id);
            if (item != null)
            {
                bandItems.Add(item.ToDockBandItem(showLabels: band.ShowLabels));
            }
        }

        return new PropertySet()
        {
            { "DockBands", bandItems.ToArray() },
        };
    }

    private void AppState_StateChanged(AppStateModel state, object? _)
    {
        // TODO! Be more precise - don't blast our bands just when any state changes
        RaiseItemsChanged();
    }

    public override void InitializeWithHost(IExtensionHost host) => BuiltinsExtensionHost.Instance.Initialize(host);
}
