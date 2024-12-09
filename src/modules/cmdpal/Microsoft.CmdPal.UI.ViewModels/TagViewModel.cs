// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class TagViewModel(ITag _tag, TaskScheduler Scheduler) : ExtensionObjectViewModel
{
    private readonly ExtensionObject<ITag> _tagModel = new(_tag);

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public string Text { get; private set; } = string.Empty;

    public string Tooltip { get; private set; } = string.Empty;

    public OptionalColor Color { get; private set; }

    // TODO Icon
    public ExtensionObject<ICommand> Command { get; private set; } = new(null);

    public override void InitializeProperties()
    {
        var model = _tagModel.Unsafe;
        if (model == null)
        {
            return;
        }

        Command = new(model.Command);
        Text = model.Text;
        Color = model.Color;
        Tooltip = model.ToolTip;

        UpdateProperty(nameof(Text));
        UpdateProperty(nameof(Color));
        UpdateProperty(nameof(Tooltip));
    }

    protected void UpdateProperty(string propertyName) => Task.Factory.StartNew(() => { OnPropertyChanged(propertyName); }, CancellationToken.None, TaskCreationOptions.None, Scheduler);
}
