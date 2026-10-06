// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace Microsoft.PowerToys.RegistryPreview.UITests;

/// <summary>Registry Preview editor, parser, file, value-grid, and registry-write scenarios.</summary>
/// <remarks>Covers checklist items 1-5 of issue #40675.</remarks>
[TestClass]
[DoNotParallelize]
public sealed class RegistryPreviewEditorTests : RegistryPreviewTestBase
{
    [TestMethod("RegistryPreview.Editor.SaveEditAndSaveAs")]
    [TestCategory("RegistryPreview")]
    public void EditorRepopulatesTreeAndSupportsSaveEditAndSaveAs()
    {
        var folder = CreateTestFolder();
        var keyPath = CreateIsolatedRegistryKeyPath();
        var fixture = CreateRegFixture(folder, "source.reg", keyPath);
        var savedAs = Path.Combine(folder, "saved-as.reg");
        var typedKeyPath = keyPath + @"\TypedKey";
        var replacement = CreateRegContent(typedKeyPath, "typed-value");
        var window = LaunchRegistryPreviewWithEditor(fixture);

        try
        {
            AssertExactElement(window, Path.GetFileName(keyPath), "Initial visual-tree key");

            ReplaceEditorContent(window, replacement);
            var save = window.Find<Button>(By.AccessibilityId("saveButton"), ActionTimeoutMS);
            Assert.IsTrue(
                window.WaitFor(() => save.IsEnabled, ActionTimeoutMS, pollIntervalMS: 200),
                "Save did not become enabled after editing the Monaco document.");

            // This assertion intentionally precedes Save: it proves the visual tree is repopulated
            // from the editor's TextChanged event while the user is still typing.
            AssertExactElement(window, "TypedKey", "Live visual-tree key", EditorTimeoutMS);

            Step("Saving the edited file in place");
            save.Click(msPostAction: 300);
            Assert.IsTrue(
                window.WaitFor(() => !save.IsEnabled, ActionTimeoutMS, pollIntervalMS: 200),
                "Save remained enabled after saving the edited file.");
            Assert.AreEqual(
                NormalizeRegContent(replacement),
                NormalizeRegContent(File.ReadAllText(fixture)),
                "Save did not persist the Monaco document.");

            Step("Opening the saved file through the Edit command");
            Session? notepad = null;
            for (var attempt = 1; attempt <= 3 && notepad is null; attempt++)
            {
                window.Find<Button>(By.AccessibilityId("editButton"), ActionTimeoutMS).Click(msPostAction: 300);
                notepad = WindowsFinder.WaitForWindowByApp(
                    "notepad",
                    candidate => candidate.Width > 0 && candidate.Height > 0,
                    timeoutMS: 5_000);
            }

            Assert.IsNotNull(notepad, "Edit did not open the saved .reg file in its configured editor.");
            WindowControl.TryCloseByApp("notepad", timeoutMS: 3_000);

            Step("Saving a copy through Save As");
            window.Find<Button>(By.AccessibilityId("saveAsButton"), ActionTimeoutMS).Click(msPostAction: 200);
            CompleteFileDialogWithPath(savedAs);
            Assert.IsTrue(
                window.WaitFor(() => File.Exists(savedAs), ActionTimeoutMS, pollIntervalMS: 200),
                $"Save As did not create '{savedAs}'.");
            Assert.AreEqual(
                NormalizeRegContent(replacement),
                NormalizeRegContent(File.ReadAllText(savedAs)),
                "Save As did not preserve the edited content.");
            Assert.IsTrue(
                window.WaitFor(
                    () => WindowsFinder.ListByApp(RegistryPreviewProcessName)
                        .Any(candidate => candidate.Title.Contains(Path.GetFileName(savedAs), StringComparison.OrdinalIgnoreCase)),
                    ActionTimeoutMS,
                    pollIntervalMS: 200),
                "Registry Preview did not switch to the Save As destination.");
        }
        finally
        {
            WindowControl.TryCloseByApp("notepad", timeoutMS: 2_000);
        }
    }

