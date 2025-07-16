// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerRename.UITests;

[TestClass]
public class PowerRenameUITestBase : UITestBase
{
    private static readonly string[] OriginalTestFilePaths = new string[]
    {
        Path.Combine("testItems", "folder1"), // Test folder
        Path.Combine("testItems", "folder2"), // Test folder
        Path.Combine("testItems", "testCase1.txt"), // Test file
    };

    private static readonly string BaseTestFileFolderPath = Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "test", typeof(BasicRenameTests).Name);

    private static List<string> TestFilesAndFoldersArray { get; } = InitCleanTestEnvironment();

    private static List<string> InitCleanTestEnvironment()
    {
        var testFilesAndFolders = new List<string>
        {
        };

        foreach (var files in OriginalTestFilePaths)
        {
            var targetFolder = Path.Combine(BaseTestFileFolderPath, files);
            testFilesAndFolders.Add(targetFolder);
        }

        return testFilesAndFolders;
    }

    [TestInitialize]
    public void InitTestCase()
    {
        // Clean up any existing test directories for this test class
        CleanupTestDirectories();

        // copy files and folders from OriginalTestFilePaths to testFilesAndFoldersArray
        CopyTestFilesToDestination();

        RestartScopeExe();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PowerRenameUITestBase"/> class.
    /// Initialize PowerRename UITest with default test files
    /// </summary>
    public PowerRenameUITestBase()
        : base(PowerToysModule.PowerRename, WindowSize.UnSpecified, TestFilesAndFoldersArray.ToArray())
    {
    }

    /// <summary>
    /// Clean up any existing test directories for the specified test class
    /// </summary>
    private static void CleanupTestDirectories()
    {
        try
        {
            if (Directory.Exists(BaseTestFileFolderPath))
            {
                Directory.Delete(BaseTestFileFolderPath, true);
                Console.WriteLine($"Cleaned up old test directory: {BaseTestFileFolderPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error during cleanup: {ex.Message}");
        }

        try
        {
            Directory.CreateDirectory(BaseTestFileFolderPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error during cleanup create folder: {ex.Message}");
        }
    }

    /// <summary>
    /// Copy test files and folders from source paths to destination paths
    /// </summary>
    private static void CopyTestFilesToDestination()
    {
        try
        {
            for (int i = 0; i < OriginalTestFilePaths.Length && i < TestFilesAndFoldersArray.Count; i++)
            {
                var sourcePath = Path.GetFullPath(OriginalTestFilePaths[i]);
                var destinationPath = TestFilesAndFoldersArray[i];

                var destinationDir = Path.GetDirectoryName(destinationPath);
                if (destinationDir != null && !Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                if (Directory.Exists(sourcePath))
                {
                    CopyDirectory(sourcePath, destinationPath);
                    Console.WriteLine($"Copied directory from {sourcePath} to {destinationPath}");
                }
                else if (File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, destinationPath, true);
                    Console.WriteLine($"Copied file from {sourcePath} to {destinationPath}");
                }
                else
                {
                    Console.WriteLine($"Warning: Source path does not exist: {sourcePath}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during file copy operation: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Recursively copy a directory and its contents
    /// </summary>
    /// <param name="sourceDir">Source directory path</param>
    /// <param name="destDir">Destination directory path</param>
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        try
        {
            // Create target directory
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // Copy all files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, true);
            }

            // Recursively copy all subdirectories
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                var destSubDir = Path.Combine(destDir, dirName);
                CopyDirectory(dir, destSubDir);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error copying directory from {sourceDir} to {destDir}: {ex.Message}");
            throw;
        }
    }

    protected void SetSearchBoxText(string text)
    {
        Assert.IsTrue(this.Find<TextBox>("Search for").SetText(text, true).Text == text);
    }

    protected void SetReplaceBoxText(string text)
    {
        Assert.IsTrue(this.Find<TextBox>("Replace with").SetText(text, true).Text == text);
    }

    protected void SetRegularExpressionCheckbox(bool flag)
    {
        Assert.IsTrue(this.Find<CheckBox>("Use regular expressions").SetCheck(flag).IsChecked == flag);
    }

    protected void SetMatchAllOccurrencesCheckbox(bool flag)
    {
        Assert.IsTrue(this.Find<CheckBox>("Match all occurrences").SetCheck(flag).IsChecked == flag);
    }

    protected void SetCaseSensitiveCheckbox(bool flag)
    {
        Assert.IsTrue(this.Find<CheckBox>("Case sensitive").SetCheck(flag).IsChecked == flag);
    }

    protected void CheckOriginalOrRenamedCount(int count)
    {
        Assert.IsTrue(this.Find<TextBlock>($"({count})").Text == $"({count})");
    }
}
