// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

public abstract partial class ContentViewModel : ExtensionObjectViewModel
{
    protected ContentViewModel(WeakReference<IPageContext> context)
        : this(context, null)
    {
    }

    internal ContentViewModel(
        WeakReference<IPageContext> context,
        FallbackQueryContext? fallbackContext)
        : base(context)
    {
        FallbackContext = fallbackContext;
    }

    internal FallbackQueryContext? FallbackContext { get; }

    public bool OnlyControlOnPage { get; internal set; }
}
