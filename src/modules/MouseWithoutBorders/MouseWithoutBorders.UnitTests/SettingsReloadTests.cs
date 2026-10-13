// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions;
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
    private static readonly string[] UpdatedMatrix = ["third", "fourth", string.Empty, string.Empty];

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SetMachineMatrix_OverlappingAdoption_PreservesCompleteSnapshots(bool pauseAfterFirstValue)
    {
        var previous = new MouseWithoutBordersProperties { MachineMatrixString = new(OriginalMatrix) };
        var previousList = previous.MachineMatrixString;
        var loaded = new MouseWithoutBordersProperties { MachineMatrixString = new(ReloadedMatrix) };
        var loadedList = loaded.MachineMatrixString;
        var settings = CreateSettings(previous);
        using var enumerationPaused = new ManualResetEventSlim();
        using var resumeEnumeration = new ManualResetEventSlim();

        // Exercise the production setter path while its replacement is being built.
        // No watcher, sockets, UI, or real settings-file writes are involved.
        var update = Task.Run(() => settings.SetMachineMatrix(PausingMatrix()));
        try
        {
            Assert.IsTrue(enumerationPaused.Wait(TimeSpan.FromSeconds(10)), "Matrix update did not reach the barrier.");
            Assert.IsTrue(settings.AdoptLoadedProperties(loaded));
            CollectionAssert.AreEqual(OriginalMatrix, previousList);
            CollectionAssert.AreEqual(ReloadedMatrix, loadedList);
        }
        finally
        {
            resumeEnumeration.Set();
            Assert.IsTrue(update.Wait(TimeSpan.FromSeconds(10)), "Matrix update did not finish.");
        }

        CollectionAssert.AreEqual(OriginalMatrix, previousList);
        CollectionAssert.AreEqual(UpdatedMatrix, previous.MachineMatrixString);
        CollectionAssert.AreEqual(ReloadedMatrix, loaded.MachineMatrixString);
        Assert.HasCount(4, loaded.MachineMatrixString);
        Assert.AreEqual(string.Join(",", ReloadedMatrix), settings.MachineMatrixString);

        IEnumerable<string> PausingMatrix()
        {
            for (var i = 0; i < UpdatedMatrix.Length; i++)
            {
                if (i == (pauseAfterFirstValue ? 1 : 0))
                {
                    enumerationPaused.Set();
                    if (!resumeEnumeration.Wait(TimeSpan.FromSeconds(10)))
                    {
                        throw new TimeoutException("Settings adoption did not release the matrix update.");
                    }
                }

                yield return UpdatedMatrix[i];
            }
        }
    }

    [TestMethod]
    public void MachineMatrixString_AdoptionBeforeSetter_ReplacesLoadedListWithoutMutatingItsSnapshot()
    {
        var previous = new MouseWithoutBordersProperties { MachineMatrixString = new(OriginalMatrix) };
        var loaded = new MouseWithoutBordersProperties { MachineMatrixString = new(ReloadedMatrix) };
        var loadedList = loaded.MachineMatrixString;
        var settings = CreateSettings(previous);

        Assert.IsTrue(settings.AdoptLoadedProperties(loaded));
        settings.MachineMatrixString = string.Join(",", UpdatedMatrix);

        CollectionAssert.AreEqual(OriginalMatrix, previous.MachineMatrixString);
        CollectionAssert.AreEqual(ReloadedMatrix, loadedList);
        CollectionAssert.AreEqual(UpdatedMatrix, loaded.MachineMatrixString);
        Assert.HasCount(4, loaded.MachineMatrixString);
        Assert.AreEqual(string.Join(",", UpdatedMatrix), settings.MachineMatrixString);
    }

    [TestMethod]
    public void SetMachineMatrix_EnumerationFails_LeavesCompleteMatrixUnchanged()
    {
        var properties = new MouseWithoutBordersProperties { MachineMatrixString = new(OriginalMatrix) };
        var snapshot = properties.MachineMatrixString;
        var settings = CreateSettings(properties);

        Assert.ThrowsException<InvalidOperationException>(() => settings.SetMachineMatrix(FailingMatrix()));

        Assert.AreSame(snapshot, properties.MachineMatrixString);
        CollectionAssert.AreEqual(OriginalMatrix, properties.MachineMatrixString);
        Assert.AreEqual(string.Join(",", OriginalMatrix), settings.MachineMatrixString);

        static IEnumerable<string> FailingMatrix()
        {
            yield return "replacement";
            throw new InvalidOperationException("Enumeration failed.");
        }
    }

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

    private static MwbSettings CreateSettings(MouseWithoutBordersProperties properties)
    {
        return new MwbSettings(
            new SettingsUtils(new FileSystem()),
            new MouseWithoutBordersSettings { Properties = properties })
        {
            PauseInstantSaving = true,
        };
    }
}
