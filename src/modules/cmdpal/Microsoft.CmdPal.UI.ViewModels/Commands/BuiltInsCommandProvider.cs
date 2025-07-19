// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

/// <summary>
/// Built-in Provider for a top-level command which can quit the application. Invokes the <see cref="QuitCommand"/>, which sends a <see cref="QuitMessage"/>.
/// </summary>
public partial class BuiltInsCommandProvider : CommandProvider
{
    private readonly OpenSettingsCommand openSettings = new();
    private readonly QuitCommand quitCommand = new();
    private readonly FallbackReloadItem _fallbackReloadItem = new();
    private readonly FallbackLogItem _fallbackLogItem = new();
    private readonly NewExtensionPage _newExtension = new();

    public override ICommandItem[] TopLevelCommands() =>
        [
            new CommandItem(openSettings) { Subtitle = Properties.Resources.builtin_open_settings_subtitle },
            new CommandItem(_newExtension) { Title = _newExtension.Title, Subtitle = Properties.Resources.builtin_new_extension_subtitle },
            new ListItem(new CommandWithParams() { Name = "Invoke with params 2" })
            {
                Title = "Do a thing with a string",
                Subtitle = "This command requires more input",
                Icon = new IconInfo("\uE961"),
            }
        ];

    public override IFallbackCommandItem[] FallbackCommands() =>
        [
            new FallbackCommandItem(quitCommand, displayTitle: Properties.Resources.builtin_quit_subtitle) { Subtitle = Properties.Resources.builtin_quit_subtitle },
            _fallbackReloadItem,
            _fallbackLogItem,
        ];

    public BuiltInsCommandProvider()
    {
        Id = "Core";
        DisplayName = Properties.Resources.builtin_display_name;
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.scale-200.png");
    }

    public override void InitializeWithHost(IExtensionHost host) => BuiltinsExtensionHost.Instance.Initialize(host);

    internal sealed partial class CommandWithParams : InvokableCommand, IInvokableCommandWithParameters
    {
        public ICommandArgument[] Parameters => [new TextParam("Test")];

        public ICommandResult InvokeWithArgs(object sender, ICommandArgument[] args)
        {
            if (args.Length > 0)
            {
                var arg = args[0];
                var msg = $"Arg {arg.Name} = {arg.Value}";
                var toast = new ToastStatusMessage(new StatusMessage() { Message = msg, State = MessageState.Success });
                toast.Show();
            }
            else
            {
                var toast = new ToastStatusMessage(new StatusMessage() { Message = "didn't work homes", State = MessageState.Error });
                toast.Show();
            }

            return CommandResult.KeepOpen();
        }
    }

    internal sealed partial class TextParam(string name, bool required = true) : BaseObservable, ICommandArgument
    {
        public string Name => name;

        public bool Required => required;

        public ParameterType Type => ParameterType.Text;

        public object? Value { get; set; }

        public void ShowPicker(ulong hostHwnd)
        {
        }
    }
}
