// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Class;
using MouseWithoutBorders.Core;
using MwbSettings = MouseWithoutBorders.Class.Settings;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed class SettingsReloadTests
{
    private static readonly string[] OriginalMatrix = ["first", "second", string.Empty, string.Empty];
    private static readonly string[] ReloadedMatrix = ["second", "first", string.Empty, string.Empty];
    private static readonly string[] TwoMachines = ["first", "second"];
    private static readonly string[] UpdatedMatrix = ["third", "fourth", string.Empty, string.Empty];
    private static readonly string[] MatrixReloadEffects = ["cursor", "invalidate", "send", "save"];

    private string _previousKey = null!;

    [TestInitialize]
    public void InitializeRuntimeKey()
    {
        _previousKey = Encryption.MyKey;
        Encryption.MyKey = "RELOAD-TEST-KEY";
    }

    [TestCleanup]
    public void RestoreRuntimeKey()
    {
        Encryption.MyKey = _previousKey;
    }

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
            Assert.IsTrue(ApplySettings(settings, loaded));
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

        Assert.IsTrue(ApplySettings(settings, loaded));
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
    public void ApplyLoadedSettings_ChangedMatrix_PreservesLoadedAndPreviousValues()
    {
        var previous = new MouseWithoutBordersProperties { MachineMatrixString = new(OriginalMatrix) };
        var loaded = new MouseWithoutBordersProperties { MachineMatrixString = new(ReloadedMatrix) };
        var settings = CreateSettings(previous);

        Assert.IsTrue(ApplySettings(settings, loaded));

        Assert.AreEqual(string.Join(",", ReloadedMatrix), settings.MachineMatrixString);
        CollectionAssert.AreEqual(ReloadedMatrix, loaded.MachineMatrixString);
        CollectionAssert.AreEqual(OriginalMatrix, previous.MachineMatrixString);
    }

    [TestMethod]
    public void ApplyLoadedSettings_SameValues_DoesNotRequestMatrixResend()
    {
        var current = new MouseWithoutBordersProperties { MachineMatrixString = new(TwoMachines) };
        var loaded = new MouseWithoutBordersProperties { MachineMatrixString = new(TwoMachines) };
        var settings = CreateSettings(current);

        Assert.IsFalse(ApplySettings(settings, loaded));
        Assert.AreEqual(string.Join(",", TwoMachines), settings.MachineMatrixString);
        CollectionAssert.AreEqual(TwoMachines, loaded.MachineMatrixString);
    }

    [TestMethod]
    public void ApplyLoadedSettings_FirstLoadAndRepeatedLoad_PreserveMatrix()
    {
        var settings = CreateSettings(null!);
        var loaded = new MouseWithoutBordersProperties { MachineMatrixString = new(TwoMachines) };

        Assert.IsFalse(ApplySettings(settings, loaded));
        Assert.IsFalse(ApplySettings(settings, loaded));
        CollectionAssert.AreEqual(TwoMachines, loaded.MachineMatrixString);
        Assert.AreEqual(string.Join(",", TwoMachines), settings.MachineMatrixString);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ApplyLoadedSettings_CursorWaitOverlapsWriter_ComparesAfterWriterCompletes(bool pauseAfterFirstValue)
    {
        var previous = new MouseWithoutBordersProperties { MachineMatrixString = new(OriginalMatrix), DrawMouseCursor = true };
        var loaded = new MouseWithoutBordersProperties { MachineMatrixString = new(OriginalMatrix), DrawMouseCursor = false };
        loaded.SecurityKey.Value = Encryption.MyKey;
        var settings = CreateSettings(previous);
        using var enumerationPaused = new ManualResetEventSlim();
        using var resumeEnumeration = new ManualResetEventSlim();
        using var cursorEntered = new ManualResetEventSlim();
        var effects = new List<string>();
        string[]? invalidatedMatrix = null;
        string? sentMatrix = null;
        string? savedMatrix = null;
        Task? reload = null;

        // SetMachineMatrix holds the real settings lock and captures the old properties
        // before enumerating. Model the runtime cache already containing the writer's M1.
        var runtimeMatrix = UpdatedMatrix;
        var update = Task.Run(() => settings.SetMachineMatrix(PausingMatrix()));
        try
        {
            Assert.IsTrue(enumerationPaused.Wait(TimeSpan.FromSeconds(10)), "Writer did not capture the old properties.");
            reload = Task.Run(() => settings.ApplyLoadedSettings(
                new MouseWithoutBordersSettings { Properties = loaded },
                () =>
                {
                    cursorEntered.Set();

                    // This is the same synchronized read performed by ShowFakeMouseCursor,
                    // without creating UI or using the global Setting.Values instance.
                    Assert.IsFalse(settings.DrawMouse);
                    effects.Add("cursor");
                },
                () =>
                {
                    invalidatedMatrix = runtimeMatrix;
                    runtimeMatrix = settings.MachineMatrixString.Split(',');
                    effects.Add("invalidate");
                },
                () =>
                {
                    sentMatrix = settings.MachineMatrixString;
                    effects.Add("send");
                },
                () =>
                {
                    savedMatrix = settings.MachineMatrixString;
                    effects.Add("save");
                }));

            Assert.IsTrue(cursorEntered.Wait(TimeSpan.FromSeconds(10)), "Reload did not adopt properties and enter the cursor path.");
            CollectionAssert.AreEqual(OriginalMatrix, previous.MachineMatrixString);
            CollectionAssert.AreEqual(OriginalMatrix, loaded.MachineMatrixString);
        }
        finally
        {
            resumeEnumeration.Set();
            Assert.IsTrue(Task.WhenAll(update, reload ?? Task.CompletedTask).Wait(TimeSpan.FromSeconds(10)), "Writer/reload did not finish after releasing the settings lock.");
        }

        CollectionAssert.AreEqual(UpdatedMatrix, previous.MachineMatrixString);
        CollectionAssert.AreEqual(OriginalMatrix, loaded.MachineMatrixString);
        Assert.IsNotNull(invalidatedMatrix, "Reload must invalidate the writer's now-stale runtime cache.");
        CollectionAssert.AreEqual(UpdatedMatrix, invalidatedMatrix);
        CollectionAssert.AreEqual(OriginalMatrix, runtimeMatrix);
        Assert.AreEqual(string.Join(",", OriginalMatrix), sentMatrix);
        Assert.AreEqual(string.Join(",", OriginalMatrix), savedMatrix);
        CollectionAssert.AreEqual(MatrixReloadEffects, effects);

        IEnumerable<string> PausingMatrix()
        {
            for (var i = 0; i < UpdatedMatrix.Length; i++)
            {
                if (i == (pauseAfterFirstValue ? 1 : 0))
                {
                    enumerationPaused.Set();
                    if (!resumeEnumeration.Wait(TimeSpan.FromSeconds(10)))
                    {
                        throw new TimeoutException("Reload did not release the matrix writer.");
                    }
                }

                yield return UpdatedMatrix[i];
            }
        }
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, true)]
    [DataRow(true, true, false)]
    [DataRow(false, true, true)]
    [DataRow(true, false, true)]
    [DataRow(true, true, true)]
    public void ApplyLoadedSettings_RequestsOnlyRequiredEffects(bool hideCursor, bool changeMatrix, bool changeWrapMouse)
    {
        var previous = new MouseWithoutBordersProperties { MachineMatrixString = new(OriginalMatrix), DrawMouseCursor = true };
        var loaded = new MouseWithoutBordersProperties
        {
            MachineMatrixString = new(changeMatrix ? ReloadedMatrix : OriginalMatrix),
            DrawMouseCursor = !hideCursor,
            WrapMouse = changeWrapMouse ? !previous.WrapMouse : previous.WrapMouse,
        };
        loaded.SecurityKey.Value = Encryption.MyKey;
        var settings = CreateSettings(previous);
        var effects = new List<string>();

        settings.ApplyLoadedSettings(
            new MouseWithoutBordersSettings { Properties = loaded },
            () =>
            {
                Assert.IsFalse(settings.DrawMouse);
                effects.Add("cursor");
            },
            () => effects.Add("invalidate"),
            () => effects.Add("send"),
            () => effects.Add("save"));

        var expected = new List<string>();
        if (hideCursor)
        {
            expected.Add("cursor");
        }

        if (changeMatrix)
        {
            expected.Add("invalidate");
        }

        if (changeMatrix || changeWrapMouse)
        {
            expected.Add("send");
            expected.Add("save");
        }

        CollectionAssert.AreEqual(expected, effects);
        CollectionAssert.AreEqual(OriginalMatrix, previous.MachineMatrixString);
        Assert.AreEqual(string.Join(",", loaded.MachineMatrixString), settings.MachineMatrixString);
    }

    private static bool ApplySettings(MwbSettings settings, MouseWithoutBordersProperties loaded)
    {
        loaded.SecurityKey.Value = Encryption.MyKey;
        var invalidated = false;
        settings.ApplyLoadedSettings(
            new MouseWithoutBordersSettings { Properties = loaded },
            () => { },
            () => invalidated = true,
            () => { },
            () => { });
        return invalidated;
    }

    private static MwbSettings CreateSettings(MouseWithoutBordersProperties properties)
    {
        return new MwbSettings(
            new SettingsUtils(new FileSystem()),
            new MouseWithoutBordersSettings { Properties = properties },
            new PassthroughPairingKeyCodec())
        {
            PauseInstantSaving = true,
        };
    }
}
