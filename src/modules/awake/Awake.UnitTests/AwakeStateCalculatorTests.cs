// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Awake.Core;
using Awake.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.UnitTests;

[TestClass]
public class AwakeStateCalculatorTests
{
    [TestMethod]
    public void ComputeAwakeState_WhenUnlockedAndDisplayRequested_IncludesDisplayRequiredFlag()
    {
        ExecutionState state = AwakeStateCalculator.ComputeAwakeState(keepDisplayOn: true, isScreenLocked: false);

        Assert.IsTrue(state.HasFlag(ExecutionState.ES_SYSTEM_REQUIRED));
        Assert.IsTrue(state.HasFlag(ExecutionState.ES_DISPLAY_REQUIRED));
        Assert.IsTrue(state.HasFlag(ExecutionState.ES_CONTINUOUS));
    }

    [TestMethod]
    public void ComputeAwakeState_WhenLockedAndDisplayRequested_DoesNotIncludeDisplayRequiredFlag()
    {
        ExecutionState state = AwakeStateCalculator.ComputeAwakeState(keepDisplayOn: true, isScreenLocked: true);

        Assert.IsTrue(state.HasFlag(ExecutionState.ES_SYSTEM_REQUIRED));
        Assert.IsFalse(state.HasFlag(ExecutionState.ES_DISPLAY_REQUIRED));
        Assert.IsTrue(state.HasFlag(ExecutionState.ES_CONTINUOUS));
    }

    [TestMethod]
    public void ComputeAwakeState_WhenDisplayOff_DoesNotIncludeDisplayRequiredFlag()
    {
        ExecutionState state = AwakeStateCalculator.ComputeAwakeState(keepDisplayOn: false, isScreenLocked: false);

        Assert.IsTrue(state.HasFlag(ExecutionState.ES_SYSTEM_REQUIRED));
        Assert.IsFalse(state.HasFlag(ExecutionState.ES_DISPLAY_REQUIRED));
        Assert.IsTrue(state.HasFlag(ExecutionState.ES_CONTINUOUS));
    }
}
