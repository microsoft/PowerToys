// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public sealed partial class DetailsSize : IDetailsData, IDetailsSize
{
    public ContentSize Size { get; private set; }

    public DetailsSize(ContentSize size)
    {
        Size = size;
    }
}
