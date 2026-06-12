// WorkingDirectoryHelper.cs
// Fix for Issue #39305: Peek's "open with" leaves working directory in PowerToys
// Ensures launched applications get the correct working directory

using System;
using System.Diagnostics;
using System.IO;

namespace Peek.Common.Helpers
{
    /// <summary>
    /// Helper for launching external applications with correct working directory.
    /// </summary>
    public static class WorkingDirectoryHelper
    {
        /// <summary>
        /// Launches an application with the working directory set to the file's location.
        /// </summary>
        /// <param name="filePath">The file to open.</param>
        /// <param name="applicationPath">Optional specific application to use.</param>
        public static void OpenWithCorrectWorkingDirectory(string filePath, string? applicationPath = null)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return;
            }
            
            var directory = Path.GetDirectoryName(filePath);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = applicationPath ?? filePath,
                WorkingDirectory = directory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                UseShellExecute = true,
            };
            
            if (!string.IsNullOrEmpty(applicationPath))
            {
                startInfo.Arguments = $"\"{filePath}\"";
            }
            
            try
            {
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                // Log error but don't throw - graceful degradation
                System.Diagnostics.Debug.WriteLine($"Failed to open file: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Opens the "Open with" dialog with correct working directory.
        /// </summary>
        public static void ShowOpenWithDialog(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return;
            }
            
            var directory = Path.GetDirectoryName(filePath);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "rundll32.exe",
                Arguments = $"shell32.dll,OpenAs_RunDLL \"{filePath}\"",
                WorkingDirectory = directory ?? string.Empty,
                UseShellExecute = false,
            };
            
            try
            {
                Process.Start(startInfo);
            }
            catch
            {
                // Fallback to default behavior
            }
        }
    }
}
