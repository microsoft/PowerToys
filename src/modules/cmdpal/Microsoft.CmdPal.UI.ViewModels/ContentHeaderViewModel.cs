// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContentHeaderViewModel(IHeaderContent model, WeakReference<IPageContext> context)
    : ObservedContentViewModel<IHeaderContent>(model, context)
{
    public string Title { get; private set; } = string.Empty;

    public string Subtitle { get; private set; } = string.Empty;

    public IconInfoViewModel Image { get; private set; } = new(null);

    public bool HasImage => Image.IsSet;

    protected override void ReadProperties()
    {
        Title = Model.Title ?? string.Empty;
        Subtitle = Model.Subtitle ?? string.Empty;
        Image = new(Model.Image);
        Image.InitializeProperties();
        UpdateProperty(nameof(Title), nameof(Subtitle), nameof(Image), nameof(HasImage));
    }
}
