// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class DetailsSeparatorViewModel(
    IDetailsElement _detailsElement,
    IPageContext context) : DetailsElementViewModel(_detailsElement, context)
{
    private readonly ExtensionObject<IDetailsSeparator> _dataModel =
        new(_detailsElement.Data as IDetailsSeparator);

    public override void InitializeProperties()
    {
        base.InitializeProperties();
    }
}
