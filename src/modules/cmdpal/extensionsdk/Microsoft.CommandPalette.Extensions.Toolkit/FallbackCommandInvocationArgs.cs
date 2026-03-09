// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class FallbackCommandInvocationArgs : IFallbackCommandInvocationArgs
{
    public required string Query { get; init; }

    public required string QueryId { get; init; }

    public FallbackCommandInvocationArgs()
    {
    }

    public FallbackCommandInvocationArgs(string query, string queryId)
    {
        Query = query;
        QueryId = queryId;
    }
}
