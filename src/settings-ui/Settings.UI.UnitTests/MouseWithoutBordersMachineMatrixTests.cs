// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Settings.UI.UnitTests;

[TestClass]
public sealed class MouseWithoutBordersMachineMatrixTests
{
    private static readonly string[] HostOnlyMatrix = ["HOST", string.Empty, string.Empty, string.Empty];
    private static readonly string[] HostGuestMatrix = ["HOST", "GUEST", string.Empty, string.Empty];
    private static readonly string[] FullMatrix = ["A", "B", "C", "D"];

    [TestMethod]
    public void EmptySlotsDoNotCauseRepeatedSettingsWrites()
    {
        var matrix = new List<string> { "HOST", string.Empty, string.Empty, string.Empty };
        var available = new List<string> { "HOST" };

        Assert.IsFalse(MouseWithoutBordersMachineMatrix.Reconcile(matrix, available));
        Assert.IsFalse(MouseWithoutBordersMachineMatrix.Reconcile(matrix, available));
        CollectionAssert.AreEqual(HostOnlyMatrix, matrix);
    }

    [TestMethod]
    public void AddingAnAvailableMachineBecomesStableAfterOneUpdate()
    {
        var matrix = new List<string> { string.Empty, string.Empty, string.Empty, string.Empty };
        var available = new List<string> { "HOST" };

        Assert.IsTrue(MouseWithoutBordersMachineMatrix.Reconcile(matrix, available));
        Assert.IsFalse(MouseWithoutBordersMachineMatrix.Reconcile(matrix, available));
        CollectionAssert.AreEqual(HostOnlyMatrix, matrix);
    }

    [TestMethod]
    public void RemovingAnUnavailableMachinePreservesOtherPositions()
    {
        var matrix = new List<string> { "OLD", "GUEST", string.Empty, string.Empty };
        var available = new List<string> { "GUEST", "HOST" };

        Assert.IsTrue(MouseWithoutBordersMachineMatrix.Reconcile(matrix, available));
        CollectionAssert.AreEqual(HostGuestMatrix, matrix);
        Assert.IsFalse(MouseWithoutBordersMachineMatrix.Reconcile(matrix, available));
    }

    [TestMethod]
    public void FullMatrixDoesNotChangeForAnAdditionalAvailableMachine()
    {
        var matrix = new List<string> { "A", "B", "C", "D" };
        var available = new List<string> { "A", "B", "C", "D", "E" };

        Assert.IsFalse(MouseWithoutBordersMachineMatrix.Reconcile(matrix, available));
        CollectionAssert.AreEqual(FullMatrix, matrix);
    }

    [TestMethod]
    public void DuplicateAvailableMachinesAreNotInsertedTwice()
    {
        var matrix = new List<string> { string.Empty, string.Empty, string.Empty, string.Empty };
        var available = new List<string> { "HOST", "HOST" };

        Assert.IsTrue(MouseWithoutBordersMachineMatrix.Reconcile(matrix, available));
        CollectionAssert.AreEqual(HostOnlyMatrix, matrix);
        Assert.IsFalse(MouseWithoutBordersMachineMatrix.Reconcile(matrix, available));
    }
}
