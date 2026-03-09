// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class FallbackCommandResult : IFallbackCommandResult
{
    public required string Query { get; set; }

    public required string QueryId { get; set; }

    public IListItem[] Items { get; set; } = [];
}
