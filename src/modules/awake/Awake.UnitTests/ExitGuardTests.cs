// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Awake.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.UnitTests;

[SupportedOSPlatform("windows")]
[TestClass]
public class ExitGuardTests
{
    [TestMethod]
    public void TryBeginExit_WhenCalledRepeatedly_OnlyFirstCallSucceeds()
    {
        int exitStarted = 0;

        Assert.IsTrue(Manager.TryBeginExit(ref exitStarted));
        Assert.IsFalse(Manager.TryBeginExit(ref exitStarted));
        Assert.AreEqual(1, exitStarted);
    }

    [TestMethod]
    public void TryBeginExit_WhenCalledConcurrently_OnlyOneCallerSucceeds()
    {
        int exitStarted = 0;
        int successfulCallers = 0;

        Parallel.For(0, 100, _ =>
        {
            if (Manager.TryBeginExit(ref exitStarted))
            {
                Interlocked.Increment(ref successfulCallers);
            }
        });

        Assert.AreEqual(1, successfulCallers);
        Assert.IsFalse(Manager.TryBeginExit(ref exitStarted));
    }
}
