// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public class ListSection : ISection
{
    public string Title { get; set; } = "";
    public virtual IListItem[] Items { get; set; } = [];
}
