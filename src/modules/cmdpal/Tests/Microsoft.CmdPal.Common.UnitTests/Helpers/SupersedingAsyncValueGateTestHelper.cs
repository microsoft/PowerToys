// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Helpers;

namespace Microsoft.CmdPal.Common.UnitTests.Helpers;

internal static class SupersedingAsyncValueGateTestHelper
{
    internal static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    internal static TaskCompletionSource NewSignal()
    {
        return new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    internal static SupersedingAsyncValueGate<int> CreateGate(Func<CancellationToken, Task> action)
    {
        return new SupersedingAsyncValueGate<int>(token => WithResultAsync(action(token)), _ => { });
    }

    private static async Task<int> WithResultAsync(Task action)
    {
        await action.ConfigureAwait(false);
        return 42;
    }
}
