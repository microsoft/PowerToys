// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions;

namespace PowerToys.TryRun.CmdPal;

[Guid("E6E62235-B207-45CC-92A3-ABCB37AD63B1")]
public sealed partial class TryRunExtension(ManualResetEvent disposed) : IExtension, IDisposable
{
    private readonly TryRunCommandsProvider provider = new();

    public object? GetProvider(ProviderType providerType) => providerType == ProviderType.Commands ? provider : null;

    public void Dispose() => disposed.Set();
}
