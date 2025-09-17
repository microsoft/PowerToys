// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Awake.Core
{
    public class PowerSchemeManager
    {
        private readonly List<PowerScheme> _schemes = new();

        public PowerSchemeManager()
        {
            RefreshSchemes();
        }

        public void RefreshSchemes()
        {
            _schemes.Clear();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/L",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var matches = Regex.Matches(output, @"Power Scheme GUID: ([a-fA-F0-9\-]+)\s+\((.+?)\)(\s+\*)?");
            foreach (Match match in matches)
            {
                _schemes.Add(new PowerScheme
                {
                    PSGuid = match.Groups[1].Value,
                    Name = match.Groups[2].Value,
                    IsActive = match.Groups[3].Value.Contains('*'),
                });
            }

            // Rank schemes by performance (descending)
            _schemes.Sort((a, b) => GetScore(b).CompareTo(GetScore(a)));
        }

        /// <summary>
        /// Returns all power schemes sorted by performance (highest first).
        /// </summary>
        public IReadOnlyList<PowerScheme> GetAllSchemes() => _schemes;

        /// <summary>
        /// Returns the highest performance scheme currently available (may already be active).
        /// </summary>
        public PowerScheme? GetHighestPerformanceScheme()
        {
            if (_schemes.Count == 0)
            {
                RefreshSchemes();
            }

            return _schemes.Count == 0 ? null : _schemes[0];
        }

        public bool SwitchScheme(string psGuid)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powercfg",
                        Arguments = $"/setactive {psGuid}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    },
                };
                process.Start();
                process.WaitForExit();
                RefreshSchemes();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int GetScore(PowerScheme scheme)
        {
            // Heuristic based on name (multi-language basic keywords).
            string name = scheme.Name.ToLowerInvariant();

            // High performance indicators
            if (name.Contains("ultimate") || name.Contains("ultra"))
            {
                return 380;
            }

            if (name.Contains("high"))
            {
                return 310;
            }

            if (name.Contains("balanced"))
            {
                return 200;
            }

            if (name.Contains("saver"))
            {
                return 120;
            }

            // Default for unknown custom plans.
            return 180;
        }

        public class PowerScheme
        {
            public string PSGuid { get; set; } = string.Empty;

            public string Name { get; set; } = string.Empty;

            public bool IsActive { get; set; }
        }
    }
}
