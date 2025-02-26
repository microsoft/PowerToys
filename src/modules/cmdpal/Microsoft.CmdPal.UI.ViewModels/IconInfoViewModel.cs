// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class IconInfoViewModel : ObservableObject
{
    private readonly ExtensionObject<IIconInfo> _model = new(null);

    // These are properties that are "observable" from the extension object
    // itself, in the sense that they get raised by PropChanged events from the
    // extension. However, we don't want to actually make them
    // [ObservableProperty]s, because PropChanged comes in off the UI thread,
    // and ObservableProperty is not smart enough to raise the PropertyChanged
    // on the UI thread.
    public IconDataViewModel Light { get; private set; }

    public IconDataViewModel Dark { get; private set; }

    public IconDataViewModel IconForTheme(bool light) => Light = light ? Light : Dark;

    public bool HasIcon(bool light) => IconForTheme(light).HasIcon;

    public bool IsSet => _model.Unsafe != null;

    public IconInfoViewModel(IIconInfo? icon)
    {
        _model = new(icon);
        Light = new(null);
        Dark = new(null);
    }

    // Unsafe, needs to be called on BG thread
    public void InitializeProperties()
    {
        var model = _model.Unsafe;
        if (model == null)
        {
            return;
        }

        Light = new(model.Light);
        Light.InitializeProperties();

        Dark = new(model.Dark);
        Dark.InitializeProperties();
    }
}
