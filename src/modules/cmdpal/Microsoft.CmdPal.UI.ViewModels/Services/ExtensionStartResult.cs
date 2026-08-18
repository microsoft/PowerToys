// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.Common.Services;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Represents the outcome of attempting to start a single WinRT extension.
/// </summary>
internal sealed class ExtensionStartResult
{
    public IExtensionWrapper Extension { get; }

    public CommandProviderWrapper? Wrapper { get; private init; }

    public Task? PendingStartTask { get; private init; }

    public Stopwatch? Stopwatch { get; private init; }

    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(Wrapper))]
    public bool IsStarted => Wrapper is not null;

    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(PendingStartTask), nameof(Stopwatch))]
    public bool IsTimedOut => PendingStartTask is not null;

    private ExtensionStartResult(IExtensionWrapper extension)
    {
        Extension = extension;
    }

    public static ExtensionStartResult Started(IExtensionWrapper extension, CommandProviderWrapper wrapper)
    {
        return new ExtensionStartResult(extension) { Wrapper = wrapper };
    }

    public static ExtensionStartResult TimedOut(IExtensionWrapper extension, Task pendingStartTask, Stopwatch sw)
    {
        return new ExtensionStartResult(extension) { PendingStartTask = pendingStartTask, Stopwatch = sw };
    }

    public static ExtensionStartResult Failed(IExtensionWrapper extension)
    {
        return new ExtensionStartResult(extension);
    }
}
