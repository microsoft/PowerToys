using System;
using System.Diagnostics;
using System.IO;

class TestFoundryProcess
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Testing foundry.exe Process.Start ===");
        Console.WriteLine();
        
        Console.WriteLine($"Current Directory: {Environment.CurrentDirectory}");
        Console.WriteLine();
        
        // Test 1: UseShellExecute = false (like SDK does)
        Console.WriteLine("Test 1: UseShellExecute = false");
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = "foundry";
            process.StartInfo.Arguments = "--version";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            
            Console.WriteLine($"  Attempting to start foundry...");
            process.Start();
            
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            
            Console.WriteLine($"  SUCCESS!");
            Console.WriteLine($"  Output: {output}");
            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine($"  Error: {error}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAILED: {ex.Message}");
            Console.WriteLine($"  Type: {ex.GetType().Name}");
        }
        
        Console.WriteLine();
        
        // Test 2: Change directory to parent
        Console.WriteLine("Test 2: Change to AppData\\Local and retry");
        string originalDir = Environment.CurrentDirectory;
        try
        {
            string testDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PowerToys");
            if (Directory.Exists(testDir))
            {
                Environment.CurrentDirectory = testDir;
                Console.WriteLine($"  Changed directory to: {Environment.CurrentDirectory}");
                
                using var process = new Process();
                process.StartInfo.FileName = "foundry";
                process.StartInfo.Arguments = "--version";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                
                Console.WriteLine($"  Attempting to start foundry...");
                process.Start();
                
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                
                Console.WriteLine($"  SUCCESS!");
                Console.WriteLine($"  Output: {output}");
            }
            else
            {
                Console.WriteLine($"  Directory doesn't exist: {testDir}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAILED: {ex.Message}");
            Console.WriteLine($"  Type: {ex.GetType().Name}");
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
        }
        
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
