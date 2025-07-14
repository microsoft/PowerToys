// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Peek.UITests;

[TestClass]
public class PeekFilePreviewTests : UITestBase
{
    public PeekFilePreviewTests()
        : base(PowerToysModule.PowerToysSettings, WindowSize.Small_Vertical)
    {
    }

    [TestMethod("Peek.FilePreview.Folder")]
    [TestCategory("Preview files")]
    public void PeekFolderFilePreview()
    {
        string folderFullPath = Path.GetFullPath(@".\TestAssets");

        OpenAndPeekFile(folderFullPath, "TestAssets - Peek");
        Assert.IsTrue(FindAll<TextBlock>("File Type: File folder", 500, true).Count > 0, "Should show folder detail in Peek File Preview");
    }

    /// <summary>
    /// Comprehensive test for all files in TestAssets with visual comparison
    /// Tests all supported file types and validates preview rendering with image comparison
    /// </summary>
    [TestMethod("Peek.FilePreview.AllTestAssets")]
    [TestCategory("Preview files")]
    public void PeekAllTestAssetsWithVisualComparison()
    {
        // Get all test asset files
        string testAssetsPath = Path.GetFullPath(@".\TestAssets");
        var testFiles = Directory.GetFiles(testAssetsPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(file => !Path.GetFileName(file).StartsWith('.'))
            .OrderBy(file => file)
            .ToList();

        Console.WriteLine($"Found {testFiles.Count} test files in TestAssets:");
        foreach (var file in testFiles)
        {
            Console.WriteLine($"  - {Path.GetFileName(file)} ({Path.GetExtension(file)})");
        }

        Assert.IsTrue(testFiles.Count > 0, "Should have test files in TestAssets folder");

        // Test each file individually with visual comparison
        foreach (var testFile in testFiles)
        {
            string fileName = Path.GetFileName(testFile);
            string fileExtension = Path.GetExtension(testFile).ToLowerInvariant();

            Console.WriteLine($"Testing preview for: {fileName}");

            try
            {
                // Perform visual assertion with file-specific scenario name
                string scenarioName = $"TestAssets_{Path.GetFileNameWithoutExtension(fileName)}_{fileExtension.Replace(".", string.Empty)}";
                TestFilePreviewWithVisualComparison(testFile, scenarioName);

                Console.WriteLine($"✓ Successfully tested {fileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Failed to test {fileName}: {ex.Message}");

                // Try to close any open peek window before continuing
                try
                {
                    Session.CloseMainWindow();
                }
                catch
                {
                    // Ignore cleanup errors
                }

                // Re-throw for test failure
                throw new AssertFailedException($"Preview test failed for {fileName}: {ex.Message}", ex);
            }
        }

        Console.WriteLine($"Successfully completed visual comparison tests for {testFiles.Count} files");
    }

    private void OpenAndPeekFile(string fullPath, string peekWindowTitle)
    {
        SendKeys(Key.Enter);

        Session.StartExe("explorer.exe", $"/select,\"{fullPath}\"");

        // Wait a moment for Explorer to open
        Task.Delay(1000).Wait();

        SendKeys(Key.LCtrl, Key.Space);

        Task.Delay(1000).Wait();

        Session.Attach(peekWindowTitle);
    }

    /// <summary>
    /// Opens a file with Peek and performs visual comparison with baseline image
    /// </summary>
    /// <param name="filePath">Full path to the file to preview</param>
    /// <param name="scenarioName">Unique scenario name for baseline image matching</param>
    private void TestFilePreviewWithVisualComparison(string filePath, string scenarioName)
    {
        string fileName = Path.GetFileName(filePath);
        string peekWindowTitle = $"{fileName} - Peek";

        // Open file with Peek
        OpenAndPeekFile(filePath, peekWindowTitle);

        // Get the preview window for comparison
        var previewWindow = Find(peekWindowTitle, 2000, true);
        Assert.IsNotNull(previewWindow, $"Should open Peek window for {fileName}");

        // Perform visual assertion
        // Note: Baseline images are embedded resources and must be created during the first run in the pipeline
        // The scenario name should be unique to avoid conflicts between different file types
        VisualAssert.AreEqual(TestContext, previewWindow, scenarioName);

        // Close peek window
        Session.CloseMainWindow();
    }
}
