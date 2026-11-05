// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Contains the query information for a fallback command.
/// </summary>
public sealed partial class FallbackCommandInvocationArgs : FallbackQueryArgs, IFallbackCommandInvocationArgs
{
    public FallbackCommandInvocationArgs(string query, string queryId, string[]? languageTags = null)
        : base(query, queryId, 0, languageTags)
    {
    }
}
