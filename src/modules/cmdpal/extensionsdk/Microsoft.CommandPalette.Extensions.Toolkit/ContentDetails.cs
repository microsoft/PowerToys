// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Details whose presentation is supplied entirely by content. Legacy fields may
/// be populated for older hosts, but are not rendered alongside Content by new hosts.
/// </summary>
public partial class ContentDetails : Details, IDetails2
{
    public virtual IContent[] Content { get; set => SetProperty(ref field, value); } = [];

    public virtual IContent[] GetContent() => Content;
}
