// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

public static class CommandProviderContext
{
    public static ICommandProviderContext Empty { get; } = new EmptyCommandProviderContext();

    private sealed class EmptyCommandProviderContext : ICommandProviderContext
    {
        public string ProviderId => "<EMPTY>";

        public bool SupportsPinning => false;
    }
}

public interface ICommandProviderContext
{
    string ProviderId { get; }

    bool SupportsPinning { get; }
}
