// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class DetailsViewModel(IDetails _details, IPageContext context) : ExtensionObjectViewModel(context)
{
    private readonly ExtensionObject<IDetails> _detailsModel = new(_details);

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public IconInfoViewModel HeroImage { get; private set; } = new(null);

    // TODO: Metadata is an array of IDetailsElement,
    // where IDetailsElement = {IDetailsTags, IDetailsLink, IDetailsSeparator}
    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public override void InitializeProperties()
    {
        var model = _detailsModel.Unsafe;
        if (model == null)
        {
            return;
        }

        Title = model.Title ?? string.Empty;
        Body = model.Body ?? string.Empty;
        HeroImage = new(model.HeroImage);
        HeroImage.InitializeProperties();

        UpdateProperty(nameof(Title));
        UpdateProperty(nameof(Body));
        UpdateProperty(nameof(HeroImage));
    }
}
