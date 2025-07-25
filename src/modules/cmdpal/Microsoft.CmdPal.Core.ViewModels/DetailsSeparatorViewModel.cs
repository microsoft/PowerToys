// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class DetailsSeparatorViewModel(
    IDetailsElement _detailsElement,
    WeakReference<IPageContext> context) : DetailsElementViewModel(_detailsElement, context)
{
    public override void InitializeProperties()
    {
        base.InitializeProperties();
    }
}
