// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MwbSettings = MouseWithoutBorders.Class.Settings;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class SettingsReloadTests
{
    private static readonly string[] OriginalMatrix = ["first", "second", string.Empty, string.Empty];
    private static readonly string[] ReloadedMatrix = ["second", "first", string.Empty, string.Empty];
    private static readonly string[] TwoMachines = ["first", "second"];

    [TestMethod]
    public void AdoptLoadedProperties_ChangedMatrix_PreservesLoadedAndPreviousValues()
    {
        var previous = new MouseWithoutBordersProperties { MachineMatrixString = new(OriginalMatrix) };
        var loaded = new MouseWithoutBordersProperties { MachineMatrixString = new(ReloadedMatrix) };
        var current = previous;

        Assert.IsTrue(MwbSettings.AdoptLoadedProperties(ref current, loaded));

        Assert.AreSame(loaded, current);
        CollectionAssert.AreEqual(ReloadedMatrix, current.MachineMatrixString);
        CollectionAssert.AreEqual(OriginalMatrix, previous.MachineMatrixString);
    }

    [TestMethod]
    public void AdoptLoadedProperties_SameValues_DoesNotRequestMatrixResend()
    {
        var current = new MouseWithoutBordersProperties { MachineMatrixString = new(TwoMachines) };
        var loaded = new MouseWithoutBordersProperties { MachineMatrixString = new(TwoMachines) };

        Assert.IsFalse(MwbSettings.AdoptLoadedProperties(ref current, loaded));
        Assert.AreSame(loaded, current);
        CollectionAssert.AreEqual(TwoMachines, current.MachineMatrixString);
    }

    [TestMethod]
    public void AdoptLoadedProperties_FirstLoadAndRepeatedLoad_PreserveMatrix()
    {
        MouseWithoutBordersProperties current = null!;
        var loaded = new MouseWithoutBordersProperties { MachineMatrixString = new(TwoMachines) };

        Assert.IsFalse(MwbSettings.AdoptLoadedProperties(ref current, loaded));
        Assert.IsFalse(MwbSettings.AdoptLoadedProperties(ref current, loaded));
        CollectionAssert.AreEqual(TwoMachines, current.MachineMatrixString);
    }
}
