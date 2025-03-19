// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

public abstract partial class ContentViewModel(WeakReference<IPageContext> context) :
    ExtensionObjectViewModel(context)
{
}
