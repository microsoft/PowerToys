// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class DetailsViewModel(IDetails _details, WeakReference<IPageContext> context) : ExtensionObjectViewModel(context)
{
    private readonly ExtensionObject<IDetails> _detailsModel = new(_details);

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public IconInfoViewModel HeroImage { get; private set; } = new(null);

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    // Metadata is an array of IDetailsElement,
    //   where IDetailsElement = {IDetailsTags, IDetailsLink, IDetailsSeparator}
    public List<DetailsElementViewModel> Metadata { get; private set; } = [];

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

        var meta = model.Metadata;
        if (meta != null)
        {
            foreach (var element in meta)
            {
                DetailsElementViewModel? vm = element.Data switch
                {
                    IDetailsSeparator => new DetailsSeparatorViewModel(element, this.PageContext),
                    IDetailsLink => new DetailsLinkViewModel(element, this.PageContext),
                    IDetailsTags => new DetailsTagsViewModel(element, this.PageContext),
                    _ => null,
                };
                if (vm != null)
                {
                    vm.InitializeProperties();
                    Metadata.Add(vm);
                }
            }
        }
    }
}
