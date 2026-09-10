// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.Data;

namespace WorkspacesLauncherUI.UnitTests
{
    /// <summary>
    /// Tests for the LaunchingState enum values and their integer mapping.
    /// The C++ launcher engine sends state as integer values over IPC.
    /// These integer values MUST remain stable across the migration.
    /// </summary>
    [TestClass]
    public class LaunchStateEnumContractTests
    {
        [TestMethod]
        [TestCategory("DataModel")]
        public void EnumContract_WaitingState_MapsToIntegerZero()
        {
            Assert.AreEqual(0, (int)LaunchingState.Waiting);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void EnumContract_LaunchedState_MapsToIntegerOne()
        {
            Assert.AreEqual(1, (int)LaunchingState.Launched);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void EnumContract_LaunchedAndMovedState_MapsToIntegerTwo()
        {
            Assert.AreEqual(2, (int)LaunchingState.LaunchedAndMoved);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void EnumContract_FailedState_MapsToIntegerThree()
        {
            Assert.AreEqual(3, (int)LaunchingState.Failed);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void EnumContract_CanceledState_MapsToIntegerFour()
        {
            Assert.AreEqual(4, (int)LaunchingState.Canceled);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void EnumContract_TotalMemberCount_IsExactlyFiveMatchingCppHeader()
        {
            var values = Enum.GetValues(typeof(LaunchingState));
            Assert.AreEqual(5, values.Length, "LaunchingState must have exactly 5 values to match C++ LaunchingStateEnum.h");
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void EnumContract_IntToEnumCast_RoundTripsForAllValues()
        {
            for (int i = 0; i <= 4; i++)
            {
                var state = (LaunchingState)i;
                Assert.AreEqual(i, (int)state);
            }
        }
    }
}
