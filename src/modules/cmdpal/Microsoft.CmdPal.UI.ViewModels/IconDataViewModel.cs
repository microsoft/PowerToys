// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class IconDataViewModel : ObservableObject
{
    private readonly ExtensionObject<IIconData> _model = new(null);

    // If the extension previously gave us a Data, then died, the data will
    // throw if we actually try to read it, but the pointer itself won't be
    // null, so this is relatively safe.
    public bool HasIcon => !string.IsNullOrEmpty(Icon) || Data.Unsafe != null;

    // Locally cached properties from IIconData.
    public string Icon { get; private set; } = string.Empty;

    // Streams are not trivially copy-able, so we can't copy the data locally
    // first. Hence why we're sticking this into an ExtensionObject
    public ExtensionObject<IRandomAccessStreamReference> Data { get; private set; } = new(null);

    public IconDataViewModel(IIconData? icon)
    {
        _model = new(icon);
    }

    // Unsafe, needs to be called on BG thread
    public void InitializeProperties()
    {
        var model = _model.Unsafe;
        if (model == null)
        {
            return;
        }

        Icon = model.Icon;
        Data = new(model.Data);
    }
}
