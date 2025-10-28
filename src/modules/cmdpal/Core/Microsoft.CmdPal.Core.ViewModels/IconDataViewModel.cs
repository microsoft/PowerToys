// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class IconDataViewModel : ObservableObject, IIconData
{
    private readonly ExtensionObject<IIconData> _model = new(null);

    // If the extension previously gave us a Data, then died, the data will
    // throw if we actually try to read it, but the pointer itself won't be
    // null, so this is relatively safe.
    public bool HasIcon => !string.IsNullOrEmpty(Icon) || Data.Unsafe is not null;

    // Locally cached properties from IIconData.
    public string Icon { get; private set; } = string.Empty;

    // Streams are not trivially copy-able, so we can't copy the data locally
    // first. Hence why we're sticking this into an ExtensionObject
    public ExtensionObject<IRandomAccessStreamReference> Data { get; private set; } = new(null);

    IRandomAccessStreamReference? IIconData.Data => Data.Unsafe;

    public string? FontFamily { get; private set; }

    public IconDataViewModel(IIconData? icon)
    {
        _model = new(icon);
    }

    // Unsafe, needs to be called on BG thread
    public void InitializeProperties()
    {
        var model = _model.Unsafe;
        if (model is null)
        {
            return;
        }

        Icon = model.Icon;
        Data = new(model.Data);

        if (model is IExtendedAttributesProvider icon2)
        {
            var props = icon2.GetProperties();

            // From Raymond Chen:
            // Make sure you don't try do do something like
            //    icon2.GetProperties().TryGetValue("awesomeKey", out var awesomeValue);
            //    icon2.GetProperties().TryGetValue("slackerKey", out var slackerValue);
            // because each call to GetProperties() is a cross process hop, and if you
            // marshal-by-value the property set, then you don't want to throw it away and
            // re-marshal it for every property. MAKE SURE YOU CACHE IT.
            if (props?.TryGetValue("FontFamily", out var family) ?? false)
            {
                FontFamily = family as string;
            }
        }
    }
}
