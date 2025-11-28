// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

public sealed partial class MediumDetailsViewModel : IDetailsSizeViewModel
{
    private readonly ExtensionObject<IMediumDetails> _model;

    public MediumDetailsViewModel(IMediumDetails smallDetailsLayout)
    {
        _model = new(smallDetailsLayout);
    }

    public void InitializeProperties()
    {
    }
}
