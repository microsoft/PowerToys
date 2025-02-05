// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class DetailsElement : IDetailsElement
{
    public string Key { get; set; } = string.Empty;

    public IDetailsData? Data { get; set; }
}
