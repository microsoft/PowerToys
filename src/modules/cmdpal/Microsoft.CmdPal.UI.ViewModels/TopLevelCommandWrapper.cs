// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class TopLevelCommandWrapper : ICommand
{
    private readonly ExtensionObject<ICommand> _command;

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged;

    public string Name { get; private set; } = string.Empty;

    public string Id { get; private set; } = string.Empty;

    public IIconInfo Icon { get; private set; } = new IconInfo(null);

    public ICommand Command => _command.Unsafe!;

    public CommandPaletteHost ExtensionHost { get; }

    public TopLevelCommandWrapper(ICommand command, CommandPaletteHost extensionHost)
    {
        _command = new(command);
        ExtensionHost = extensionHost;
    }

    public void UnsafeInitializeProperties()
    {
        var model = _command.Unsafe!;

        Name = model.Name;
        Id = model.Id;
        Icon = model.Icon;

        model.PropChanged += Model_PropChanged;
        model.PropChanged += this.PropChanged;
    }

    private void Model_PropChanged(object sender, IPropChangedEventArgs args)
    {
        try
        {
            var propertyName = args.PropertyName;
            var model = _command.Unsafe;
            if (model == null)
            {
                return; // throw?
            }

            switch (propertyName)
            {
                case nameof(Name):
                    this.Name = model.Name;
                    break;
                case nameof(Icon):
                    var listIcon = model.Icon;
                    Icon = model.Icon;
                    break;
            }

            PropChanged?.Invoke(this, args);
        }
        catch
        {
        }
    }
}
