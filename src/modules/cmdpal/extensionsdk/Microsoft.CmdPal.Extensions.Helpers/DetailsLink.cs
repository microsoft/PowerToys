// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public class DetailsLink : IDetailsLink
{
    public Uri Link { get; set; } = new(string.Empty);

    public string Text { get; set; } = string.Empty;
}
