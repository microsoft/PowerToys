// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class DetailsSeparatorViewModel(
    IDetailsElement _detailsElement,
    WeakReference<IPageContext> context) : DetailsElementViewModel(_detailsElement, context)
{
    private readonly ExtensionObject<IDetailsSeparator> _dataModel =
        new(_detailsElement.Data as IDetailsSeparator);

    public override void InitializeProperties()
    {
        base.InitializeProperties();
    }
}
