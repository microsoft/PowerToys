// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*
 * PowerRename UITest Usage Examples:
 * 
 * 1. Default test with predefined test files:
 *    var test = new Settings();
 * 
 * 2. Test with custom file paths:
 *    var test = new Settings(new string[] { @"D:\MyTestFolder", @"D:\TestFile.docx" });
 * 
 * 3. Using the helper method:
 *    var test = Settings.WithTestPaths(@"C:\Users\Test\Documents", @"C:\Users\Test\Desktop\file.txt");
 * 
 * The PowerRename application will be launched with the specified file paths as command line arguments,
 * allowing you to test PowerRename functionality with specific files/folders.
 * 
 * Note: Original files/folders will be copied to a temporary location to avoid modifying the originals.
 */

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;

namespace PowerRename.UITests;

[TestClass]
public class Settings
{
    public required TestContext TestContext { get; set; }
    public required Session Session { get; set; }

    private readonly string[] originalTestFilePaths;
    private string[] copiedTestFilePaths = Array.Empty<string>();
    private string testWorkingDirectory = string.Empty;
    private UITestBase? uiTestBase;

    /// <summary>
    /// Initialize PowerRename UITest with default test files
    /// </summary>
    public Settings()
        : this(new string[] { @"C:\Temp\TestFolder", @"C:\Temp\TestFile.txt" })
    {
    }

    /// <summary>
    /// Initialize PowerRename UITest with custom test file paths
    /// </summary>
    /// <param name="testFilePaths">Array of file/folder paths to test with</param>
    public Settings(string[] testFilePaths)
    {
        this.originalTestFilePaths = testFilePaths;
    }

    /// <summary>
    /// Helper method to create test instance with specific paths
    /// </summary>
    /// <param name="paths">Test file/folder paths</param>
    /// <returns>Settings instance configured with the specified paths</returns>
    public static Settings WithTestPaths(params string[] paths)
    {
        return new Settings(paths);
    }

    /// <summary>
    /// Test initialization - copy files and prepare test environment
    /// </summary>
    [TestInitialize]
    public void TestInit()
    {
        // Generate unique test working directory
        string testClassName = this.GetType().Name;
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string uniqueId = Guid.NewGuid().ToString("N")[..8];
        testWorkingDirectory = Path.Combine(Path.GetTempPath(), $"PowerRenameTest_{testClassName}_{timestamp}_{uniqueId}");

        // Clean up any existing test directories for this test class
        CleanupTestDirectories(testClassName);

        // Create working directory
        Directory.CreateDirectory(testWorkingDirectory);

        // Copy test files/folders to working directory
        copiedTestFilePaths = CopyTestFilesToWorkingDirectory();

        // Create UITestBase with copied file paths
        uiTestBase = new UITestBase(PowerToysModule.PowerRename, WindowSize.UnSpecified, copiedTestFilePaths);
        uiTestBase.TestContext = this.TestContext;
        uiTestBase.TestInit();
        
        this.Session = uiTestBase.Session;
    }

