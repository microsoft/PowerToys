// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class LinkContent : BaseObservable, ILinkContent
{
    public virtual string Text { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual Uri? Link { get; set => SetProperty(ref field, value); }
}
