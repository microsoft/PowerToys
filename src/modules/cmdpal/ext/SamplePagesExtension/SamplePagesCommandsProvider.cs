// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace SamplePagesExtension;

public partial class SamplePagesCommandsProvider : CommandProvider
{
    public SamplePagesCommandsProvider()
    {
        DisplayName = "Sample Pages Commands";
        Icon = new IconInfo("\uE82D");
    }

    private readonly ICommandItem[] _commands = [
        new CommandItem(new SupportCommandsWithProperties()),
       new CommandItem(new SamplesListPage())
       {
           Title = "Sample Pages",
           Subtitle = "View example commands",
       },
    ];

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

    private sealed partial class SupportCommandsWithProperties : ICommand2
    {
        public IPropertySet OtherProperties => null;

        public IIconInfo Icon => null;

        public string Id => string.Empty;

        public string Name => string.Empty;

        public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged
        {
            add { }
            remove { }
        }
    }
}
