// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TopToolbar.Models;
using TopToolbar.Providers;
using TopToolbar.Services.Profiles;

namespace TopToolbar.Test
{
    internal sealed class WorkspaceProviderTest
    {
        /// <summary>
        /// Simple test method to verify workspace-to-profile synchronization.
        /// This demonstrates the enhanced functionality.
        /// </summary>
        public static async Task TestWorkspaceProfileSyncAsync()
        {
            try
            {
                // Create a temporary workspace file for testing
                var tempDir = Path.GetTempPath();
                var testWorkspacePath = Path.Combine(tempDir, "test_workspaces.json");
                var testProfileDir = Path.Combine(tempDir, "test_profiles");

                Directory.CreateDirectory(testProfileDir);

                // Create initial workspace data
                var initialWorkspaces = new
                {
                    workspaces = new[]
                    {
                        new { id = "workspace1", name = "Development" },
                        new { id = "workspace2", name = "Research" },
                    },
                };

                await File.WriteAllTextAsync(testWorkspacePath, JsonSerializer.Serialize(initialWorkspaces));

                // Create test profile service
                var profileFileService = new ProfileFileService(testProfileDir);

                // Create test profile
                var testProfile = profileFileService.CreateEmptyProfile("test-profile", "Test Profile");
                profileFileService.SaveProfile(testProfile);

                // Create WorkspaceProvider with test dependencies
                using var workspaceProvider = new WorkspaceProvider(testWorkspacePath, profileFileService);

                // Wait a moment for initial load
                await Task.Delay(100);

                // Simulate workspace changes - add a new workspace
                var updatedWorkspaces = new
                {
                    workspaces = new[]
                    {
                        new { id = "workspace1", name = "Development" },
                        new { id = "workspace2", name = "Research" },
                        new { id = "workspace3", name = "Testing" }, // New workspace
                    },
                };

                await File.WriteAllTextAsync(testWorkspacePath, JsonSerializer.Serialize(updatedWorkspaces));

                // Wait for file watcher to trigger
                await Task.Delay(500);

                // Verify that the profile was updated
                var updatedProfile = profileFileService.GetProfile("test-profile");
                if (updatedProfile != null)
                {
                    var workspacesGroup = updatedProfile.Groups?.Find(g => g.Id == "workspaces");
                    if (workspacesGroup?.Actions != null)
                    {
                        var workspace3Action = workspacesGroup.Actions.Find(a => a.Id == "workspace::workspace3");
                        if (workspace3Action != null && workspace3Action.IsEnabled)
                        {
                            Console.WriteLine("Γ£ô Success: New workspace was automatically added to profile and enabled");
                        }
                        else
                        {
                            Console.WriteLine("Γ£ù Failed: New workspace was not properly added to profile");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Γ£ù Failed: Workspaces group not found in profile");
                    }
                }
                else
                {
                    Console.WriteLine("Γ£ù Failed: Could not retrieve updated profile");
                }

                // Clean up
                try
                {
                    File.Delete(testWorkspacePath);
                    Directory.Delete(testProfileDir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test failed with exception: {ex.Message}");
            }
        }
    }
}
