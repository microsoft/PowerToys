// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.PowerToys.Settings.UI.Library.Utilities
{
    /// <summary>
    /// Helper class for registering/unregistering MCP server to VS Code and Windows Copilot.
    /// </summary>
    public static class McpRegistrationHelper
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <summary>
        /// Register or unregister MCP server to VS Code settings.json.
        /// </summary>
        /// <param name="register">True to register, false to unregister.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public static bool UpdateVSCodeRegistration(bool register)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var settingsPath = Path.Combine(appData, "Code", "User", "settings.json");

                if (!File.Exists(settingsPath))
                {
                    // Try VS Code Insiders
                    settingsPath = Path.Combine(appData, "Code - Insiders", "User", "settings.json");
                    if (!File.Exists(settingsPath))
                    {
                        // VS Code settings.json not found
                        return false;
                    }
                }

                return UpdateVSCodeSettingsFile(settingsPath, register);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Register or unregister MCP server to Windows Copilot.
        /// </summary>
        /// <param name="register">True to register, false to unregister.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public static bool UpdateWindowsCopilotRegistration(bool register)
        {
            // TODO: Implement Windows Copilot registration when API is available
            _ = register; // Suppress unused parameter warning
            return false;
        }

        private static bool UpdateVSCodeSettingsFile(string settingsPath, bool register)
        {
            try
            {
                // Backup original file
                var backupPath = settingsPath + ".bak";
                File.Copy(settingsPath, backupPath, true);

                // Read existing settings
                var settingsJson = File.ReadAllText(settingsPath);
                JsonNode rootNode = JsonNode.Parse(settingsJson);

                if (rootNode == null || rootNode is not JsonObject rootObject)
                {
                    return false;
                }

                // Get or create mcp.servers object
                if (!rootObject.ContainsKey("mcp.servers"))
                {
                    if (register)
                    {
                        rootObject["mcp.servers"] = new JsonObject();
                    }
                    else
                    {
                        // Nothing to unregister
                        return true;
                    }
                }

                var mcpServers = rootObject["mcp.servers"] as JsonObject;
                if (mcpServers == null)
                {
                    return false;
                }

                if (register)
                {
                    // Get PowerToys installation path
                    var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PowerToys.McpServer.exe");
                    if (!File.Exists(exePath))
                    {
                        return false;
                    }

                    // Add powertoys server entry
                    var serverConfig = new JsonObject
                    {
                        ["command"] = exePath.Replace("\\", "/"),
                    };
                    mcpServers["powertoys"] = serverConfig;
                }
                else
                {
                    // Remove powertoys server entry
                    mcpServers.Remove("powertoys");

                    // Remove mcp.servers if empty
                    if (mcpServers.Count == 0)
                    {
                        rootObject.Remove("mcp.servers");
                    }
                }

                // Write updated settings with proper formatting
                var updatedJson = JsonSerializer.Serialize(rootNode, JsonOptions);
                File.WriteAllText(settingsPath, updatedJson);

                return true;
            }
            catch
            {
                // Try to restore from backup
                var backupPath = settingsPath + ".bak";
                if (File.Exists(backupPath))
                {
                    try
                    {
                        File.Copy(backupPath, settingsPath, true);
                    }
                    catch
                    {
                        // Ignore backup restore errors
                    }
                }

                return false;
            }
        }
    }
}
