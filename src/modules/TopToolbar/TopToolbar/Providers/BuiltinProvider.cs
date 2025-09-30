// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TopToolbar.Actions;
using TopToolbar.Logging;
using TopToolbar.Models;
using TopToolbar.Providers.Configuration;
using TopToolbar.Providers.External.Mcp;
using TopToolbar.Services;

namespace TopToolbar.Providers
{
    /// <summary>
    /// Built-in provider that manages workspace and MCP providers automatically.
    /// This replaces individual provider registration in the main toolbar class.
    /// </summary>
    public sealed class BuiltinProvider : IDisposable
    {
        private readonly List<IActionProvider> _providers = new();
        private readonly List<IDisposable> _disposables = new();
        private bool _disposed;

        /// <summary>
        /// Gets all registered built-in providers
        /// </summary>
        public IReadOnlyList<IActionProvider> Providers => _providers.AsReadOnly();

        /// <summary>
        /// Initializes and loads all built-in providers (workspace and MCP providers)
        /// </summary>
        public void Initialize()
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(BuiltinProvider));

            // Clear any existing providers
            DisposeProviders();

            LoadWorkspaceProvider();
            LoadMcpProviders();
        }

        /// <summary>
        /// Registers all loaded providers to the ActionProviderRuntime
        /// </summary>
        public void RegisterProvidersTo(ActionProviderRuntime runtime)
        {
            ArgumentNullException.ThrowIfNull(runtime);

            foreach (var provider in _providers)
            {
                try
                {
                    runtime.RegisterProvider(provider);
                }
                catch (Exception ex)
                {
                    // Log error but continue with other providers
                    try
                    {
                        AppLogger.LogWarning($"BuiltinProvider: Failed to register provider '{provider.Id}': {ex.Message}");
                    }
                    catch
                    {
                        // Ignore logging errors
                    }
                }
            }
        }

        /// <summary>
        /// Gets default profile groups for all built-in providers
        /// </summary>
        public async Task<List<ProfileGroup>> GetDefaultProfileGroupsAsync()
        {
            var groups = new List<ProfileGroup>();

            // Get workspace groups
            try
            {
                var workspaceGroups = await WorkspaceProvider.GetDefaultWorkspaceGroupsAsync();
                groups.AddRange(workspaceGroups);
            }
            catch (Exception ex)
            {
                try
                {
                    AppLogger.LogWarning($"BuiltinProvider: Failed to get workspace groups: {ex.Message}");
                }
                catch
                {
                    // Ignore logging errors
                }
            }

            // Get MCP provider groups
            try
            {
                var mcpGroups = await GetDynamicMcpProviderGroupsAsync();
                groups.AddRange(mcpGroups);
            }
            catch (Exception ex)
            {
                try
                {
                    AppLogger.LogWarning($"BuiltinProvider: Failed to get MCP provider groups: {ex.Message}");
                }
                catch
                {
                    // Ignore logging errors
                }
            }

            return groups;
        }

        /// <summary>
        /// Loads the workspace provider
        /// </summary>
        private void LoadWorkspaceProvider()
        {
            try
            {
                var workspaceProvider = new WorkspaceProvider();
                _providers.Add(workspaceProvider);
                _disposables.Add(workspaceProvider);
            }
            catch (Exception ex)
            {
                try
                {
                    AppLogger.LogWarning($"BuiltinProvider: Failed to load workspace provider: {ex.Message}");
                }
                catch
                {
                    // Ignore logging errors
                }
            }
        }

        /// <summary>
        /// Loads all MCP providers from configuration files
        /// </summary>
        private void LoadMcpProviders()
        {
            try
            {
                var configService = new ProviderConfigService();
                var configs = configService.LoadConfigs();

                foreach (var config in configs)
                {
                    if (config == null || string.IsNullOrWhiteSpace(config.Id))
                    {
                        continue;
                    }

                    try
                    {
                        var provider = CreateProvider(config);
                        _providers.Add(provider);

                        if (provider is IDisposable disposable)
                        {
                            _disposables.Add(disposable);
                        }
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            AppLogger.LogWarning($"BuiltinProvider: Failed to create provider '{config.Id}': {ex.Message}");
                        }
                        catch
                        {
                            // Ignore logging errors
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    AppLogger.LogWarning($"BuiltinProvider: Failed to load MCP providers: {ex.Message}");
                }
                catch
                {
                    // Ignore logging errors
                }
            }
        }

        /// <summary>
        /// Creates a provider instance from configuration
        /// </summary>
        private static IActionProvider CreateProvider(ProviderConfig config)
        {
            if (config?.External?.Type == ExternalProviderType.Mcp)
            {
                return new McpActionProvider(config);
            }

            return new ConfiguredActionProvider(config);
        }

        /// <summary>
        /// Gets dynamic MCP provider groups by discovering MCP configurations and creating profile groups with default enabled actions
        /// </summary>
        private async Task<List<ProfileGroup>> GetDynamicMcpProviderGroupsAsync()
        {
            var groups = new List<ProfileGroup>();

            try
            {
                var configService = new ProviderConfigService();
                var configs = configService.LoadConfigs();

                // Filter for MCP providers
                var mcpConfigs = configs.Where(c => c?.External?.Type == ExternalProviderType.Mcp).ToList();

                foreach (var config in mcpConfigs)
                {
                    if (string.IsNullOrWhiteSpace(config.Id))
                    {
                        continue;
                    }

                    // Check if a group with this ID already exists
                    if (groups.Any(g => string.Equals(g.Id, config.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue; // Skip duplicates
                    }

                    try
                    {
                        // Create MCP provider instance to get actual tools
                        using var mcpProvider = new McpActionProvider(config);
                        var context = new ActionContext();

                        // Get actual group with real MCP tools
                        var mcpButtonGroup = await mcpProvider.CreateGroupAsync(context, CancellationToken.None);

                        // Convert ButtonGroup to ProfileGroup with all actions enabled by default
                        var profileGroup = new ProfileGroup
                        {
                            Id = mcpButtonGroup.Id,
                            Name = mcpButtonGroup.Name,
                            Description = mcpButtonGroup.Description,
                            IsEnabled = true, // Default enabled for new MCP provider groups
                            SortOrder = groups.Count + 10, // Place after workspace groups
                            Actions = new List<ProfileAction>(),
                        };

                        // Convert each ToolbarButton to ProfileAction (all enabled by default)
                        foreach (var button in mcpButtonGroup.Buttons)
                        {
                            var profileAction = new ProfileAction
                            {
                                Id = button.Id,
                                Name = button.Name,
                                Description = button.Description,
                                IsEnabled = true, // Default enabled for new MCP actions
                                IconGlyph = button.IconGlyph,
                            };
                            profileGroup.Actions.Add(profileAction);
                        }

                        groups.Add(profileGroup);
                    }
                    catch (Exception ex)
                    {
                        // Log error for individual MCP provider but continue with others
                        try
                        {
                            AppLogger.LogWarning($"BuiltinProvider: Failed to create group for MCP provider '{config.Id}': {ex.Message}");
                        }
                        catch
                        {
                            // Ignore logging errors
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log general error but don't throw - let caller handle
                try
                {
                    AppLogger.LogWarning($"BuiltinProvider: Failed to load dynamic MCP provider groups: {ex.Message}");
                }
                catch
                {
                    // Ignore logging errors
                }

                throw; // Re-throw for caller to handle
            }

            return groups;
        }

        /// <summary>
        /// Disposes all loaded providers
        /// </summary>
        private void DisposeProviders()
        {
            foreach (var disposable in _disposables)
            {
                try
                {
                    disposable?.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }

            _disposables.Clear();
            _providers.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisposeProviders();
            GC.SuppressFinalize(this);
        }
    }
}
