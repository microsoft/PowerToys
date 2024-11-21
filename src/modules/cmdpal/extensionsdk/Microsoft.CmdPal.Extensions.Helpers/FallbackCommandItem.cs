// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public partial class FallbackCommandItem : CommandItem, IFallbackCommandItem
{
    private IFallbackHandler? _fallbackHandler;

    public FallbackCommandItem(ICommand command)
        : base(command)
    {
    }

    public IFallbackHandler? FallbackHandler
    {
        get => _fallbackHandler ?? Command as IFallbackHandler;
        init => _fallbackHandler = value;
    }
}
