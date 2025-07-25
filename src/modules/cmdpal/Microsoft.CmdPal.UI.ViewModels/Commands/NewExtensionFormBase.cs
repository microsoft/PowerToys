// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

internal abstract partial class NewExtensionFormBase : FormContent
{
    public event TypedEventHandler<NewExtensionFormBase, NewExtensionFormBase?>? FormSubmitted;

    protected void RaiseFormSubmit(NewExtensionFormBase? next) => FormSubmitted?.Invoke(this, next);
}
