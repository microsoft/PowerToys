// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.Versioning;
using Awake.Core;
using Awake.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.UnitTests;

[SupportedOSPlatform("windows")]
[TestClass]
[DoNotParallelize]
public class ExpirableKeepAwakeTests
{
    [DataRow(false)]
    [DataRow(true)]
    [DataTestMethod]
    public void SetExpirableKeepAwake_WhenDeadlineExpiresAfterNormalization_ReturnsFalse(bool keepDisplayOn)
    {
        DateTimeOffset deadline = DateTimeOffset.Now.AddMinutes(-1);
        Assert.IsTrue(CommandLineArgumentNormalizer.TryNormalize(
            ["until", deadline.ToString("O", CultureInfo.InvariantCulture)],
            deadline.AddMinutes(-1),
            CultureInfo.InvariantCulture,
            out _,
            out _,
            out _));

        FieldInfo timerField = typeof(Manager).GetField("_timerSubscription", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.IsNull(timerField.GetValue(null), "The test must not dispose an existing session timer.");

        var queue = (BlockingCollection<ExecutionState>)typeof(Manager)
            .GetField("_stateQueue", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        ExecutionState[] previousStates = queue.ToArray();
        bool wasUsingPowerToysConfig = Manager.IsUsingPowerToysConfig;

        try
        {
            while (queue.TryTake(out _))
            {
            }

            Manager.IsUsingPowerToysConfig = false;

            Assert.IsFalse(Manager.SetExpirableKeepAwake(deadline, keepDisplayOn));
            Assert.IsNull(timerField.GetValue(null));
            CollectionAssert.AreEqual(new[] { ExecutionState.ES_CONTINUOUS }, queue.ToArray());
        }
        finally
        {
            Manager.IsUsingPowerToysConfig = wasUsingPowerToysConfig;
            while (queue.TryTake(out _))
            {
            }

            foreach (ExecutionState state in previousStates)
            {
                queue.Add(state);
            }
        }
    }
}
