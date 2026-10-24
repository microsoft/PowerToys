// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Microsoft.CmdPal.Common.UnitTests.Helpers;

internal static class CoalescingAsyncRunnerTestHelper
{
    internal static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    internal static TaskCompletionSource NewSignal()
    {
        return new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