    [TestMethod("RegistryPreview.Editor.ReloadAndOpen")]
    [TestCategory("RegistryPreview")]
    public void ReloadAndOpenReplaceTheDisplayedFile()
    {
        var folder = CreateTestFolder();
        var firstKeyPath = CreateIsolatedRegistryKeyPath();
        var secondKeyPath = CreateIsolatedRegistryKeyPath();
        var first = CreateRegFixture(folder, "first.reg", firstKeyPath);
        var second = CreateRegFixture(folder, "second.reg", secondKeyPath + @"\OpenedKey", "opened-value");
        var window = LaunchRegistryPreviewWithEditor(first);

        Step("Changing the first file externally and reloading it");
        File.WriteAllText(first, CreateRegContent(firstKeyPath + @"\ReloadedKey", "external-value"), Encoding.Unicode);
        window.Find<Button>(By.AccessibilityId("refreshButton"), ActionTimeoutMS).Click(msPostAction: 300);
        AssertExactElement(window, "ReloadedKey", "Externally reloaded visual-tree key", EditorTimeoutMS);

        Step("Opening a different .reg file through the Open dialog");
        window.Find<Button>(By.AccessibilityId("openButton"), ActionTimeoutMS).Click(msPostAction: 200);
        CompleteFileDialogWithPath(second);
        AssertExactElement(window, "OpenedKey", "Newly opened visual-tree key", EditorTimeoutMS);
        Assert.IsTrue(
            window.WaitFor(
                () => WindowsFinder.ListByApp(RegistryPreviewProcessName)
                    .Any(candidate => candidate.Title.Contains(Path.GetFileName(second), StringComparison.OrdinalIgnoreCase)),
                ActionTimeoutMS,
                pollIntervalMS: 200),
            "Registry Preview did not switch its title to the newly opened file.");
    }

    [TestMethod("RegistryPreview.Editor.RegistryValues")]
    [TestCategory("RegistryPreview")]
    public void SelectingAKeyShowsItsStringAndBinaryValues()
    {
        var folder = CreateTestFolder();
        var keyPath = CreateIsolatedRegistryKeyPath();
        var fixture = CreateRegFixture(folder, "values.reg", keyPath);
        var window = LaunchRegistryPreview(fixture);
        var leafName = Path.GetFileName(keyPath);

        var key = FindExact<TextBlock>(window, leafName, ActionTimeoutMS);
        Assert.IsNotNull(key, $"The visual tree did not expose key '{leafName}'.");
        key!.Click(msPostAction: 500);

        // REG_SZ/REG_BINARY and the normalized binary rendering exist only in the value grid, not
        // in the source document, so these assertions prove that selecting the tree key populated
        // the bottom-right value surface rather than merely finding Monaco text.
        AssertExactElement(window, "SampleString", "String value name");
        AssertExactElement(window, "REG_SZ", "String value type");
        AssertExactElement(window, "sample-value", "String value data");
        AssertExactElement(window, "SampleBinary", "Binary value name");
        AssertExactElement(window, "REG_BINARY", "Binary value type");
        AssertExactElement(window, "01 02 03 04", "Normalized binary value data");
    }

    [TestMethod("RegistryPreview.Editor.WriteToRegistry")]
    [TestCategory("RegistryPreview")]
    public void WriteToRegistryImportsOnlyTheIsolatedHkcuFixture()
    {
        Assert.IsFalse(
            EnvironmentConfig.IsInPipeline &&
            IsCurrentUserLocalAdministrator() &&
            !ElevationHelper.IsCurrentProcessElevated(),
            "Unattended runs need either a standard-user token or an already-elevated token; a filtered administrator token moves Registry Editor consent to the secure desktop.");

        var folder = CreateTestFolder();
        var keyPath = CreateIsolatedRegistryKeyPath();
        var subKey = RegistrySubKeyFromFullPath(keyPath);
        var fixture = CreateRegFixture(folder, "write.reg", keyPath, "written-value");
        var window = LaunchRegistryPreview(fixture);

        Registry.CurrentUser.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false);
        using (var existing = Registry.CurrentUser.OpenSubKey(subKey))
        {
            Assert.IsNull(existing, "The isolated registry key already existed before import.");
        }

        Step("Writing the isolated HKCU fixture through Registry Preview");
        if (CanConfirmRegistryImportInteractively)
        {
            Step("Approve Registry Editor elevation if prompted. The test can wait for manual confirmation when UIPI blocks automation.");
        }

        window.Find<Button>(By.AccessibilityId("writeButton"), ActionTimeoutMS).Click(msPostAction: 300);
        var confirmationWasAutomated = ConfirmRegistryImport();

        Assert.IsTrue(
            window.WaitFor(
                () =>
                {
                    using var key = Registry.CurrentUser.OpenSubKey(subKey);
                    return string.Equals(key?.GetValue("SampleString") as string, "written-value", StringComparison.Ordinal);
                },
                confirmationWasAutomated ? ActionTimeoutMS : InteractiveRegistryImportTimeoutMS,
                pollIntervalMS: 250),
            "Registry Editor did not import the string value into the isolated HKCU key. " +
            "If UAC requested credentials for a different administrator account, run Visual Studio elevated under the test account so HKCU remains the same user.");

        using var imported = Registry.CurrentUser.OpenSubKey(subKey);
        Assert.IsNotNull(imported, "Registry Editor did not create the isolated HKCU key.");
        CollectionAssert.AreEqual(
            new byte[] { 1, 2, 3, 4 },
            imported!.GetValue("SampleBinary") as byte[],
            "Registry Editor did not import the binary value.");
    }
}
