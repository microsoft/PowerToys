// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.ViewModels;

public sealed class CommandProviderContext
{
    public required string ProviderId { get; init; }

    public static CommandProviderContext Empty { get; } = new() { ProviderId = "<EMPTY>" };
}