    /// <summary>
    /// Test cleanup - remove copied files and directories
    /// </summary>
    [TestCleanup]
    public void TestCleanup()
    {
        // Call base cleanup first
        uiTestBase?.TestCleanup();

        // Clean up test working directory
        if (Directory.Exists(testWorkingDirectory))
        {
            try
            {
                Directory.Delete(testWorkingDirectory, true);
                Console.WriteLine($"Cleaned up test directory: {testWorkingDirectory}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not delete test directory {testWorkingDirectory}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Copy test files and folders to the working directory
    /// </summary>
    /// <returns>Array of copied file/folder paths</returns>
    private string[] CopyTestFilesToWorkingDirectory()
    {
        var copiedPaths = new string[originalTestFilePaths.Length];

        for (int i = 0; i < originalTestFilePaths.Length; i++)
        {
            string originalPath = originalTestFilePaths[i];
            
            if (!File.Exists(originalPath) && !Directory.Exists(originalPath))
            {
                // Create a dummy file/folder for testing if original doesn't exist
                copiedPaths[i] = CreateDummyTestItem(originalPath, i);
                continue;
            }

            string fileName = Path.GetFileName(originalPath);
            string copiedPath = Path.Combine(testWorkingDirectory, $"{i}_{fileName}");

            try
            {
                if (Directory.Exists(originalPath))
                {
                    CopyDirectory(originalPath, copiedPath);
                    Console.WriteLine($"Copied directory: {originalPath} -> {copiedPath}");
                }
                else if (File.Exists(originalPath))
                {
                    File.Copy(originalPath, copiedPath);
                    Console.WriteLine($"Copied file: {originalPath} -> {copiedPath}");
                }

                copiedPaths[i] = copiedPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error copying {originalPath}: {ex.Message}");
                // Create a dummy item if copy fails
                copiedPaths[i] = CreateDummyTestItem(originalPath, i);
            }
        }

        return copiedPaths;
    }

    /// <summary>
    /// Create a dummy test file or folder if the original doesn't exist
    /// </summary>
    /// <param name="originalPath">Original path that was requested</param>
    /// <param name="index">Index for uniqueness</param>
    /// <returns>Path to the created dummy item</returns>
    private string CreateDummyTestItem(string originalPath, int index)
    {
        string fileName = Path.GetFileName(originalPath) ?? $"TestItem_{index}";
        string dummyPath = Path.Combine(testWorkingDirectory, $"{index}_{fileName}");

        try
        {
            if (Path.HasExtension(originalPath))
            {
                // Create a dummy file
                File.WriteAllText(dummyPath, $"Dummy test file created for testing PowerRename.\nOriginal path: {originalPath}\nCreated: {DateTime.Now}");
                Console.WriteLine($"Created dummy file: {dummyPath}");
            }
            else
            {
                // Create a dummy directory with some files
                Directory.CreateDirectory(dummyPath);
                File.WriteAllText(Path.Combine(dummyPath, "sample1.txt"), "Sample file 1 for testing");
                File.WriteAllText(Path.Combine(dummyPath, "sample2.txt"), "Sample file 2 for testing");
                File.WriteAllText(Path.Combine(dummyPath, "readme.md"), "This is a test directory created for PowerRename testing");
                Console.WriteLine($"Created dummy directory: {dummyPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating dummy test item {dummyPath}: {ex.Message}");
        }

        return dummyPath;
    }

    /// <summary>
    /// Recursively copy a directory and its contents
    /// </summary>
    /// <param name="sourceDir">Source directory path</param>
    /// <param name="destDir">Destination directory path</param>
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        // Copy files
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile);
        }

        // Copy subdirectories
        foreach (string subDir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
        }
    }

    /// <summary>
    /// Clean up any existing test directories for the specified test class
    /// </summary>
    /// <param name="testClassName">Name of the test class</param>
    private static void CleanupTestDirectories(string testClassName)
    {
        try
        {
            string tempPath = Path.GetTempPath();
            string searchPattern = $"PowerRenameTest_{testClassName}_*";
            
            foreach (string directory in Directory.GetDirectories(tempPath, searchPattern))
            {
                try
                {
                    Directory.Delete(directory, true);
                    Console.WriteLine($"Cleaned up old test directory: {directory}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not delete old test directory {directory}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error during cleanup: {ex.Message}");
        }
    }

    // Helper methods to delegate to UITestBase
    protected T Find<T>(string name, int timeoutMS = 5000, bool global = false) where T : Element, new()
    {
        return uiTestBase!.Session.Find<T>(OpenQA.Selenium.By.Name(name), timeoutMS, global);
    }

    protected Element Find(string name, int timeoutMS = 5000, bool global = false)
    {
        return uiTestBase!.Session.Find(OpenQA.Selenium.By.Name(name), timeoutMS, global);
    }

    [TestMethod]
    public void TestWarningDialog()
    {
        // Wait for PowerRename window to be ready
        System.Threading.Thread.Sleep(2000);
        
        // Verify that PowerRename has loaded with the copied test files
        Console.WriteLine($"Testing with {copiedTestFilePaths.Length} copied files/folders:");
        foreach (string path in copiedTestFilePaths)
        {
            Console.WriteLine($"  - {path}");
        }
        
        // The exact test logic will depend on PowerRename's UI structure
        this.Find<TextBox>("FilterBox").SetText("All Apps");
        
        // Add assertions to verify the test files are loaded
        // For example: Assert.IsTrue(this.Has("TestFolder"));
    }

    [TestMethod]
    public void TestWithCustomFiles()
    {
        // Example test demonstrating how to work with the copied test files
        System.Threading.Thread.Sleep(2000);
        
        Console.WriteLine($"Testing with {copiedTestFilePaths.Length} copied files/folders:");
        foreach (string path in copiedTestFilePaths)
        {
            Console.WriteLine($"  - {path}");
            
            // Verify the copied item exists
            if (File.Exists(path))
            {
                Console.WriteLine($"    File exists: {new FileInfo(path).Length} bytes");
            }
            else if (Directory.Exists(path))
            {
                int fileCount = Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length;
                Console.WriteLine($"    Directory exists: {fileCount} files");
            }
        }
        
        // Add your PowerRename UI testing logic here
        // Since these are copies, PowerRename can safely modify them
    }
}
