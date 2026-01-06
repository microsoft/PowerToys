// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using ImageResizer.Properties;
using ManagedCommon;

namespace ImageResizer.Services
{
    /// <summary>
    /// Service for caching AI availability detection results.
    /// Persists results to avoid slow API calls on every startup.
    /// Runner calls ImageResizer --detect-ai to perform detection,
    /// and ImageResizer reads the cached result on normal startup.
    /// </summary>
    public static class AiAvailabilityCacheService
    {
        private const string CacheFileName = "ai_capabilities.json";
        private const int CacheVersion = 1;

        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        private static string CachePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys",
            CacheFileName);

        /// <summary>
        /// Load AI availability state from cache.
        /// Returns null if cache doesn't exist, is invalid, or read fails.
        /// </summary>
        public static AiAvailabilityState? LoadCache()
        {
            try
            {
                if (!File.Exists(CachePath))
                {
                    return null;
                }

                var json = File.ReadAllText(CachePath);
                var cache = JsonSerializer.Deserialize<AiCapabilityCache>(json);

                if (!IsCacheValid(cache))
                {
                    return null;
                }

                return (AiAvailabilityState)cache.State;
            }
            catch (Exception ex)
            {
                // Read failure (file locked, corrupted JSON, etc.) - return null and use fallback
                Logger.LogError($"Failed to load AI cache: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Save AI availability state to cache.
        /// Called by --detect-ai mode after performing detection.
        /// </summary>
        public static void SaveCache(AiAvailabilityState state)
        {
            try
            {
                var cache = new AiCapabilityCache
                {
                    Version = CacheVersion,
                    State = (int)state,
                    WindowsBuild = Environment.OSVersion.Version.ToString(),
                    Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    Timestamp = DateTime.UtcNow.ToString("o"),
                };

                var dir = Path.GetDirectoryName(CachePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(cache, SerializerOptions);
                File.WriteAllText(CachePath, json);

                Logger.LogInfo($"AI cache saved: {state}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save AI cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Validate cache against current system environment.
        /// Cache is invalid if version, architecture, or Windows build changed.
        /// </summary>
        private static bool IsCacheValid(AiCapabilityCache cache)
        {
            if (cache == null || cache.Version != CacheVersion)
            {
                return false;
            }

            if (cache.Architecture != RuntimeInformation.ProcessArchitecture.ToString())
            {
                return false;
            }

            if (cache.WindowsBuild != Environment.OSVersion.Version.ToString())
            {
                return false;
            }

            return true;
        }
    }
}
