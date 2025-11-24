// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CmdPal.Core.ViewModels;
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

    private readonly IRootPageService _rootPageService;

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

    public BuiltInsCommandProvider(IRootPageService rootPageService)
    {
        Id = "com.microsoft.cmdpal.builtin.core";
        DisplayName = Properties.Resources.builtin_display_name;
        Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.altform-unplated_targetsize-256.png");

        _rootPageService = rootPageService;
    }

    public IDictionary<string, object> GetProperties()
    {
        var rootPage = _rootPageService.GetRootPage();
        List<ICommandItem> bandItems = new();
        bandItems.Add(new WrappedDockItem(rootPage, Properties.Resources.builtin_command_palette_title));

        return new PropertySet()
        {
            { "DockBands", bandItems.ToArray() },
        };
    }

    public override void InitializeWithHost(IExtensionHost host) => BuiltinsExtensionHost.Instance.Initialize(host);
}
